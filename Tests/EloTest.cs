using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class EloTest(ITestOutputHelper output) : EvosTest(output)
{
    private const string EloKey = "pvp";
    private const string OtherKey = "other";
    private const string SubType = "testSubType";

    // --- GetPrediction (public, testable directly) ---

    [Fact]
    public void GetPrediction_EqualElo_IsHalf()
    {
        Assert.Equal(0.5f, Elo.GetPrediction(1500f, 1500f), precision: 4);
    }

    [Fact]
    public void GetPrediction_HigherEloTeam_IsAboveHalf()
    {
        Assert.True(Elo.GetPrediction(2000f, 1500f) > 0.5f);
    }

    [Fact]
    public void GetPrediction_Symmetry()
    {
        float a = 1800f, b = 1400f;
        Assert.Equal(1.0f, Elo.GetPrediction(a, b) + Elo.GetPrediction(b, a), precision: 4);
    }

    // --- ELO change math ---

    [Fact]
    public void EloChange_EqualTeams_HighConfidence()
    {
        // confidence=2 → factor=0.5; k = 64*(0.5/2 + 0.5/2) = 32; change = 32*(1-0.5) = 16
        const float expectedChange = 16f;

        var (teamA, teamB) = MakeSymmetricTeams(1500f, 1500f, 2, 2);
        RunGame(teamA, teamB, GameResult.TeamAWon, out float[] gainA, out float[] lossB);

        Assert.Equal(expectedChange, gainA[0], precision: 3);
        Assert.Equal(-expectedChange, lossB[0], precision: 3);
    }

    [Fact]
    public void EloChange_EqualTeams_LowConfidence()
    {
        // confidence=0 → factor=1.0; k = 64*(1.0/2 + 1.0/2) = 64; change = 64*(1-0.5) = 32
        const float expectedChange = 32f;

        var (teamA, teamB) = MakeSymmetricTeams(1500f, 1500f, 0, 0);
        RunGame(teamA, teamB, GameResult.TeamAWon, out float[] gainA, out float[] lossB);

        Assert.Equal(expectedChange, gainA[0], precision: 3);
        Assert.Equal(-expectedChange, lossB[0], precision: 3);
    }

    [Fact]
    public void EloChange_HighConfidence_SmallerThanLowConfidence()
    {
        var (teamAHigh, teamBHigh) = MakeSymmetricTeams(1500f, 1500f, 2, 2);
        var (teamALow, teamBLow) = MakeSymmetricTeams(1500f, 1500f, 0, 0);

        RunGame(teamAHigh, teamBHigh, GameResult.TeamAWon, out float[] highGain, out _);
        RunGame(teamALow, teamBLow, GameResult.TeamAWon, out float[] lowGain, out _);

        Assert.True(Math.Abs(highGain[0]) < Math.Abs(lowGain[0]));
    }

    [Fact]
    public void EloChange_FavoriteWins_SmallerGainThanEqualTeams()
    {
        // High ELO team wins: prediction > 0.5, so gain = k*(1-prediction) < k*0.5
        var (equalA, equalB) = MakeSymmetricTeams(1500f, 1500f, 2, 2);
        var teamFav = new[] { MakePlayer(1, "Fav", 2000f, 2) };
        var teamUnder = new[] { MakePlayer(2, "Under", 1500f, 2) };

        RunGame(equalA, equalB, GameResult.TeamAWon, out float[] equalGain, out _);
        RunGame(teamFav, teamUnder, GameResult.TeamAWon, out float[] favGain, out _);

        Assert.True(favGain[0] < equalGain[0]);
    }

    [Fact]
    public void EloChange_UnderdogWins_LargerGainThanEqualTeams()
    {
        // Low ELO team wins: prediction < 0.5, so gain = k*(1-prediction) > k*0.5
        var (equalA, equalB) = MakeSymmetricTeams(1500f, 1500f, 2, 2);
        var teamUnder = new[] { MakePlayer(1, "Under", 1000f, 2) };
        var teamFav = new[] { MakePlayer(2, "Fav", 2000f, 2) };

        RunGame(equalA, equalB, GameResult.TeamAWon, out float[] equalGain, out _);
        RunGame(teamUnder, teamFav, GameResult.TeamAWon, out float[] underdogGain, out _);

        Assert.True(underdogGain[0] > equalGain[0]);
    }

    [Fact]
    public void EloChange_MixedConfidenceTeam_ProportionalDistribution()
    {
        // Team of 2: player1 confidence=0 (factor=1.0), player2 confidence=2 (factor=0.5)
        // avgConf = (1.0+0.5)/2 = 0.75
        // player1 share = 1.0/0.75 ≈ 1.333, player2 share = 0.5/0.75 ≈ 0.667
        // player1 gets ~2x what player2 gets
        var player1 = MakePlayer(1, "Low", 1500f, 0);
        var player2 = MakePlayer(2, "High", 1500f, 2);
        var teamA = new[] { player1, player2 };
        var teamB = new[] { MakePlayer(3, "Opp", 1500f, 2) };

        RunGame(teamA, teamB, GameResult.TeamAWon, out float[] gainA, out _);

        // player1 gain should be ~2x player2 gain (factor ratio 1.0/0.5 = 2)
        Assert.True(gainA[0] > gainA[1]);
        Assert.Equal(1.0f / 0.5f, gainA[0] / gainA[1], precision: 3);
    }

    // --- eloKey correctness ---

    [Fact]
    public void EloKey_OnlyCorrectKeyUpdated()
    {
        var player = MakePlayer(1, "P1", 1500f, 2);
        player.ExperienceComponent.EloValues.UpdateElo(OtherKey, 1800f, 2);
        var opponent = MakePlayer(2, "P2", 1500f, 2);
        opponent.ExperienceComponent.EloValues.UpdateElo(OtherKey, 1800f, 2);

        RunGame([player], [opponent], GameResult.TeamAWon, out _, out _);

        // eloKey value changed
        player.ExperienceComponent.EloValues.GetElo(EloKey, out float newElo, out _);
        Assert.NotEqual(1500f, newElo);

        // the other key unchanged
        player.ExperienceComponent.EloValues.GetElo(OtherKey, out float otherElo, out _);
        Assert.Equal(1800f, otherElo);
    }

    [Fact]
    public void EloKey_CalculationUsesCorrectKey()
    {
        // If we set up eloKey values as equal (1500 vs. 1500) but otherKey as very unequal (2000 vs. 1000),
        // the ELO change should match the equal-ELO prediction (≈32 for confidence=0), not the unequal one.
        var p1 = MakePlayer(1, "P1", 1500f, 0);
        p1.ExperienceComponent.EloValues.UpdateElo(OtherKey, 2000f, 0);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        p2.ExperienceComponent.EloValues.UpdateElo(OtherKey, 1000f, 0);

        RunGame([p1], [p2], GameResult.TeamAWon, out float[] gain, out _);

        // With equal eloKey ELOs, expected change = 64 * 1.0 * (1 - 0.5) = 32
        Assert.Equal(32f, gain[0], precision: 3);
    }

    // --- skip conditions ---

    [Fact]
    public void SkipNonPvP_NoEloChange()
    {
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 2);

        int updaterCallCount = 0;
        RunGameCustom(
            [p1],
            [p2],
            GameResult.TeamAWon,
            gameType: GameType.Ranked,
            updaterCallCount: ref updaterCallCount);

        Assert.Equal(0, updaterCallCount);
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out float elo, out _);
        Assert.Equal(1500f, elo);
    }

    [Fact]
    public void SkipNonDecisiveResult_NoEloChange()
    {
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 2);

        int updaterCallCount = 0;
        RunGameCustom(
            [p1],
            [p2],
            GameResult.TieGame,
            gameType: GameType.PvP,
            updaterCallCount: ref updaterCallCount);

        Assert.Equal(0, updaterCallCount);
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out float elo, out _);
        Assert.Equal(1500f, elo);
    }

    [Fact]
    public void SkipNullSummary_NoException()
    {
        int updaterCallCount = 0;
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 2);
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            null,
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            DateTime.UtcNow,
            _ => null,
            _ => [],
            Updater);

        Assert.Equal(0, updaterCallCount);
        return;
        void Updater(PersistedAccountData _) => updaterCallCount++;
    }

    // --- confidence update logic ---

    [Fact]
    public void Confidence_NoHistory_DropsToZero()
    {
        // Player starts at confidence=2, no match history → delta=-100 → clamped to 0
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 2);

        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            DateTime.UtcNow,
            id => id == p1.AccountId ? p1 : p2,
            _ => [],
            _ => { });

        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(0, cf);
    }

    [Fact]
    public void Confidence_RecentMatches_Upgrades()
    {
        // Player at confidence=0 with 10 recent PvP matches → upgrade threshold met → level becomes 1
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 0);
        var p2 = MakePlayer(2, "P2", 1500f, 0);

        var history = Enumerable.Range(0, 10)
            .Select(_ => MakeMatch(GameType.PvP, now - TimeSpan.FromDays(1)))
            .ToList();

        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });

        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(1, cf);
    }

    [Fact]
    public void Confidence_OldMatch_Decays()
    {
        // Player at confidence=1; last match was 200 days ago (>EloConfidenceRetention[2]=180d)
        // → decay delta=-2 → level becomes max(0, 1-2) = 0
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 1);
        var p2 = MakePlayer(2, "P2", 1500f, 1);

        // verify initial confidence
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int initialCf);
        Assert.Equal(1, initialCf);

        var history = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromDays(200))
        };

        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            new MatchmakingConfiguration(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });

        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(0, cf);
    }

    // --- GetTeamElo ---

    [Fact]
    public void GetTeamElo_SinglePlayer_ReturnsPlayerElo()
    {
        var p = MakeMatchPlayerDataWithChars(MakePlayer(1, "P", 1700f, 0), 1);
        Assert.Equal(1700f, Elo.GetTeamElo([p]), precision: 3);
    }

    [Fact]
    public void GetTeamElo_TwoPlayers_ReturnsArithmeticMean()
    {
        var p1 = MakeMatchPlayerDataWithChars(MakePlayer(1, "A", 1000f, 0), 1);
        var p2 = MakeMatchPlayerDataWithChars(MakePlayer(2, "B", 2000f, 0), 1);
        Assert.Equal(1500f, Elo.GetTeamElo([p1, p2]), precision: 3);
    }

    [Fact]
    public void GetTeamElo_WeightedByNumControlledCharacters()
    {
        // p1 controls 2 chars at 2000, p2 controls 1 char at 1000
        // weighted mean = (2000*2 + 1000*1) / (2+1) = 5000/3
        var p1 = MakeMatchPlayerDataWithChars(MakePlayer(1, "A", 2000f, 0), 2);
        var p2 = MakeMatchPlayerDataWithChars(MakePlayer(2, "B", 1000f, 0), 1);
        Assert.Equal(5000f / 3f, Elo.GetTeamElo([p1, p2]), precision: 3);
    }

    [Fact]
    public void EloChange_ControllerOfMultipleCharacters_GetsScaledDelta()
    {
        // 1v1, equal ELOs, cf=0: k=64, eloChange=32, AwardEloTeam gain = 32*factor*numChars/avgConf
        // numChars=2 → gain = 64, vs. numChars=1 → gain = 32
        var multiCharPlayer = MakePlayer(1, "Multi", 1500f, 0);
        var opponent = MakePlayer(2, "Opp", 1500f, 0);
        var now = DateTime.UtcNow;
        var allPlayers = new[] { multiCharPlayer, opponent }.ToDictionary(p => p.AccountId);
        var stableHistory = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromHours(1))
        };

        multiCharPlayer.ExperienceComponent.EloValues.GetElo(EloKey, out float before, out _);
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [multiCharPlayer.AccountId], [opponent.AccountId]),
            [MakeMatchPlayerDataWithChars(multiCharPlayer, 2), MakeMatchPlayerData(opponent)],
            DefaultConf(),
            now,
            id => allPlayers[id],
            _ => stableHistory,
            _ => { });

        multiCharPlayer.ExperienceComponent.EloValues.GetElo(EloKey, out float after, out _);
        Assert.Equal(64f, after - before, precision: 3);
    }

    // --- InitElo ---

    [Fact]
    public void InitElo_KeyAlreadyExists_NoUpdate()
    {
        var account = MakePlayer(1, "P1", 1500f, 2);
        int updaterCount = 0;
        Elo.InitElo(account.AccountId, EloKey, _ => account, _ => updaterCount++);
        Assert.Equal(0, updaterCount);
        account.ExperienceComponent.EloValues.GetElo(EloKey, out float elo, out _);
        Assert.Equal(1500f, elo);
    }

    [Fact]
    public void InitElo_NoFallbackKey_NoUpdate()
    {
        // "PvP$ranked" absent; fallback "PvP" also absent (account only has lowercase "pvp" key)
        var account = MakePlayer(1, "P1", 1500f, 2);
        int updaterCount = 0;
        Elo.InitElo(account.AccountId, "PvP$ranked", _ => account, _ => updaterCount++);
        Assert.Equal(0, updaterCount);
        Assert.False(account.ExperienceComponent.EloValues.Values.ContainsKey("PvP$ranked"));
    }

    [Fact]
    public void InitElo_FallbackExists_CopiesAndUpdates()
    {
        var account = MakePlayer(1, "P1", 1500f, 0);
        account.ExperienceComponent.EloValues.UpdateElo("PvP", 1800f, 1);
        int updaterCount = 0;
        Elo.InitElo(account.AccountId, "PvP$ranked", _ => account, _ => updaterCount++);
        Assert.Equal(1, updaterCount);
        account.ExperienceComponent.EloValues.GetElo("PvP$ranked", out float elo, out int cf);
        Assert.Equal(1800f, elo);
        Assert.Equal(1, cf);
    }

    [Fact]
    public void InitElo_CopiedDatumIsIndependent()
    {
        // EloDatum.Clone() must produce an independent copy so mutating the fallback doesn't affect the new key
        var account = MakePlayer(1, "P1", 1500f, 0);
        account.ExperienceComponent.EloValues.UpdateElo("PvP", 1800f, 1);
        Elo.InitElo(account.AccountId, "PvP$ranked", _ => account, _ => { });
        account.ExperienceComponent.EloValues.UpdateElo("PvP", 2000f, 0);
        account.ExperienceComponent.EloValues.GetElo("PvP$ranked", out float elo, out int cf);
        Assert.Equal(1800f, elo);
        Assert.Equal(1, cf);
    }

    // --- GetEloKey format ---

    [Fact]
    public void GetEloKey_Format()
    {
        var subType = new GameSubType { LocalizedName = "5v5" };
        Assert.Equal("PvP$5v5", Elo.GetEloKey(GameType.PvP, subType));
    }

    // --- additional confidence update logic ---

    [Fact]
    public void Confidence_MatchAt60Days_NoDecay()
    {
        // 30d < 60d < 90d: loop hits i=0 → delta = -0 = 0; upgrade skipped (cf=2 is max)
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromDays(60))
        };
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(2, cf);
    }

    [Fact]
    public void Confidence_MatchAt120Days_DecaysByOne()
    {
        // 90d < 120d < 180d: loop hits i=1 → delta = -1; 2-1 = 1
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromDays(120))
        };
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(1, cf);
    }

    [Fact]
    public void Confidence_AlreadyAtMaxLevel_NoUpgrade()
    {
        // cf=2 equals EloConfidenceUpgrade.Count=2, so the upgrade branch is never entered
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = Enumerable.Range(0, 30)
            .Select(_ => MakeMatch(GameType.PvP, now - TimeSpan.FromHours(12)))
            .ToList();
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(2, cf);
    }

    [Fact]
    public void Confidence_UpgradeFromLevelOne_ToLevelTwo()
    {
        // cf=1, 25 recent PvP matches → EloConfidenceUpgrade[1]=25 threshold met → cf becomes 2
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 1);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = Enumerable.Range(0, 25)
            .Select(_ => MakeMatch(GameType.PvP, now - TimeSpan.FromHours(12)))
            .ToList();
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(2, cf);
    }

    [Fact]
    public void Confidence_MatchOfDifferentSubType_TreatedAsNoHistory()
    {
        // No match with the correct subtype → lastMatch=null → delta=-100 → cf drops to 0
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = Enumerable.Range(0, 30)
            .Select(_ => new PersistedCharacterMatchData
            {
                MatchComponent = new MatchComponent
                {
                    GameType = GameType.PvP,
                    MatchTime = now - TimeSpan.FromHours(1),
                    SubTypeLocTag = "wrongSubType"
                }
            })
            .ToList();
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(0, cf);
    }

    [Fact]
    public void Confidence_UpgradeCountIncludesAllGameTypeMatches()
    {
        // 1 correct-subtype match + 9 other-subtype PvP matches = 10 total PvP matches.
        // The upgrade count query filters only by GameType, not SubTypeLocTag, so all 10 count.
        // 10 >= EloConfidenceUpgrade[0]=10 → upgrade fires despite only 1 correct-subtype match.
        var now = DateTime.UtcNow;
        var p1 = MakePlayer(1, "P1", 1500f, 0);
        var p2 = MakePlayer(2, "P2", 1500f, 0);
        var history = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromHours(1))
        };
        history.AddRange(Enumerable.Range(0, 9).Select(_ => new PersistedCharacterMatchData
        {
            MatchComponent = new MatchComponent
            {
                GameType = GameType.PvP,
                MatchTime = now - TimeSpan.FromHours(1),
                SubTypeLocTag = "otherSubType"
            }
        }));
        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(GameResult.TeamAWon, [p1.AccountId], [p2.AccountId]),
            [MakeMatchPlayerData(p1), MakeMatchPlayerData(p2)],
            DefaultConf(),
            now,
            id => id == p1.AccountId ? p1 : p2,
            _ => history,
            _ => { });
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out _, out int cf);
        Assert.Equal(1, cf);
    }

    // --- ELO conservation ---

    [Fact]
    public void EloChange_TotalEloConserved()
    {
        // For equal-size teams, the total ELO gain across all players is zero
        var (teamA, teamB) = MakeSymmetricTeams(1800f, 1400f, 1, 2);
        RunGame(teamA, teamB, GameResult.TeamBWon, out float[] gainA, out float[] gainB);
        Assert.Equal(0f, gainA.Sum() + gainB.Sum(), precision: 3);
    }

    // --- fixture helpers ---

    private static PersistedAccountData MakePlayer(long id, string name, float elo, int confidence)
        => TestAccountHelper.MakeAccount(id, name, elo, confidence, EloKey);

    private static MatchPlayerData MakeMatchPlayerData(PersistedAccountData player) =>
        new(player.AccountId, player.Handle, EloKey, player.ExperienceComponent.EloValues,
            [CharacterType.PendingWillFill], []);

    private static MatchPlayerData MakeMatchPlayerDataWithChars(PersistedAccountData player, int numChars) =>
        new(player.AccountId, player.Handle, EloKey, player.ExperienceComponent.EloValues,
            Enumerable.Repeat(CharacterType.PendingWillFill, numChars).ToList(), []);

    private static (PersistedAccountData[], PersistedAccountData[]) MakeSymmetricTeams(
        float eloA, float eloB, int confidenceA, int confidenceB)
    {
        return (
            [MakePlayer(1, "A", eloA, confidenceA)],
            [MakePlayer(2, "B", eloB, confidenceB)]
        );
    }

    private static void RunGame(
        PersistedAccountData[] teamA,
        PersistedAccountData[] teamB,
        GameResult result,
        out float[] gainA,
        out float[] gainB)
    {
        var now = DateTime.UtcNow;
        var allPlayers = teamA.Concat(teamB).ToDictionary(p => p.AccountId);
        var beforeA = teamA.Select(p => { p.ExperienceComponent.EloValues.GetElo(EloKey, out float e, out _); return e; }).ToArray();
        var beforeB = teamB.Select(p => { p.ExperienceComponent.EloValues.GetElo(EloKey, out float e, out _); return e; }).ToArray();

        // Provide one recent match per player so confidence doesn't change (upgrade threshold not met)
        var stableHistory = new List<PersistedCharacterMatchData>
        {
            MakeMatch(GameType.PvP, now - TimeSpan.FromHours(1))
        };

        Elo.OnGameEnded(
            MakeGameInfo(GameType.PvP),
            MakeSummary(result, teamA.Select(p => p.AccountId).ToArray(), teamB.Select(p => p.AccountId).ToArray()),
            teamA.Concat(teamB).Select(MakeMatchPlayerData).ToList(),
            DefaultConf(),
            now,
            id => allPlayers[id],
            _ => stableHistory,
            _ => { });

        gainA = teamA.Select((p, i) =>
        {
            p.ExperienceComponent.EloValues.GetElo(EloKey, out float e, out _);
            return e - beforeA[i];
        }).ToArray();

        gainB = teamB.Select((p, i) =>
        {
            p.ExperienceComponent.EloValues.GetElo(EloKey, out float e, out _);
            return e - beforeB[i];
        }).ToArray();
    }

    private void RunGameCustom(
        PersistedAccountData[] teamA,
        PersistedAccountData[] teamB,
        GameResult result,
        GameType gameType,
        ref int updaterCallCount)
    {
        var allPlayers = teamA.Concat(teamB).ToDictionary(p => p.AccountId);
        int count = 0;

        Elo.OnGameEnded(
            MakeGameInfo(gameType),
            MakeSummary(result, teamA.Select(p => p.AccountId).ToArray(), teamB.Select(p => p.AccountId).ToArray()),
            teamA.Concat(teamB).Select(MakeMatchPlayerData).ToList(),
            DefaultConf(),
            DateTime.UtcNow,
            id => allPlayers[id],
            _ => [],
            Updater);

        updaterCallCount = count;
        return;

        void Updater(PersistedAccountData _) => count++;
    }

    private static LobbyGameInfo MakeGameInfo(GameType gameType) => new()
    {
        GameServerProcessCode = "test",
        GameConfig = new LobbyGameConfig
        {
            GameType = gameType,
            SubTypes = [ new GameSubType { LocalizedName = SubType}],
            InstanceSubTypeBit = 1,
        }
    };

    private static LobbyGameSummary MakeSummary(GameResult result, long[] teamAIds, long[] teamBIds)
    {
        var summary = new LobbyGameSummary { GameResult = result };
        foreach (long id in teamAIds)
        {
            summary.PlayerGameSummaryList.Add(new PlayerGameSummary { AccountId = id, Team = 0 });
        }
        foreach (long id in teamBIds)
        {
            summary.PlayerGameSummaryList.Add(new PlayerGameSummary { AccountId = id, Team = 1 });
        }
        return summary;
    }

    private static GameSubType MakePvPSubType(bool fourlancer = false) => new()
    {
        LocalizedName = "Test",
        Mods = fourlancer
            ? [GameSubType.SubTypeMods.ControlAllBots]
            : []
    };

    private static MatchmakingConfiguration DefaultConf() => new()
    {
        EloBasePot = 64f,
        EloConfidenceFactor = [1.0f, 0.75f, 0.5f],
        EloConfidenceRetention =
        [
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
            TimeSpan.FromDays(180)
        ],
        EloConfidenceUpgrade = [10, 25]
    };

    private static PersistedCharacterMatchData MakeMatch(GameType gameType, DateTime time) => new()
    {
        MatchComponent = new MatchComponent { GameType = gameType, MatchTime = time, SubTypeLocTag = SubType }
    };
}
