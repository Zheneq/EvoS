using System;
using System.Collections.Generic;
using System.Linq;
using BitFaster.Caching.Lru;
using EvoS.Framework.DataAccess.Mock;

namespace EvoS.Framework.DataAccess.Daos
{
    public class RegistrationCodeDaoCached: RegistrationCodeDao
    {
        private readonly RegistrationCodeDao dao;
    
        private const int Capacity = 128;
        private readonly FastConcurrentLru<string, RegistrationCodeDao.RegistrationCodeEntry> cache = new(Capacity);

        public RegistrationCodeDaoCached(RegistrationCodeDao dao)
        {
            this.dao = dao;
        }

        private void Cache(RegistrationCodeDao.RegistrationCodeEntry entry)
        {
            cache.AddOrUpdate(entry.Code, entry);
        }

        public RegistrationCodeDao.RegistrationCodeEntry Find(string code)
        {
            if (cache.TryGet(code, out var entry))
            {
                return entry;
            }

            var nonCachedEntry = dao.Find(code);
            if (nonCachedEntry != null)
            {
                Cache(nonCachedEntry);
            }
            return nonCachedEntry;
        }

        private static bool IsIssued(RegistrationCodeDao.RegistrationCodeEntry entry) =>
            entry.State != RegistrationCodeDao.RegistrationState.Requested
            && entry.State != RegistrationCodeDao.RegistrationState.Declined;

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindIssuedBefore(int limit, DateTime dateTime)
        {
            if (dao is RegistrationCodeMockDao)
            {
                return cache
                    .Select(x => x.Value)
                    .Where(x => IsIssued(x) && x.IssuedAt < dateTime)
                    .OrderByDescending(x => x.IssuedAt)
                    .Take(limit)
                    .ToList();
            }

            List<RegistrationCodeDao.RegistrationCodeEntry> daoEntries = dao.FindIssuedBefore(limit, dateTime);
            daoEntries.ForEach(Cache);
            return daoEntries;
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindAllIssued(int limit, int offset)
        {
            if (dao is RegistrationCodeMockDao)
            {
                return cache
                    .Select(x => x.Value)
                    .Where(IsIssued)
                    .OrderByDescending(x => x.IssuedAt)
                    .Skip(offset)
                    .Take(limit)
                    .ToList();
            }

            List<RegistrationCodeDao.RegistrationCodeEntry> daoEntries = dao.FindAllIssued(limit, offset);
            daoEntries.ForEach(Cache);
            return daoEntries;
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindByState(RegistrationCodeDao.RegistrationState state, int limit)
        {
            if (dao is RegistrationCodeMockDao)
            {
                return cache
                    .Select(x => x.Value)
                    .Where(x => x.State == state)
                    .OrderByDescending(x => x.RequestedAt)
                    .Take(limit)
                    .ToList();
            }

            List<RegistrationCodeDao.RegistrationCodeEntry> daoEntries = dao.FindByState(state, limit);
            daoEntries.ForEach(Cache);
            return daoEntries;
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindLatestByDiscordUser(ulong discordUserId)
        {
            if (dao is RegistrationCodeMockDao)
            {
                return cache
                    .Select(x => x.Value)
                    .Where(x => x.DiscordUserId == discordUserId)
                    .OrderByDescending(x => x.RequestedAt)
                    .FirstOrDefault();
            }

            RegistrationCodeDao.RegistrationCodeEntry entry = dao.FindLatestByDiscordUser(discordUserId);
            if (entry != null)
            {
                Cache(entry);
            }
            return entry;
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindRequestByDiscordUser(ulong discordUserId, string username)
        {
            if (dao is RegistrationCodeMockDao)
            {
                return cache
                    .Select(x => x.Value)
                    .Where(x => x.DiscordUserId == discordUserId && x.IssuedTo == username)
                    .OrderByDescending(x => x.RequestedAt)
                    .FirstOrDefault();
            }

            RegistrationCodeDao.RegistrationCodeEntry entry = dao.FindRequestByDiscordUser(discordUserId, username);
            if (entry != null)
            {
                Cache(entry);
            }
            return entry;
        }

        public void Save(RegistrationCodeDao.RegistrationCodeEntry entry)
        {
            dao.Save(entry);
            Cache(entry);
        }
    }
}