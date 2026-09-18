using CentralServer.LobbyServer.Group;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="GroupManager"/> methods that go through <see cref="CentralServer.LobbyServer.Session.IClientNotifier"/>.
/// All tests in this collection run sequentially because the notifier seam is process-global.
/// Any future test class that calls <see cref="CentralServer.LobbyServer.Session.ClientNotifier.Set"/> must join
/// [Collection("ClientNotifierSeam")].
/// </summary>
[Collection("ClientNotifierSeam")]
public class GroupManagerNotifierTest : EvosTest
{
    public GroupManagerNotifierTest(ITestOutputHelper output) : base(output)
    {
    }

    private static GroupInfo MakeGroup(long leaderId, params long[] members)
    {
        var group = new GroupInfo(1);
        group.AddPlayer(leaderId);
        foreach (long id in members)
        {
            group.AddPlayer(id);
        }
        return group;
    }

    [Fact]
    public void BroadcastSendsToAllMembersExceptSkipped()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(10, 20, 30);
        var message = new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null };

        GroupManager.Broadcast(group, message, skipAccountId: 20);

        Assert.Equal(2, scope.Notifier.Sent.Count);
        Assert.Contains(scope.Notifier.Sent, t => t.AccountId == 10);
        Assert.Contains(scope.Notifier.Sent, t => t.AccountId == 30);
        Assert.DoesNotContain(scope.Notifier.Sent, t => t.AccountId == 20);
    }

    [Fact]
    public void BroadcastSystemMessageSendsToAllMembersExceptSkipped()
    {
        using var scope = new ClientNotifierScope();
        GroupInfo group = MakeGroup(10, 20, 30);
        LocalizationPayload msg = LocalizationPayload.Create("GroupDisbanded", "Global");

        GroupManager.BroadcastSystemMessage(group, msg, skipAccountId: 30);

        Assert.Equal(2, scope.Notifier.SystemMessages.Count);
        Assert.Contains(scope.Notifier.SystemMessages, t => t.AccountId == 10);
        Assert.Contains(scope.Notifier.SystemMessages, t => t.AccountId == 20);
        Assert.DoesNotContain(scope.Notifier.SystemMessages, t => t.AccountId == 30);
    }

    [Fact]
    public void DefaultNotifierNoOpsForOfflineAccounts()
    {
        // SessionManager is empty in tests; the default SessionManagerClientNotifier must not throw.
        var message = new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null };
        LocalizationPayload sysMsg = LocalizationPayload.Create("GroupDisbanded", "Global");

        var ex = Record.Exception(() =>
        {
            var notifier = CentralServer.LobbyServer.Session.ClientNotifier.Get();
            notifier.Send(999, message);
            notifier.SendSystemMessage(999, sysMsg);
            notifier.MarkFriendListForUpdate(999);
            notifier.BroadcastRefreshGroup(999, true);
            notifier.NotifyJoinedGroup(999);
            notifier.NotifyLeftGroup(999);
            notifier.NotifyGroupDisbanded(999);
        });

        Assert.Null(ex);
    }
}
