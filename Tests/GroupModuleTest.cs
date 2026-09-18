using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="GroupModule"/> handlers.
/// Joined to [Collection("ClientNotifierSeam")] because <see cref="GroupManager"/> state is
/// process-global and <see cref="GroupManagerNotifierTest"/> also mutates it;
/// serializing them avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class GroupModuleTest : EvosTest
{
    public GroupModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds to avoid cross-test interference.
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 2_000_000L;

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

    private static (GroupModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GroupModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    // Helper: assert that a LocalizationPayload has the expected Term and Context.
    // LocalizationPayload does not override Equals, so we compare fields directly.
    private static void AssertLocalization(LocalizationPayload payload, string expectedTerm, string expectedContext)
    {
        Assert.NotNull(payload);
        Assert.Equal(expectedTerm, payload.Term);
        Assert.Equal(expectedContext, payload.Context);
    }

    // Case 1: GroupPromoteRequest while solo → Success = false, NotInGroupMember
    [Fact]
    public void GroupPromote_WhileSolo_ReturnsNotInGroupMember()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new GroupPromoteRequest
        {
            Name = "SomePlayer#0001",
            RequestId = 1
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<GroupPromoteResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        AssertLocalization(response.LocalizedFailure, "NotInGroupMember", "GroupManager");
    }

    // Case 2: GroupKickRequest while solo → Success = false, NotInGroupMember
    [Fact]
    public void GroupKick_WhileSolo_ReturnsNotInGroupMember()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new GroupKickRequest
        {
            MemberName = "SomePlayer#0001",
            RequestId = 2
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<GroupKickResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        AssertLocalization(response.LocalizedFailure, "NotInGroupMember", "GroupManager");
    }

    // Case 3: GroupInviteRequest for unknown handle → Success = false, PlayerNotFound, exactly one response
    [Fact]
    public void GroupInvite_UnknownHandle_PlayerNotFoundFailure()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new GroupInviteRequest
        {
            FriendHandle = "NoSuchPlayer#9999",
            RequestId = 3
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<GroupInviteResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        AssertLocalization(response.LocalizedFailure, "PlayerNotFound", "Invite");
    }

    // Case 4: GroupJoinRequest for unknown handle → PlayerNotFound failure
    [Fact]
    public void GroupJoin_UnknownHandle_PlayerNotFoundFailure()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new GroupJoinRequest
        {
            FriendHandle = "NoSuchPlayer#9999",
            RequestId = 4
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<GroupJoinResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        AssertLocalization(response.LocalizedFailure, "PlayerNotFound", "Invite");
    }

    // Case 5: GroupLeaveRequest → player ends up solo, FriendListRefreshes == 1
    [Fact]
    public void GroupLeave_PlayerEndsSolo_FriendListRefreshed()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new GroupLeaveRequest { RequestId = 5 });

        Assert.True(GroupManager.GetPlayerGroup(accountId)?.IsSolo() ?? false);
        Assert.Equal(1, conn.FriendListRefreshes);
    }

    // Case 6: GroupConfirmationResponse with unknown confirmation number while in a solo group
    //         → exactly one system message recorded (invite-expired path)
    [Fact]
    public void GroupConfirmation_UnknownConfirmationNumber_OneSystemMessage()
    {
        long accountId = UniqueId();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        // Use GroupId = -1 (won't match the player's actual group) so we hit the
        // FailedToJoinGroupInviteExpired branch.
        registry.Dispatch(new GroupConfirmationResponse
        {
            ConfirmationNumber = -9999L,
            GroupId = -1L,
            JoinerAccountId = 0L,
            RequestId = 6
        });

        Assert.Single(conn.SystemMessages);
        Assert.Empty(conn.Sent);
    }
}
