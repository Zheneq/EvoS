using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class UserFeedbackMongoDao : MongoDao<ObjectId, UserFeedbackDao.UserFeedback>, UserFeedbackDao
    {
        public UserFeedbackMongoDao() : base(
            "user_feedback", 
            new CreateIndexModel<UserFeedbackDao.UserFeedback>(Builders<UserFeedbackDao.UserFeedback>.IndexKeys
                .Ascending(msg => msg.accountId)
                .Ascending(msg => msg.time)), 
            new CreateIndexModel<UserFeedbackDao.UserFeedback>(Builders<UserFeedbackDao.UserFeedback>.IndexKeys
                .Ascending(msg => msg.reportedPlayerAccountId)
                .Ascending(msg => msg.time)))
        {
        }

        public List<UserFeedbackDao.UserFeedback> Get(long accountId)
        {
            return c.Find(f.Eq(nameof(UserFeedbackDao.UserFeedback.accountId), accountId))
                .Sort(s.Descending(nameof(UserFeedbackDao.UserFeedback.time)))
                .Limit(100)
                .ToList();
        }

        public List<UserFeedbackDao.UserFeedback> GetReportsAgainst(long accountId)
        {
            return c.Find(f.Eq(nameof(UserFeedbackDao.UserFeedback.reportedPlayerAccountId), accountId))
                .Sort(s.Descending(nameof(UserFeedbackDao.UserFeedback.time)))
                .Limit(100)
                .ToList();
        }

        public void Save(UserFeedbackDao.UserFeedback entry)
        {
            insert(entry._id, entry);
        }
    }
} 