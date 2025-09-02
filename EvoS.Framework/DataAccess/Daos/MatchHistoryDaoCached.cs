using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BitFaster.Caching.Lru;
using EvoS.Framework.Network.Static;
using Microsoft.IdentityModel.Tokens;

namespace EvoS.Framework.DataAccess.Daos
{
    public class MatchHistoryDaoCached: MatchHistoryDao
    {
        private readonly MatchHistoryDao dao;
    
        private const int Capacity = 1024;
        private readonly FastConcurrentLru<long, ConcurrentQueue<PersistedCharacterMatchData>> cache = new(Capacity);
        private readonly FastConcurrentLru<long, object> accountLocks = new(Capacity);

        public MatchHistoryDaoCached(MatchHistoryDao dao)
        {
            this.dao = dao;
        }

        private void AddToCache(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
            var byAccount = matchEntries.GroupBy(m => m.AccountId);

            foreach (IGrouping<long, MatchHistoryDao.MatchEntry> accountToMatches in byAccount)
            {
                long accountId = accountToMatches.Key;
                object _lock = accountLocks.GetOrAdd(accountId, _ => new());
                lock (_lock)
                {
                    if (!cache.TryGet(accountId, out var matches))
                    {
                        Fetch(accountId);
                        matches = cache.GetOrAdd(accountId, _ => new());
                    }
                    
                    foreach (MatchHistoryDao.MatchEntry match in accountToMatches)
                    {
                        matches.Enqueue(match.Data);
                    }
                    while (matches.Count > MatchHistoryDao.LIMIT)
                    {
                        matches.TryDequeue(out _);
                    }
                }
            }
        }

        private void OverrideCache(long accountId, List<PersistedCharacterMatchData> matchDataList)
        {
            cache.AddOrUpdate(accountId, new ConcurrentQueue<PersistedCharacterMatchData>(matchDataList.AsEnumerable().Reverse()));
        }

        public List<PersistedCharacterMatchData> Find(long accountId)
        {
            object _lock = accountLocks.GetOrAdd(accountId, _ => new());
            lock (_lock)
            {
                bool hasCachedValue = cache.TryGet(accountId, out var cachedMatches);
                if (hasCachedValue)
                {
                    return cachedMatches.Reverse().ToList();
                }

                return Fetch(accountId);
            }
        }

        public List<PersistedCharacterMatchData> Find(long accountId, bool isAfter, DateTime afterTime, int limit)
        {
            return dao.Find(accountId, isAfter, afterTime, limit);
        }

        public PersistedCharacterMatchData FindByProcessCode(long accountId, string processCode)
        {
            return dao.FindByProcessCode(accountId, processCode);
        }

        public PersistedCharacterMatchData FindByTimestamp(long accountId, string timestamp)
        {
            return dao.FindByTimestamp(accountId, timestamp);
        }

        private List<PersistedCharacterMatchData> Fetch(long accountId)
        {
            List<PersistedCharacterMatchData> dbMatches = dao.Find(accountId);
            if (!dbMatches.IsNullOrEmpty())
            {
                OverrideCache(accountId, dbMatches);
            }
            return dbMatches ?? new();
        }

        public void Save(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
            AddToCache(matchEntries);
            dao.Save(matchEntries);
        }
    }
}