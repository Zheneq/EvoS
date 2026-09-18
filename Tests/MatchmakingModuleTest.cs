using System;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="MatchmakingModule"/> state behaviour.
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
}
