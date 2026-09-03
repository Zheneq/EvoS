using CentralServer.BridgeServer;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;

namespace Tests;

public class GameServerKeyManagerTest
{
    private sealed class FakeConnection : IGameServerConnection
    {
        public bool IsConnected { get; set; } = true;
        public int Completed { get; private set; }
        public int Rejected { get; private set; }
        public void CompleteRegistration() => Completed++;
        public void RejectRegistration() => Rejected++;
    }

    private static string Seed(GameServerKeyStatus status)
    {
        string fingerprint = "mgr-" + Guid.NewGuid();
        DB.Get().GameServerKeyDao.Save(new GameServerKeyDao.GameServerKey
        {
            Fingerprint = fingerprint,
            PublicKey = "pk",
            Status = status,
            FirstSeenAt = DateTime.UtcNow,
        });
        return fingerprint;
    }

    [Fact]
    public void ApproveCompletesHeldPendingConnection()
    {
        string fp = Seed(GameServerKeyStatus.Pending);
        FakeConnection conn = new FakeConnection();
        GameServerKeyManager.AddPending(fp, conn);

        Assert.True(GameServerKeyManager.Approve(fp, adminAccountId: 1));

        Assert.Equal(1, conn.Completed);
        Assert.Equal(0, conn.Rejected);
        Assert.Equal(GameServerKeyStatus.Approved, DB.Get().GameServerKeyDao.Find(fp).Status);
        // No longer counts as in use after it is taken out of the pending set.
        Assert.False(GameServerKeyManager.IsKeyInUse(fp, "proc"));
    }

    [Fact]
    public void DeclineRejectsHeldPendingConnection()
    {
        string fp = Seed(GameServerKeyStatus.Pending);
        FakeConnection conn = new FakeConnection();
        GameServerKeyManager.AddPending(fp, conn);

        Assert.True(GameServerKeyManager.Reject(fp, adminAccountId: 1));

        Assert.Equal(1, conn.Rejected);
        Assert.Equal(0, conn.Completed);
        Assert.Equal(GameServerKeyStatus.Declined, DB.Get().GameServerKeyDao.Find(fp).Status);
    }

    [Fact]
    public void RevokeApprovedKeyMarksRevoked()
    {
        string fp = Seed(GameServerKeyStatus.Approved);

        Assert.True(GameServerKeyManager.Reject(fp, adminAccountId: 1));

        Assert.Equal(GameServerKeyStatus.Revoked, DB.Get().GameServerKeyDao.Find(fp).Status);
    }

    [Fact]
    public void ApproveUnknownKeyReturnsFalse()
    {
        Assert.False(GameServerKeyManager.Approve("missing-" + Guid.NewGuid(), adminAccountId: 1));
    }

    [Fact]
    public void StrictModeReportsKeyInUseWhilePendingThenFreeAfterRemoval()
    {
        string fp = "inuse-" + Guid.NewGuid();
        FakeConnection conn = new FakeConnection();

        Assert.False(GameServerKeyManager.IsKeyInUse(fp, "proc-1"));

        GameServerKeyManager.AddPending(fp, conn);
        // A second connection with the same key is reported in-use regardless of its process code.
        Assert.True(GameServerKeyManager.IsKeyInUse(fp, "proc-1"));
        Assert.True(GameServerKeyManager.IsKeyInUse(fp, "proc-2"));

        GameServerKeyManager.RemovePending(conn);
        Assert.False(GameServerKeyManager.IsKeyInUse(fp, "proc-1"));
    }

    [Fact]
    public void ApproveDoesNotCompleteDisconnectedConnection()
    {
        string fp = Seed(GameServerKeyStatus.Pending);
        FakeConnection conn = new FakeConnection { IsConnected = false };
        GameServerKeyManager.AddPending(fp, conn);

        GameServerKeyManager.Approve(fp, adminAccountId: 1);

        Assert.Equal(0, conn.Completed);
        Assert.False(GameServerKeyManager.IsKeyInUse(fp, "proc"));
    }
}
