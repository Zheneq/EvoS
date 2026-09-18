using CentralServer.LobbyServer.Matchmaking;

namespace Tests;

public class QueuePriorityManagerTest
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime Future = Now.AddMinutes(2);
    private static readonly DateTime Past = Now.AddMinutes(-1);

    // Distinct account id ranges per test — QueuePriorityManager keeps a shared static store.

    [Fact]
    public void AllMembersCredited_ReturnsLatestOriginalQueueTime()
    {
        const long a = 1001, b = 1002;
        DateTime early = Now.AddMinutes(-10);
        DateTime late = Now.AddMinutes(-3);
        QueuePriorityManager.GrantCredit(a, early, Future);
        QueuePriorityManager.GrantCredit(b, late, Future);

        bool ok = QueuePriorityManager.TryGetGroupQueueTime([a, b], out DateTime queueTime);

        Assert.True(ok);
        Assert.Equal(late, queueTime); // latest (most conservative) of the members' times

        QueuePriorityManager.Consume([a, b]);
    }

    [Fact]
    public void MissingMember_ReturnsFalse()
    {
        const long a = 2001, uncredited = 2002;
        QueuePriorityManager.GrantCredit(a, Now.AddMinutes(-5), Future);

        Assert.False(QueuePriorityManager.TryGetGroupQueueTime([a, uncredited], out _));

        QueuePriorityManager.Consume([a]);
    }

    [Fact]
    public void ExpiredCredit_ReturnsFalse()
    {
        const long a = 3001;
        QueuePriorityManager.GrantCredit(a, Now.AddMinutes(-5), Past);

        Assert.False(QueuePriorityManager.TryGetGroupQueueTime([a], out _));
    }

    [Fact]
    public void Consume_ClearsCredit()
    {
        const long a = 4001;
        QueuePriorityManager.GrantCredit(a, Now.AddMinutes(-5), Future);
        Assert.True(QueuePriorityManager.TryGetGroupQueueTime([a], out _));

        QueuePriorityManager.Consume([a]);

        Assert.False(QueuePriorityManager.TryGetGroupQueueTime([a], out _));
    }

    [Fact]
    public void EmptyMembers_ReturnsFalse()
    {
        Assert.False(QueuePriorityManager.TryGetGroupQueueTime([], out _));
    }
}
