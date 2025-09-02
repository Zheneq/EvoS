using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess.Daos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class ChatHistoryMongoDao : MongoDao<ObjectId, ChatHistoryDao.Entry>, ChatHistoryDao
    {
        public ChatHistoryMongoDao() : base(
            "chat_history", 
            new CreateIndexModel<ChatHistoryDao.Entry>(Builders<ChatHistoryDao.Entry>.IndexKeys
                .Ascending(msg => msg.sender)
                .Ascending(msg => msg.time)), 
            new CreateIndexModel<ChatHistoryDao.Entry>(Builders<ChatHistoryDao.Entry>.IndexKeys
                .Ascending(msg => msg.recipients)
                .Ascending(msg => msg.time)), 
            new CreateIndexModel<ChatHistoryDao.Entry>(Builders<ChatHistoryDao.Entry>.IndexKeys
                .Ascending(msg => msg.blockedRecipients)
                .Ascending(msg => msg.time)),
            new CreateIndexModel<ChatHistoryDao.Entry>(Builders<ChatHistoryDao.Entry>
                    .IndexKeys
                    .Ascending(msg => msg.time),
                new CreateIndexOptions<ChatHistoryDao.Entry>
                {
                    PartialFilterExpression = f.Eq(e => e.consoleMessageType, ConsoleMessageType.GlobalChat)
                }))
        {
        }

        public List<ChatHistoryDao.Entry> GetRelevantMessages(
            long accountId,
            bool includeBlocked,
            bool includeGeneral,
            bool isAfter,
            DateTime time,
            int limit)
        {
            if (isAfter)
            {
                return c
                    .Find(f.And(
                        f.Gte(e => e.time, time),
                        MakeBaseCondition(accountId, includeBlocked, includeGeneral)
                    ))
                    .Sort(s.Ascending(e => e.time))
                    .Limit(limit)
                    .ToList();
            }
            else
            {
                List<ChatHistoryDao.Entry> entries = c
                    .Find(f.And(
                        f.Lte(e => e.time, time),
                        MakeBaseCondition(accountId, includeBlocked, includeGeneral)
                    ))
                    .Sort(s.Descending(e => e.time))
                    .Limit(limit)
                    .ToList();
                entries.Reverse();
                return entries;
            }
        }


        private static FilterDefinition<ChatHistoryDao.Entry> MakeBaseCondition(long accountId, bool includeBlocked, bool includeGeneral)
        {
            var cond = f.Or(
                f.Eq(e => e.sender, accountId),
                f.AnyEq(e => e.recipients, accountId));
            cond = includeBlocked
                ? f.Or(cond, f.AnyEq(e => e.blockedRecipients, accountId))
                : cond;
            cond = includeGeneral
                ? f.Or(cond, f.Eq(e => e.consoleMessageType, ConsoleMessageType.GlobalChat))
                : cond;
            return cond;
        }

        public void Save(ChatHistoryDao.Entry entry)
        {
            insert(entry._id, entry);
        }
    }
} 