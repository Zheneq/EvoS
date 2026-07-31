using System;
using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mock
{
    public class RegistrationCodeMockDao: RegistrationCodeDao
    {
        public RegistrationCodeDao.RegistrationCodeEntry Find(string code)
        {
            return null;
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindIssuedBefore(int limit, DateTime dateTime)
        {
            return new List<RegistrationCodeDao.RegistrationCodeEntry>();
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindAllIssued(int limit, int offset)
        {
            return new List<RegistrationCodeDao.RegistrationCodeEntry>();
        }

        public List<RegistrationCodeDao.RegistrationCodeEntry> FindByState(RegistrationCodeDao.RegistrationState state, int limit)
        {
            return new List<RegistrationCodeDao.RegistrationCodeEntry>();
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindLatestByDiscordUser(ulong discordUserId)
        {
            return null;
        }

        public RegistrationCodeDao.RegistrationCodeEntry FindRequestByDiscordUser(ulong discordUserId, string username)
        {
            return null;
        }

        public void Save(RegistrationCodeDao.RegistrationCodeEntry entry)
        {
        }
    }
}