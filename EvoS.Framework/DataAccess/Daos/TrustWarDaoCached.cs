using System;
using System.Collections.Concurrent;
using EvoS.Framework.DataAccess.Mock;

namespace EvoS.Framework.DataAccess.Daos
{
    public class TrustWarDaoCached : TrustWarDao
    {
        private readonly TrustWarDao dao;
        private readonly ConcurrentDictionary<string, TrustWarDao.TrustWarDaoEntry> cache =
            new ConcurrentDictionary<string, TrustWarDao.TrustWarDaoEntry>();

        public TrustWarDaoCached(TrustWarDao dao)
        {
            this.dao = dao;
        }

        public TrustWarDao.TrustWarDaoEntry Find()
        {
            string cacheKey = "TrustWar";

            if (cache.TryGetValue(cacheKey, out var cachedEntry))
            {
                return cachedEntry;
            }

            var nonCachedEntry = dao.Find();
            if (nonCachedEntry != null)
            {
                Cache(cacheKey, nonCachedEntry);
            }

            return nonCachedEntry;
        }

        public void Save(TrustWarDao.TrustWarDaoEntry entry)
        {
            dao.Save(entry);
            string cacheKey = "TrustWar";
            Cache(cacheKey, entry);
        }

        private void Cache(string key, TrustWarDao.TrustWarDaoEntry entry)
        {
            cache.AddOrUpdate(key, entry, (k, v) => entry);
        }
    }
}
