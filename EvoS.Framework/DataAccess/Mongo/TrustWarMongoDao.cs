using EvoS.Framework.DataAccess.Daos;
using MongoDB.Bson;
using MongoDB.Driver;
using static EvoS.Framework.DataAccess.Daos.TrustWarDao;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class TrustWarMongoDao : MongoDao<ObjectId, TrustWarDaoEntry>, TrustWarDao
    {
        public TrustWarMongoDao() : base(
            "TrustWar",
            new CreateIndexModel<TrustWarDaoEntry>(Builders<TrustWarDaoEntry>.IndexKeys
                .Ascending(msg => msg.Warbotics)
                .Ascending(msg => msg.Evos)
                .Ascending(msg => msg.Omni)))
        {
        }

        public TrustWarDaoEntry Find()
        {
            TrustWarDaoEntry entry = c.Find(Builders<TrustWarDaoEntry>.Filter.Empty).FirstOrDefault();
            if (entry == null)
            {
                return new TrustWarDaoEntry() { Omni = 0, Evos = 0, Warbotics = 0 };
            }
            return entry;
        }

        public void Save(TrustWarDaoEntry entry)
        {
            c.ReplaceOne(
                Builders<TrustWarDaoEntry>.Filter.Eq(x => x.Omni, entry.Omni),
                entry,
                new ReplaceOptions { IsUpsert = true });
            c.ReplaceOne(
                Builders<TrustWarDaoEntry>.Filter.Eq(x => x.Evos, entry.Evos),
                entry,
                new ReplaceOptions { IsUpsert = true });
            c.ReplaceOne(
                Builders<TrustWarDaoEntry>.Filter.Eq(x => x.Warbotics, entry.Warbotics),
                entry,
                new ReplaceOptions { IsUpsert = true });
        }
    }
}
