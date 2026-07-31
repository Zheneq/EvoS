using System;
using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class RegistrationCodeMongoDao : MongoDao<string, RegistrationCodeDao.RegistrationCodeEntry>, RegistrationCodeDao
    {
        public RegistrationCodeMongoDao() : base(
            "registration_codes",
            new CreateIndexModel<RegistrationCodeDao.RegistrationCodeEntry>(Builders<RegistrationCodeDao.RegistrationCodeEntry>.IndexKeys
                .Descending(entry => entry.IssuedAt)),
            new CreateIndexModel<RegistrationCodeDao.RegistrationCodeEntry>(Builders<RegistrationCodeDao.RegistrationCodeEntry>.IndexKeys
                .Ascending(entry => entry.DiscordUserId)))
        {
        }
        
        private FilterDefinition<RegistrationCodeDao.RegistrationCodeEntry> IssuedOnly =>
            f.And(
                f.Ne("State", RegistrationCodeDao.RegistrationState.Requested),
                f.Ne("State", RegistrationCodeDao.RegistrationState.Declined));

        public RegistrationCodeDao.RegistrationCodeEntry Find(string code)
        {
            return c.Find(f.Eq("Code", code)).FirstOrDefault();
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindIssuedBefore(int limit, DateTime dateTime)
        {
            return c
                .Find(f.And(IssuedOnly, f.Lt("IssuedAt", dateTime)))
                .Sort(s.Descending("IssuedAt"))
                .Limit(limit)
                .ToList();
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindAllIssued(int limit, int offset)
        {
            return c
                .Find(IssuedOnly)
                .Sort(s.Descending("IssuedAt"))
                .Skip(offset)
                .Limit(limit)
                .ToList();
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindByState(RegistrationCodeDao.RegistrationState state, int limit)
        {
            return c
                .Find(f.Eq("State", state))
                .Sort(s.Descending("RequestedAt"))
                .Limit(limit)
                .ToList();
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindLatestByDiscordUser(ulong discordUserId)
        {
            return c
                .Find(f.Eq("DiscordUserId", discordUserId))
                .Sort(s.Descending("RequestedAt"))
                .FirstOrDefault();
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindRequestByDiscordUser(ulong discordUserId, string username)
        {
            return c
                .Find(f.And(f.Eq("DiscordUserId", discordUserId), f.Eq("IssuedTo", username)))
                .Sort(s.Descending("RequestedAt"))
                .FirstOrDefault();
        }

        public void Save(RegistrationCodeDao.RegistrationCodeEntry entry)
        {
            insert(entry.Code, entry);
        }
    }
}
