using System;
using CentralServer.LobbyServer.GameLifecycle;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="GameLifecycleModule"/> state behaviour.
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

    // Case 1: Fresh module: CurrentGame null, IsInGame() false, PlayerInfo null.
    [Fact]
    public void FreshModule_CurrentGameNull_IsInGameFalse_PlayerInfoNull()
    {
        long accountId = UniqueId();
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn);

        Assert.Null(module.CurrentGame);
        Assert.False(module.IsInGame());
        Assert.Null(module.PlayerInfo);
    }

    // Case 2: LeaveGame(null) returns false (null-server log path), no sends.
    [Fact]
    public void LeaveGame_NullGame_ReturnsFalse_NoSends()
    {
        long accountId = UniqueId();
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn);

        bool result = module.LeaveGame(null);

        Assert.False(result);
        Assert.Empty(conn.Sent);
    }

    // Case 3: LeaveGame(someGame) when not in a game — can't construct a Game easily
    // (abstract, heavy deps); dropping this case per plan's drop-and-note clause.
}
