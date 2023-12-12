using System.Collections.Concurrent;

namespace EvoS.Framework.DataAccess.Daos
{
    public class MiscDaoCached: MiscDao
    {
        private readonly MiscDao dao;
        private readonly ConcurrentDictionary<string, MiscDao.Entry> cache = new();

        public MiscDaoCached(MiscDao dao)
        {
            this.dao = dao;
        }

        private void Cache(MiscDao.Entry entry)
        {
            cache.AddOrUpdate(entry.Key, entry, (k, v) => entry);
        }

        public MiscDao.Entry GetEntry(string key)
        {
            if (cache.TryGetValue(key, out var entry))
            {
                return entry;
            }

            var nonCachedAccount = dao.GetEntry(key);
            if (nonCachedAccount != null)
            {
                Cache(nonCachedAccount);
            }
            return nonCachedAccount;
        }

        public void SaveEntry(MiscDao.Entry entry)
        {
            dao.SaveEntry(entry);
            Cache(entry);
        }
    }
}