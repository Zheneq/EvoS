using Mongo2Go;
using MongoDB.Driver;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("Tests.DataAccess.DbTestFramework", "Tests")]
namespace Tests.DataAccess;

public sealed class DbTestFramework: XunitTestFramework, IDisposable
{
    private readonly MongoDbRunner _runner;
    
    public DbTestFramework(IMessageSink messageSink) : base(messageSink)
    {
        _runner = MongoDbRunner.Start();
        MongoClient client = new MongoClient(_runner.ConnectionString);
        var database = client.GetDatabase("IntegrationTest");
        EvoS.Framework.DataAccess.Mongo.MongoDB.GetInstance().SetDatabase(database);
    }

    public new void Dispose()
    {
        _runner.Dispose();
        base.Dispose();
    }
    
}