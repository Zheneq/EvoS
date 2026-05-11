using EvoS.Framework.DataAccess.Mongo;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests.DataAccess;

public class UserMetadataMongoDaoTest(ITestOutputHelper testOutputHelper) : EvosTest(testOutputHelper)
{
    [Fact]
    public void TestPartialUpsert()
    {
        UserMetadataMongoDao dao = new UserMetadataMongoDao();
        
        dao.UpsertOptions(1, new EvosOptionsNotification());

        var result = dao.Get(1);
        
        Assert.NotNull(result);
    }
    
    [Fact]
    public void TestNullUpsert()
    {
        UserMetadataMongoDao dao = new UserMetadataMongoDao();

        var expectedProxy = "proxy";
        var expectedVersion = new BuildVersionInfo("STABLE-122-100_1.5-Beta");
        
        dao.UpsertLastSession(2, "proxy", expectedVersion);

        var result = dao.Get(2);
        
        Assert.NotNull(result);
        Assert.Equal(expectedProxy, result.LastSessionInfo.Proxy);
        Assert.Equal(expectedVersion, result.LastSessionInfo.Version);
        
        dao.UpsertLastSession(2, null, expectedVersion);
        result = dao.Get(2);
        
        Assert.NotNull(result);
        Assert.Null(result.LastSessionInfo.Proxy);
        Assert.Equal(expectedVersion, result.LastSessionInfo.Version);
    }
}