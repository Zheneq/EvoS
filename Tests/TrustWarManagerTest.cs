using CentralServer.BridgeServer;
using CentralServer.LobbyServer.TrustWar;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="TrustWarManager.CalculateTrustWar"/>.
/// Joined to [Collection("ClientNotifierSeam")] because faction contributions are sent
/// via the global <see cref="CentralServer.LobbyServer.Session.IClientNotifier"/>.
/// </summary>
[Collection("ClientNotifierSeam")]
public class TrustWarManagerTest(ITestOutputHelper output) : EvosTest(output)
{
    private const int RibbonId = 1;
    private const int FactionId = 1;

    private sealed class TestGame : PvpGame
    {
        public TestGame() : base(null)
        {
            GameInfo = new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig { GameType = GameType.PvP },
                GameServerProcessCode = $"test-{Guid.NewGuid()}",
            };
            TeamInfo = new LobbyServerTeamInfo { TeamPlayerInfo = [] };
        }

        public LobbyServerPlayerInfo AddPlayer(long accountId, Team team)
        {
            LobbyServerPlayerInfo player = new() { AccountId = accountId, TeamId = team };
            TeamInfo.TeamPlayerInfo.Add(player);
            return player;
        }

        // Mirrors Game.AddBot: characters a player controls besides their own (ControlAllBots, asymmetric games)
        public void AddProxies(LobbyServerPlayerInfo controllingPlayer, int count)
        {
            for (int i = 0; i < count; i++)
            {
                TeamInfo.TeamPlayerInfo.Add(new LobbyServerPlayerInfo
                {
                    AccountId = controllingPlayer.AccountId,
                    TeamId = controllingPlayer.TeamId,
                    ControllingPlayerInfo = controllingPlayer,
                });
            }
        }

        public void AddNpcBot(Team team) =>
            TeamInfo.TeamPlayerInfo.Add(new LobbyServerPlayerInfo { IsNPCBot = true, TeamId = team });
    }

    private static long MakeAccount()
    {
        long accountId = (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 14_000_000L;
        PersistedAccountData account = new()
        {
            AccountId = accountId,
            Handle = $"TestTrustWar#{accountId}",
            UserName = $"TestTrustWar{accountId}",
            AccountComponent = new AccountComponent
            {
                SelectedRibbonID = RibbonId,
                FactionCompetitionData =
                {
                    [0] = new PlayerFactionCompetitionData
                    {
                        CompetitionID = 1,
                        Factions = new Dictionary<int, FactionPlayerData>
                        {
                            { 0, new FactionPlayerData { FactionID = 1 } },
                            { 1, new FactionPlayerData { FactionID = 2 } },
                            { 2, new FactionPlayerData { FactionID = 3 } },
                        }
                    }
                }
            },
        };
        DB.Get().AccountDao.CreateAccount(account);
        return accountId;
    }

    private static int FactionXp(long accountId)
    {
        return TrustWarManager.GetTotalXPByFactionID(DB.Get().AccountDao.GetAccount(accountId), FactionId);
    }

    [Fact]
    public void OnlyHumanPlayersEarnPoints_OncePerPlayer()
    {
        using ClientNotifierScope scope = new();
        long winner = MakeAccount();
        long loser = MakeAccount();
        long spectator = MakeAccount();
        TestGame game = new();
        game.AddProxies(game.AddPlayer(winner, Team.TeamA), 2);
        game.AddPlayer(loser, Team.TeamB);
        game.AddNpcBot(Team.TeamB);
        game.AddPlayer(spectator, Team.Spectator);

        TrustWarManager.CalculateTrustWar(game, new LobbyGameSummary { GameResult = GameResult.TeamAWon });

        Assert.Equal(LobbyConfiguration.GetTrustWarGameWonPoints(), FactionXp(winner));
        Assert.Equal(LobbyConfiguration.GetTrustWarGamePlayedPoints(), FactionXp(loser));
        Assert.Equal(0, FactionXp(spectator));
        Assert.Equal(
            [winner, loser],
            scope.Notifier.Sent
                .Where(s => s.Message is PlayerFactionContributionChangeNotification)
                .Select(s => s.AccountId));
    }
}
