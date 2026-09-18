using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="MatchmakingModule"/> state behaviour and handlers.
/// Joined to [Collection("ClientNotifierSeam")] because GroupManager state is
/// process-global and other collection members also mutate it;
/// serializing them avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class MatchmakingModuleTest : EvosTest
{
    public MatchmakingModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds (> 5_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 5_000_000L;

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

    private static (MatchmakingModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModuleWithRegistry(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new MatchmakingModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static (MatchmakingModule module, RecordingClientConnection conn)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new MatchmakingModule(conn);
        return (module, conn);
    }

    // Case 1: GetSubTypeMask() returns 1 when mask is 0; returns the mask when set.
    [Fact]
    public void GetSubTypeMask_WhenZero_ReturnsOne()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        Assert.Equal(1, module.GetSubTypeMask());
    }

    [Fact]
    public void GetSubTypeMask_WhenSet_ReturnsMask()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        module.SelectedSubTypeMask = 3;

        Assert.Equal(3, module.GetSubTypeMask());
    }

    // Case 2: SetContextualReadyState(Ready) for an account with no group and no penalties:
    // IsReady becomes true (the no-group branch logs and returns after setting it).
    [Fact]
    public void SetContextualReadyState_Ready_NoGroup_IsReadyBecomesTrue()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);
        // Account is not registered in SessionManager/GroupManager → GetPlayerGroup returns null.

        module.SetContextualReadyState(new ContextualReadyState
        {
            ReadyState = ReadyState.Ready,
            GameProcessCode = string.Empty
        });

        Assert.True(module.IsReady);
    }

    // Case 3: ResetReadyState() after being ready: IsReady is false again.
    [Fact]
    public void ResetReadyState_AfterReady_IsReadyFalse()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        module.SetContextualReadyState(new ContextualReadyState
        {
            ReadyState = ReadyState.Ready,
            GameProcessCode = string.Empty
        });
        Assert.True(module.IsReady); // precondition

        module.ResetReadyState();

        Assert.False(module.IsReady);
    }

    // Case 4: Unready() sets IsReady false without touching anything else
    // (fresh module, no group — must not throw).
    [Fact]
    public void Unready_FreshModule_DoesNotThrow()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        var ex = Record.Exception(() => module.Unready());

        Assert.Null(ex);
        Assert.False(module.IsReady);
    }

    // Case 5: JoinMatchmakingQueueRequest for an account with no group → caught by try/catch,
    // exactly one JoinMatchmakingQueueResponse with Success = false and ServerError@Global.
    [Fact]
    public void JoinMatchmakingQueue_NoGroup_ReturnsCatchPathFailure()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);
        // Account not in GroupManager → GetPlayerGroup returns null → NullReferenceException in handler → caught

        registry.Dispatch(new JoinMatchmakingQueueRequest
        {
            GameType = GameType.PvP,
            RequestId = 5
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<JoinMatchmakingQueueResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        Assert.NotNull(response.LocalizedFailure);
        Assert.Equal("ServerError", response.LocalizedFailure.Term);
        Assert.Equal("Global", response.LocalizedFailure.Context);
    }

    // Case 6: LeaveMatchmakingQueueRequest for an account with no group → Success = false
    // (same catch path).
    [Fact]
    public void LeaveMatchmakingQueue_NoGroup_ReturnsCatchPathFailure()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new LeaveMatchmakingQueueRequest { RequestId = 6 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<LeaveMatchmakingQueueResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 7: SetGameSubTypeRequest → SelectedSubTypeMask updated, one SetGameSubTypeResponse sent.
    // Account is in a solo group so UpdateSelectedSubTypesForAccount takes its logged no-op path (offline leader).
    [Fact]
    public void SetGameSubType_UpdatesMaskAndSendsResponse()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new SetGameSubTypeRequest
        {
            SubTypeMask = 5,
            RequestId = 7
        });

        Assert.Equal(5, module.SelectedSubTypeMask);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SetGameSubTypeResponse>(conn.Sent[0]);
        Assert.Equal(7, response.ResponseId);
    }
}
