using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;
using Moq;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests.DataAccess;

public class MatchHistoryDaoCachedTest : EvosTest
{
    public MatchHistoryDaoCachedTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }
    
    [Fact]
    public void TestSaveBeforeFetch()
    {
        const long accountId = 123;
        var dao = new Mock<MatchHistoryDao>();
        var originalHistory = Enumerable
            .Range(0, MatchHistoryDao.LIMIT)
            .Select(i => new PersistedCharacterMatchData { GameServerProcessCode = $"{i}" })
            .ToList();
        var history = new Queue<PersistedCharacterMatchData>(originalHistory);
        dao.Setup(d => d.Find(accountId)).Returns((long _) => history.Reverse().ToList());
        dao
            .Setup(d => d.Save(It.IsAny<ICollection<MatchHistoryDao.MatchEntry>>()))
            .Callback(
                (ICollection<MatchHistoryDao.MatchEntry> entries) =>
                {
                    Assert.True(entries.All(e => e.AccountId == accountId));
                    foreach (MatchHistoryDao.MatchEntry matchEntry in entries)
                    {
                       history.Enqueue(matchEntry.Data);
                    }
                    while (history.Count > MatchHistoryDao.LIMIT)
                    {
                        history.Dequeue();
                    }
                });

        var target = new MatchHistoryDaoCached(dao.Object);

        var input = new List<MatchHistoryDao.MatchEntry>
        {
            new() { AccountId = accountId, Data = new PersistedCharacterMatchData { GameServerProcessCode = "-1" } }
        };
        target.Save(input);

        var output = target.Find(accountId);
        
        Assert.Equal(MatchHistoryDao.LIMIT, output.Count);
        Assert.Equal(input[0].Data, output[0]);
        for (int i = 1; i < MatchHistoryDao.LIMIT; i++)
        {
            Assert.Equal(originalHistory[MatchHistoryDao.LIMIT - i], output[i]);
        }
    }
}