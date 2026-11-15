using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests that a group removed from a matchmaking queue is told it left, whatever removed it.
/// A match start removes the matched groups with <see cref="MatchmakingQueue.RemoveGroup"/> directly,
/// so these tests call it directly too.
/// Joined to [Collection("ClientNotifierSeam")] because group and queue state is process-global.
/// No player is online, so filtering sub types logs a "missing leader" error and leaves the mask as is.
/// </summary>
[Collection("ClientNotifierSeam")]
public class MatchmakingQueueLeftNotificationTest(ITestOutputHelper output) : EvosTest(output)
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

    private MatchmakingQueue QueueGroup(GroupInfo group)
    {
        MatchmakingQueue queue = _lobby.Matchmaking.GetQueue(GameType.PvP);
        queue.AddGroup(group.GroupId, out bool added);
        Assert.True(added);
        return queue;
    }

    private static int CountLeftQueue(RecordingClientNotifier notifier, long accountId)
    {
        return notifier.Sent.Count(t => t.AccountId == accountId
                                        && t.Message is MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo: null });
    }

    // Other test classes may send to their own accounts concurrently, so only this leader's refreshes count
    private static int CountGroupRefreshes(RecordingClientNotifier notifier, long leader)
    {
        return notifier.GroupRefreshes.Count(r => r.AccountId == leader);
    }

    [Fact]
    public void RemoveGroup_TellsMembersTheyLeftQueue()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(9_300_001, 9_300_002);
        MatchmakingQueue queue = QueueGroup(group);
        scope.Notifier.Sent.Clear();
        scope.Notifier.GroupRefreshes.Clear();

        Assert.True(queue.RemoveGroup(group.GroupId));

        Assert.Equal(1, CountLeftQueue(scope.Notifier, 9_300_001));
        Assert.Equal(1, CountLeftQueue(scope.Notifier, 9_300_002));
        Assert.Contains((9_300_001L, false), scope.Notifier.GroupRefreshes);
    }

    [Fact]
    public void RemoveGroupFromQueue_TellsMembersOnce()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(9_300_003, 9_300_004);
        QueueGroup(group);
        scope.Notifier.Sent.Clear();
        scope.Notifier.GroupRefreshes.Clear();

        Assert.True(_lobby.Matchmaking.RemoveGroupFromQueue(group));

        Assert.Equal(1, CountLeftQueue(scope.Notifier, 9_300_003));
        Assert.Equal(1, CountLeftQueue(scope.Notifier, 9_300_004));
        Assert.Equal(1, CountGroupRefreshes(scope.Notifier, 9_300_003));
    }

    [Fact]
    public void RemoveGroup_NotQueued_TellsNobody()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(9_300_005, 9_300_006);
        MatchmakingQueue queue = _lobby.Matchmaking.GetQueue(GameType.PvP);
        scope.Notifier.Sent.Clear();
        scope.Notifier.GroupRefreshes.Clear();

        Assert.False(queue.RemoveGroup(group.GroupId));

        Assert.DoesNotContain(scope.Notifier.Sent, t => t.AccountId is 9_300_005 or 9_300_006);
        Assert.Equal(0, CountGroupRefreshes(scope.Notifier, 9_300_005));
    }
}
