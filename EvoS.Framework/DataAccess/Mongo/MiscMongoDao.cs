using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class MiscMongoDao : MongoDao<string, MiscDao.Entry>, MiscDao
    {
        public MiscMongoDao() : base("misc")
        {
        }

        public MiscDao.Entry GetEntry(string key)
        {
            return findById(key);
        }

        public void SaveEntry(MiscDao.Entry data)
        {
            insert(data.Key, data);
        }
    }
} 