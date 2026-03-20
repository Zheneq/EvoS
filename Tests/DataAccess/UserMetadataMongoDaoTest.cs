using EvoS.Framework.DataAccess.Mongo;
using EvoS.Framework.Network.NetworkMessages;
using Mongo2Go;
using MongoDB.Driver;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests.DataAccess;

public class UserMetadataMongoDaoTest(ITestOutputHelper testOutputHelper) : EvosTest(testOutputHelper)
{
    [Fact]
    public void TestPartialUpsert()
    {
        using MongoDbRunner runner = MongoDbRunner.Start();
        MongoClient client = new MongoClient(runner.ConnectionString);
        var database = client.GetDatabase("IntegrationTest");
        EvoS.Framework.DataAccess.Mongo.MongoDB.GetInstance().SetDatabase(database);
        UserMetadataMongoDao dao = new UserMetadataMongoDao();
        
        dao.UpsertOptions(1, new EvosOptionsNotification());

        var result = dao.Get(1);
        
        Assert.NotNull(result);
    }
    
}