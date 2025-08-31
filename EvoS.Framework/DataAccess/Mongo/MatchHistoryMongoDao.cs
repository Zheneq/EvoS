using System;
using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class MatchHistoryMongoDao : MongoDao<long, MatchHistoryDao.MatchEntry>, MatchHistoryDao
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchHistoryMongoDao));
        
        public MatchHistoryMongoDao() : base(
            "match_history", 
            new CreateIndexModel<MatchHistoryDao.MatchEntry>(Builders<MatchHistoryDao.MatchEntry>.IndexKeys
                .Ascending(entry => entry.AccountId)
                .Descending(entry => entry.Data.CreateDate)),
            new CreateIndexModel<MatchHistoryDao.MatchEntry>(Builders<MatchHistoryDao.MatchEntry>.IndexKeys
                .Ascending(entry => entry.AccountId)
                .Ascending(entry => entry.Data.GameServerProcessCode)))
        {
        }

        public List<PersistedCharacterMatchData> Find(long accountId)
        {
            return c.Find(
                f.Eq("AccountId", accountId))
                .SortByDescending(m => m.Data.CreateDate)
                .Limit(MatchHistoryDao.LIMIT)
                .Project(m => m.Data)
                .ToList();
        }

        public PersistedCharacterMatchData FindByProcessCode(long accountId, string processCode)
        {
            return c.Find(
                f.And(
                        f.Eq("AccountId", accountId),
                        f.Eq("Data.GameServerProcessCode", processCode)))
                .Project(m => m.Data)
                .FirstOrDefault();
        }

        public PersistedCharacterMatchData FindByTimestamp(long accountId, string timestamp)
        {
            DateTime? date = GameUtils.ParseGameId(timestamp);
            if (!date.HasValue)
            {
                log.Error($"Invalid timestamp: {timestamp}");
                return null;
            }
            
            return c.Find(
                    f.And(
                        f.Eq("AccountId", accountId),
                        f.Eq("Data.CreateDate", date)))
                .Project(m => m.Data)
                .FirstOrDefault();
        }

        public void Save(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
            if (!matchEntries.IsNullOrEmpty())
            {
                c.InsertMany(matchEntries);
            }
        }
    }
} 