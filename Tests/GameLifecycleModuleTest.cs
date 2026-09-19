using System;
using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="GameLifecycleModule"/> state behaviour and handlers.
/// Joined to [Collection("ClientNotifierSeam")] because GroupManager state is
/// process-global; serializing avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class GameLifecycleModuleTest : EvosTest
{
    public GameLifecycleModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds (> 7_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 7_000_000L;

    /// <summary>
    /// Minimal IHandlerRegistry that lets tests dispatch messages directly into a module.
    /// </summary>
    private sealed class CapturingRegistry : IHandlerRegistry
    {
        private readonly Dictionary<Type, Action<WebSocketMessage>> _handlers = new();

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            _handlers[typeof(T)] = msg => handler((T)msg);
        }

        public void Dispatch<T>(T message) where T : WebSocketMessage
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                handler(message);
            else
                throw new InvalidOperationException($"No handler registered for {typeof(T).Name}");
        }
    }

    private static (GameLifecycleModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModuleWithRegistry(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn, GameManager.Instance);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static (GameLifecycleModule module, RecordingClientConnection conn)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn, GameManager.Instance);
        return (module, conn);
    }

    // Case 1: Fresh module: CurrentGame null, IsInGame() false, PlayerInfo null.
    [Fact]
    public void FreshModule_CurrentGameNull_IsInGameFalse_PlayerInfoNull()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        Assert.Null(module.CurrentGame);
        Assert.False(module.IsInGame());
        Assert.Null(module.PlayerInfo);
    }

    // Case 2: LeaveGame(null) returns false (null-server log path), no sends.
    [Fact]
    public void LeaveGame_NullGame_ReturnsFalse_NoSends()
    {
        long accountId = UniqueId();
        var (module, conn) = MakeModule(accountId);

        bool result = module.LeaveGame(null);

        Assert.False(result);
        Assert.Empty(conn.Sent);
    }

    // Case 3: LeaveGame(someGame) when not in a game — can't construct a Game easily
    // (abstract, heavy deps); dropping this case per plan's drop-and-note clause.

    // Case 4: BalancedTeamRequest with no game: one BalancedTeamResponse, Success = false.
    [Fact]
    public void BalancedTeamRequest_NoGame_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new BalancedTeamRequest
        {
            RequestId = 4,
            Slots = new System.Collections.Generic.List<BalanceTeamSlot>()
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<BalancedTeamResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 5: PreviousGameInfoRequest with no game: one response, PreviousGameInfo == null.
    [Fact]
    public void PreviousGameInfoRequest_NoGame_NullPreviousGameInfo()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new PreviousGameInfoRequest { RequestId = 5 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PreviousGameInfoResponse>(conn.Sent[0]);
        Assert.Null(response.PreviousGameInfo);
    }

    // Case 6: LeaveGameRequest with no current game: LeaveGameResponse (Success = true) +
    // GameStatusNotification (Stopped) + one SendGameUnassignmentNotification call — in that order.
    [Fact]
    public void LeaveGameRequest_NoCurrentGame_SendsThreeResponsesInOrder()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new LeaveGameRequest { RequestId = 6 });

        Assert.Equal(2, conn.Sent.Count);
        var leaveResponse = Assert.IsType<LeaveGameResponse>(conn.Sent[0]);
        Assert.True(leaveResponse.Success);
        var statusNotification = Assert.IsType<GameStatusNotification>(conn.Sent[1]);
        Assert.Equal(GameStatus.Stopped, statusNotification.GameStatus);
        Assert.Equal(1, conn.GameUnassignmentCalls);
    }

    // Case 7: JoinGameRequest for an unknown process code: failed JoinGameResponse with the
    // failure payload; ResetReadyStateCalls == 1.
    [Fact]
    public void JoinGameRequest_UnknownProcessCode_FailedResponseAndResetReadyState()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new JoinGameRequest
        {
            RequestId = 7,
            GameServerProcessCode = "no-such-game-xyz",
            AsSpectator = false
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<JoinGameResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        Assert.NotNull(response.LocalizedFailure);
        Assert.Equal(1, conn.ResetReadyStateCalls);
    }

    // Case 8a: GameInvitationRequest sends exactly one failed response.
    [Fact]
    public void GameInvitationRequest_SendsOneFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new GameInvitationRequest
        {
            RequestId = 8,
            InviteeHandle = "someone"
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<GameInvitationResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 8b: RankedLeaderboardOverviewRequest sends exactly one failed response.
    [Fact]
    public void RankedLeaderboardOverviewRequest_SendsOneFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new RankedLeaderboardOverviewRequest { RequestId = 9 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<RankedLeaderboardOverviewResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 8c: CalculateFreelancerStatsRequest sends exactly one failed response.
    [Fact]
    public void CalculateFreelancerStatsRequest_SendsOneFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new CalculateFreelancerStatsRequest { RequestId = 10 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<CalculateFreelancerStatsResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 8d: GameInviteConfirmationResponse no-throw.
    [Fact]
    public void GameInviteConfirmationResponse_NoThrow()
    {
        long accountId = UniqueId();
        var (_, _, registry) = MakeModuleWithRegistry(accountId);

        var ex = Record.Exception(() => registry.Dispatch(new GameInviteConfirmationResponse()));

        Assert.Null(ex);
    }

    // Case 8e: PlayerPanelUpdatedNotification no-throw.
    [Fact]
    public void PlayerPanelUpdatedNotification_NoThrow()
    {
        long accountId = UniqueId();
        var (_, _, registry) = MakeModuleWithRegistry(accountId);

        var ex = Record.Exception(() => registry.Dispatch(new PlayerPanelUpdatedNotification()));

        Assert.Null(ex);
    }
}
