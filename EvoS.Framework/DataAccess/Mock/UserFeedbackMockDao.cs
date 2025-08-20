using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mock
{
    public class UserFeedbackMockDao: UserFeedbackDao
    {
        public List<UserFeedbackDao.UserFeedback> Get(long accountId)
        {
            return new List<UserFeedbackDao.UserFeedback>();
        }

        public List<UserFeedbackDao.UserFeedback> GetReportsAgainst(long accountId)
        {
            return new List<UserFeedbackDao.UserFeedback>();
        }

        public void Save(UserFeedbackDao.UserFeedback entry)
        {
        }
    }
}