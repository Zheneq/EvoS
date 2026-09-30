using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for the rules deciding whether a leaver is penalized (<see cref="QueuePenaltyManager.IssueQueuePenalties"/>).
/// Joined to [Collection("ClientNotifierSeam")] because issuing a penalty looks up the player's group
/// via the global <see cref="CentralServer.LobbyServer.Session.IClientNotifier"/>.
/// </summary>
[Collection("ClientNotifierSeam")]
public class QueuePenaltyIssueTest : EvosTest
{
    private const int PlayerCount = 8;

    public QueuePenaltyIssueTest(ITestOutputHelper output) : base(output)
    {
    }

    private sealed class TestGame : Game
    {
        public TestGame(bool draft, GameStatus status, IEnumerable<long> accountIds)
        {
            GameSubType = new GameSubType
            {
                Mods = draft ? [GameSubType.SubTypeMods.RankedFreelancerSelection] : []
            };
            GameInfo = new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig { GameType = GameType.PvP },
                GameStatus = status,
                GameResult = GameResult.NoResult,
            };
            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo = accountIds.Select(id => new LobbyServerPlayerInfo { AccountId = id }).ToList()
            };
        }

        public LobbyServerPlayerInfo Player(int index) => TeamInfo.TeamPlayerInfo[index];
    }

    private static long[] MakeAccounts(int count)
    {
        long[] accountIds = new long[count];
        for (int i = 0; i < count; i++)
        {
            long accountId = (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 13_000_000L;
            DB.Get().AccountDao.CreateAccount(new PersistedAccountData
            {
                AccountId = accountId,
                Handle = $"TestLeaver#{accountId}",
                UserName = $"TestLeaver{accountId}",
                AdminComponent = new AdminComponent(),
            });
            accountIds[i] = accountId;
        }
        return accountIds;
    }

    private static int OffenseCount(long accountId)
    {
        return DB.Get().AccountDao.GetAccount(accountId).AdminComponent.ActiveQueuePenalties
            ?.GetValueOrDefault(GameType.PvP)
            ?.QueueDodgeCount ?? 0;
    }

    // Mirrors Game.OnPlayerDisconnect: the leaver is replaced with a bot before penalties are issued.
    private static void Leave(TestGame game, int index)
    {
        game.Player(index).ReplacedWithBots = true;
        QueuePenaltyManager.IssueQueuePenalties(game.Player(index).AccountId, game);
    }

    // --- Collapse: once enough players have left, further leavers are not penalized ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Draft_LeaverPenalizedUntilGameCollapses(int alreadyReplaced)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft: true, GameStatus.Started, MakeAccounts(PlayerCount));
        for (int i = 0; i < alreadyReplaced; i++)
        {
            game.Player(i).ReplacedWithBots = true;
        }

        Leave(game, alreadyReplaced);

        bool expectPenalty = alreadyReplaced < LobbyConfiguration.GetQueuePenaltyCollapseThreshold();
        Assert.Equal(expectPenalty ? 1 : 0, OffenseCount(game.Player(alreadyReplaced).AccountId));
    }
}
