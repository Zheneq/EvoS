using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Network.Static;
using Microsoft.IdentityModel.Tokens;

namespace EvoS.Framework.DataAccess.Daos
{
    public class MatchHistoryDaoCached: MatchHistoryDao
    {
        private readonly MatchHistoryDao dao;
        private readonly ConcurrentDictionary<long, ConcurrentQueue<PersistedCharacterMatchData>> cache = new();
        private readonly ConcurrentDictionary<long, bool> cachedAccounts = new();
        private readonly ConcurrentDictionary<long, object> accountLocks = new();

        public MatchHistoryDaoCached(MatchHistoryDao dao)
        {
            this.dao = dao;
        }

        private void AddToCache(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
            var byAccount = matchEntries.GroupBy(m => m.AccountId);

            foreach (IGrouping<long, MatchHistoryDao.MatchEntry> accountToMatches in byAccount)
            {
                ConcurrentQueue<PersistedCharacterMatchData> matches = cache
                    .GetOrAdd(accountToMatches.Key, _ => new ConcurrentQueue<PersistedCharacterMatchData>());

                lock (matches)
                {
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
            cache[accountId] = new ConcurrentQueue<PersistedCharacterMatchData>(matchDataList.AsEnumerable().Reverse());
        }

        public List<PersistedCharacterMatchData> Find(long accountId)
        {
            object _lock = accountLocks.GetOrAdd(accountId, _ => new());
            lock (_lock)
            {
                bool hasCachedValue = cache.TryGetValue(accountId, out var cachedMatches);
                if (cachedAccounts.ContainsKey(accountId))
                {
                    return hasCachedValue ? cachedMatches.Reverse().ToList() : new();
                }

                List<PersistedCharacterMatchData> dbMatches = dao.Find(accountId);
                if (!dbMatches.IsNullOrEmpty())
                {
                    OverrideCache(accountId, dbMatches);
                }
                cachedAccounts[accountId] = true;
                return dbMatches;
            }
        }
        
        public void Save(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
            foreach (long accountId in matchEntries.Select(m => m.AccountId).Distinct())
            {
                if (!cachedAccounts.ContainsKey(accountId))
                {
                    Find(accountId);
                }
            }

            dao.Save(matchEntries);
            AddToCache(matchEntries);
        }
    }
}