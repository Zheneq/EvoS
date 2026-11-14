using CentralServer.LobbyServer.Group;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="IGroupRegistry.OnMembersChanged"/> and how matchmaking reacts to it: a group whose members or
/// leader changed leaves the queue.
/// Joined to [Collection("ClientNotifierSeam")] because group and queue state is process-global.
/// No player is online, so filtering sub types logs a "missing leader" error and leaves the mask as is.
/// </summary>
[Collection("ClientNotifierSeam")]
public class GroupMembersChangedTest(ITestOutputHelper output) : EvosTest(output)
{
    private readonly TestLobby _lobby = new();

    private GroupInfo MakeGroup(long leader, params long[] members)
    {
        _lobby.Groups.CreateGroup(leader);
        long groupId = _lobby.Groups.GetGroupID(leader);
        foreach (long member in members)
        {
            _lobby.Groups.JoinGroup(groupId, member);
        }
        return _lobby.Groups.GetGroup(groupId);
    }

    // Records the ids of the given groups each time OnMembersChanged is raised for one of them
    // (tests in other collections may change their own groups concurrently)
    private List<long> RecordMembersChanged(Action action, params long[] groupIds)
    {
        List<long> changed = [];

        void OnMembersChanged(GroupInfo group)
        {
            if (groupIds.Contains(group.GroupId))
            {
                changed.Add(group.GroupId);
            }
        }

        _lobby.Groups.OnMembersChanged += OnMembersChanged;
        try
        {
            action();
        }
        finally
        {
            _lobby.Groups.OnMembersChanged -= OnMembersChanged;
        }

        return changed;
    }

    [Fact]
    public void Join_RaisesOnceForLeftGroupAndOnceForJoinedGroup()
    {
        GroupInfo group = MakeGroup(9_200_001);
        GroupInfo soloGroup = MakeGroup(9_200_002);

        List<long> changed = RecordMembersChanged(
            () => _lobby.Groups.JoinGroup(group.GroupId, 9_200_002),
            group.GroupId, soloGroup.GroupId);

        Assert.Equal(new[] { soloGroup.GroupId, group.GroupId }, changed);
    }

    [Fact]
    public void Leave_RaisesOnce()
    {
        GroupInfo group = MakeGroup(9_200_003, 9_200_004);

        List<long> changed = RecordMembersChanged(
            () => _lobby.Groups.LeaveGroup(9_200_004),
            group.GroupId);

        Assert.Equal(new[] { group.GroupId }, changed);
    }

    [Fact]
    public void LeaveThatDisbandsGroup_RaisesOnce()
    {
        GroupInfo group = MakeGroup(9_200_005);

        List<long> changed = RecordMembersChanged(
            () => _lobby.Groups.LeaveGroup(9_200_005),
            group.GroupId);

        Assert.Equal(new[] { group.GroupId }, changed);
        Assert.Null(_lobby.Groups.GetGroup(group.GroupId));
    }

    [Fact]
    public void Promote_RaisesOnce()
    {
        GroupInfo group = MakeGroup(9_200_006, 9_200_007);

        List<long> changed = RecordMembersChanged(
            () => Assert.True(_lobby.Groups.PromoteMember(group, 9_200_007)),
            group.GroupId);

        Assert.Equal(new[] { group.GroupId }, changed);
    }

    [Fact]
    public void QueuedGroup_PlayerJoins_LeavesQueue()
    {
        GroupInfo group = MakeGroup(9_200_008);
        MakeGroup(9_200_009);
        Assert.True(_lobby.Matchmaking.AddGroupToQueue(GameType.PvP, group));

        _lobby.Groups.JoinGroup(group.GroupId, 9_200_009);

        Assert.False(_lobby.Matchmaking.IsQueued(group));
    }

    [Fact]
    public void QueuedGroup_MemberLeaves_LeavesQueue()
    {
        GroupInfo group = MakeGroup(9_200_010, 9_200_011);
        Assert.True(_lobby.Matchmaking.AddGroupToQueue(GameType.PvP, group));

        _lobby.Groups.LeaveGroup(9_200_011);

        Assert.False(_lobby.Matchmaking.IsQueued(group));
    }

    [Fact]
    public void QueuedGroup_LeaderPromotesMember_LeavesQueueAndResetsReadyState()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(9_200_012, 9_200_013);
        Assert.True(_lobby.Matchmaking.AddGroupToQueue(GameType.PvP, group));
        scope.Notifier.GroupRefreshes.Clear();

        Assert.True(_lobby.Groups.PromoteMember(group, 9_200_013));

        Assert.False(_lobby.Matchmaking.IsQueued(group));
        Assert.Contains((9_200_013L, true), scope.Notifier.GroupRefreshes);
    }
}
