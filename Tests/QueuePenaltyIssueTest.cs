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
        // Statuses from Launched on come from the game server, earlier ones are set by the lobby.
        public TestGame(bool draft, GameStatus status, IEnumerable<long> accountIds)
        {
            GameSubType = new GameSubType
            {
                Mods = draft ? [GameSubType.SubTypeMods.RankedFreelancerSelection] : []
            };
            GameInfo = new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig { GameType = GameType.PvP },
                GameServerProcessCode = $"test-{Guid.NewGuid()}",
                GameStatus = status,
                GameResult = GameResult.NoResult,
            };
            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo = accountIds.Select(id => new LobbyServerPlayerInfo { AccountId = id }).ToList()
            };
            if (status >= GameStatus.Launched)
            {
                OnStatusUpdate(null, status);
            }
        }

        public LobbyServerPlayerInfo Player(int index) => TeamInfo.TeamPlayerInfo[index];

        // Mirrors PvpGame.StartGameAsync, which sets Started itself right after launching the game.
        public void SetLobbyStatus(GameStatus status) => GameInfo.GameStatus = status;

        // Mirrors a player disconnecting from the lobby before the game is launched (AFK in draft, left character select).
        public void CancelBecauseOf(int index) => CancelMatch(Player(index).Handle, Player(index).AccountId);
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

    private static QueuePenalties? Penalties(long accountId)
    {
        return DB.Get().AccountDao
            .GetAccount(accountId)
            .AdminComponent
            .ActiveQueuePenalties
            ?.GetValueOrDefault(GameType.PvP);
    }

    private static int OffenseCount(long accountId)
    {
        return Penalties(accountId)?.QueueDodgeCount ?? 0;
    }

    // Offenses from earlier games, still on parole
    private static void SetPriorOffenses(long accountId, int count)
    {
        DB.Get().AccountDao.GetAccount(accountId).AdminComponent.ActiveQueuePenalties = new()
        {
            [GameType.PvP] = new QueuePenalties
            {
                QueueDodgeCount = count,
                QueueDodgeParoleTimeout = DateTime.UtcNow.AddDays(1),
            }
        };
    }

    // Mirrors Game.OnPlayerDisconnect: the leaver is replaced with a bot before penalties are issued.
    private static void Leave(TestGame game, int index)
    {
        game.Player(index).ReplacedWithBots = true;
        QueuePenaltyManager.IssueQueuePenalties(game.Player(index).AccountId, game);
    }

    // --- When leaving is penalized ---

    [Theory]
    [InlineData(true, GameStatus.FreelancerSelecting, true)]    // drafting
    [InlineData(true, GameStatus.LoadoutSelecting, true)]
    [InlineData(true, GameStatus.Launching, true)]
    [InlineData(true, GameStatus.Connecting, false)]            // loading
    [InlineData(true, GameStatus.Loading, false)]
    [InlineData(true, GameStatus.Loaded, false)]
    [InlineData(true, GameStatus.Started, true)]                // match started
    [InlineData(false, GameStatus.FreelancerSelecting, false)]  // character select
    [InlineData(false, GameStatus.Connecting, false)]           // loading
    [InlineData(false, GameStatus.Loading, false)]
    [InlineData(false, GameStatus.Loaded, false)]
    [InlineData(false, GameStatus.Started, true)]               // match started
    public void Leaver_PenalizedOnlyInDraftOrStartedMatch(bool draft, GameStatus status, bool expectPenalty)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft, status, MakeAccounts(PlayerCount));

        Leave(game, 0);

        Assert.Equal(expectPenalty ? 1 : 0, OffenseCount(game.Player(0).AccountId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Leaver_NotPenalizedWhenOnlyLobbyConsidersGameStarted(bool draft)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft, GameStatus.Connecting, MakeAccounts(PlayerCount));
        game.SetLobbyStatus(GameStatus.Started);

        Leave(game, 0);

        Assert.Equal(0, OffenseCount(game.Player(0).AccountId));
    }

    [Theory]
    [InlineData(true, GameStatus.FreelancerSelecting, true)]
    [InlineData(true, GameStatus.LoadoutSelecting, true)]
    [InlineData(true, GameStatus.Launching, true)]
    [InlineData(false, GameStatus.FreelancerSelecting, false)]
    [InlineData(false, GameStatus.LoadoutSelecting, false)]
    [InlineData(false, GameStatus.Launching, false)]
    public void Dodger_PenalizedForCancellingDraftOnly(bool draft, GameStatus status, bool expectPenalty)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft, status, MakeAccounts(PlayerCount));

        game.CancelBecauseOf(0);

        Assert.Equal(expectPenalty ? 1 : 0, OffenseCount(game.Player(0).AccountId));
    }

    // --- Leaving a running game ---

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Leaver_BlockedForModeBaseDuration(bool draft)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft, GameStatus.Started, MakeAccounts(PlayerCount));
        TimeSpan baseDuration = draft
            ? LobbyConfiguration.GetQueuePenaltyDraftBaseDuration()
            : LobbyConfiguration.GetQueuePenaltyPvPBaseDuration();
        DateTime before = DateTime.UtcNow;

        Leave(game, 0);

        DateTime blockTimeout = Penalties(game.Player(0).AccountId).QueueDodgeBlockTimeout;
        Assert.InRange(blockTimeout, before.Add(baseDuration), DateTime.UtcNow.Add(baseDuration));
    }

    // --- Collapse: once enough players have left, further leavers are not penalized ---

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    public void LeaverPenalizedUntilGameCollapses(bool draft, int alreadyReplaced)
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft, GameStatus.Started, MakeAccounts(PlayerCount));
        for (int i = 0; i < alreadyReplaced; i++)
        {
            game.Player(i).ReplacedWithBots = true;
        }

        Leave(game, alreadyReplaced);

        bool expectPenalty = alreadyReplaced < LobbyConfiguration.GetQueuePenaltyCollapseThreshold();
        Assert.Equal(expectPenalty ? 1 : 0, OffenseCount(game.Player(alreadyReplaced).AccountId));
    }

    // --- Pardons ---

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pardon_ForgivesLeaverAndCutsBlock(bool draft)
    {
        using ClientNotifierScope scope = new();
        TestGame game = new(draft, GameStatus.Started, MakeAccounts(PlayerCount));
        long leaver = game.Player(0).AccountId;
        Leave(game, 0);

        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: false);

        Assert.Equal(0, OffenseCount(leaver));
        Assert.True(Penalties(leaver)!.QueueDodgeBlockTimeout <= DateTime.UtcNow.AddSeconds(15));
        Assert.Contains(scope.Notifier.SystemMessages, m => m.AccountId == leaver);
    }

    [Fact]
    public void Pardon_ForgivesLeaverWhoseBlockRanOut()
    {
        using ClientNotifierScope scope = new();
        TestGame game = new(draft: false, GameStatus.Started, MakeAccounts(PlayerCount));
        long leaver = game.Player(0).AccountId;
        Leave(game, 0);
        Penalties(leaver)!.QueueDodgeBlockTimeout = DateTime.UtcNow.AddSeconds(-1);

        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: false);

        Assert.Equal(0, OffenseCount(leaver));
        Assert.DoesNotContain(scope.Notifier.SystemMessages, m => m.AccountId == leaver); // no block left to cut
    }

    [Fact]
    public void Pardon_OnlyForgivesPenaltiesFromThisGame()
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft: false, GameStatus.Started, MakeAccounts(PlayerCount));
        long stayer = game.Player(1).AccountId;
        SetPriorOffenses(stayer, 2);
        Leave(game, 0);

        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: false);

        Assert.Equal(2, OffenseCount(stayer));
    }

    [Fact]
    public void Pardon_ForgivesEachPenaltyOnce()
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft: false, GameStatus.Started, MakeAccounts(PlayerCount));
        long leaver = game.Player(0).AccountId;
        SetPriorOffenses(leaver, 1);
        Leave(game, 0);

        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: false);
        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: false);

        Assert.Equal(1, OffenseCount(leaver));
    }

    [Fact]
    public void Pardon_PresentPlayersOnly_SkipsLeaversWhoDidNotReturn()
    {
        using ClientNotifierScope _ = new();
        TestGame game = new(draft: false, GameStatus.Started, MakeAccounts(PlayerCount));
        Leave(game, 0);
        Leave(game, 1);
        game.Player(1).ReplacedWithBots = false; // reconnected (Game.ReconnectPlayer)

        QueuePenaltyManager.PardonQueuePenalties(game, presentPlayersOnly: true);

        Assert.Equal(1, OffenseCount(game.Player(0).AccountId));
        Assert.Equal(0, OffenseCount(game.Player(1).AccountId));
    }
}
