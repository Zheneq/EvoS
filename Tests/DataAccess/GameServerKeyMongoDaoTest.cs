using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.DataAccess.Mongo;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests.DataAccess;

public class GameServerKeyMongoDaoTest(ITestOutputHelper testOutputHelper) : EvosTest(testOutputHelper)
{
    private static GameServerKeyDao.GameServerKey Key(
        string fingerprint, DateTime firstSeenAt,
        GameServerKeyStatus status = GameServerKeyStatus.Pending)
    {
        return new GameServerKeyDao.GameServerKey
        {
            Fingerprint = fingerprint,
            PublicKey = "<RSAKeyValue><Modulus>abc</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>",
            Status = status,
            FirstSeenAt = firstSeenAt,
            LastAddress = "127.0.0.1",
            LastBuildVersion = "test-build",
        };
    }

    [Fact]
    public void TestSaveAndFind()
    {
        GameServerKeyMongoDao dao = new GameServerKeyMongoDao();
        dao.Save(Key("fp-1", DateTime.UtcNow));

        GameServerKeyDao.GameServerKey result = dao.Find("fp-1");

        Assert.NotNull(result);
        Assert.Equal(GameServerKeyStatus.Pending, result.Status);
        Assert.Equal("test-build", result.LastBuildVersion);
        Assert.Null(dao.Find("does-not-exist"));
    }

    [Fact]
    public void TestApprovalPersists()
    {
        GameServerKeyMongoDao dao = new GameServerKeyMongoDao();
        dao.Save(Key("fp-2", DateTime.UtcNow));

        GameServerKeyDao.GameServerKey key = dao.Find("fp-2");
        key.Status = GameServerKeyStatus.Approved;
        key.ApprovedAt = DateTime.UtcNow;
        key.ApprovedByAccountId = 42;
        dao.Save(key);

        GameServerKeyDao.GameServerKey result = dao.Find("fp-2");
        Assert.True(result.IsApproved);
        Assert.Equal(42, result.ApprovedByAccountId);
    }

    [Fact]
    public void TestFindAllOrderedByFirstSeenDescending()
    {
        GameServerKeyMongoDao dao = new GameServerKeyMongoDao();
        DateTime now = DateTime.UtcNow;
        dao.Save(Key("order-1", now.AddSeconds(1)));
        dao.Save(Key("order-3", now.AddSeconds(3)));
        dao.Save(Key("order-2", now.AddSeconds(2)));

        List<GameServerKeyDao.GameServerKey> all = dao.FindAll();

        int i1 = all.FindIndex(k => k.Fingerprint == "order-1");
        int i2 = all.FindIndex(k => k.Fingerprint == "order-2");
        int i3 = all.FindIndex(k => k.Fingerprint == "order-3");
        Assert.True(i3 < i2 && i2 < i1, "Newest first-seen should sort first");
    }
}
