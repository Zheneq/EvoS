using System;
using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mock
{
    public class ChatHistoryMockDao: ChatHistoryDao
    {
        public List<ChatHistoryDao.Entry> GetRelevantMessagesAfter(
            long accountId,
            bool includeBlocked,
            bool includeGeneral,
            DateTime afterTime,
            int limit)
        {
            return new List<ChatHistoryDao.Entry>();
        }

        public List<ChatHistoryDao.Entry> GetRelevantMessagesBefore(
            long accountId,
            bool includeBlocked,
            bool includeGeneral,
            DateTime beforeTime,
            int limit)
        {
            return new List<ChatHistoryDao.Entry>();
        }

        public void Save(ChatHistoryDao.Entry entry)
        {
        }
    }
}