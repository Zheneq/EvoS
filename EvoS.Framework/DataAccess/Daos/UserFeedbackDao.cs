using System;
using System.Collections.Generic;
using EvoS.Framework.Network.NetworkMessages;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface UserFeedbackDao
    {
        List<UserFeedback> Get(long accountId);
        List<UserFeedbackDao.UserFeedback> GetReportsAgainst(long accountId);
        void Save(UserFeedback entry);
        
        public class UserFeedback
        {
            [BsonId] public ObjectId _id = ObjectId.GenerateNewId();
            public long accountId;
            public DateTime time;
            public string context;
            public string message;
            public ClientFeedbackReport.FeedbackReason reason;
            public long reportedPlayerAccountId;
            public string reportedPlayerHandle;

            public UserFeedback(long AccountId, ClientFeedbackReport Report, string Context)
            {
                accountId = AccountId;
                time = DateTime.UtcNow;
                context = Context;
                message = Report.Message;
                reason = Report.Reason;
                reportedPlayerAccountId = Report.ReportedPlayerAccountId;
                reportedPlayerHandle = Report.ReportedPlayerHandle;
            }
        }
    }
}