using System;
using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.DataAccess.Mock
{
    public class MatchHistoryMockDao: MatchHistoryDao
    {

        public List<PersistedCharacterMatchData> Find(long accountId)
        {
            return new List<PersistedCharacterMatchData>();
        }

        public List<PersistedCharacterMatchData> Find(long accountId, bool isAfter, DateTime afterTime, int limit)
        {
            return new List<PersistedCharacterMatchData>();
        }

        public PersistedCharacterMatchData FindByProcessCode(long accountId, string processCode)
        {
            return null;
        }

        public PersistedCharacterMatchData FindByTimestamp(long accountId, string timestamp)
        {
            return null;
        }

        public void Save(ICollection<MatchHistoryDao.MatchEntry> matchEntries)
        {
        }
    }
}