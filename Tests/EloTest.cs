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
            fourlancer: false,
            updaterCallCount: ref updaterCallCount);

        Assert.Equal(0, updaterCallCount);
        p1.ExperienceComponent.EloValues.GetElo(EloKey, out float elo, out _);
        Assert.Equal(1500f, elo);
    }

    [Fact]
    public void SkipFourlancer_NoEloChange()
    {
        var p1 = MakePlayer(1, "P1", 1500f, 2);
        var p2 = MakePlayer(2, "P2", 1500f, 2);

        int updaterCallCount = 0;
        RunGameCustom(
            [p1],
            [p2],
            GameResult.TeamAWon,
            gameType: GameType.PvP,
            fourlancer: true,
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
            fourlancer: false,
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

    // --- fixture helpers ---

    private static PersistedAccountData MakePlayer(long id, string name, float elo, int confidence)
        => TestAccountHelper.MakeAccount(id, name, elo, confidence, EloKey);

    private static MatchPlayerData MakeMatchPlayerData(PersistedAccountData player) =>
        new(player.AccountId, player.Handle, EloKey, player.ExperienceComponent.EloValues,
            [CharacterType.PendingWillFill], []);

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
        bool fourlancer,
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
        GameConfig = new LobbyGameConfig { GameType = gameType }
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
        MatchComponent = new MatchComponent { GameType = gameType, MatchTime = time }
    };
}
