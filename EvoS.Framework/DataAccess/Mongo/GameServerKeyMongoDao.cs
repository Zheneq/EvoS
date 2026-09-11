using System.Collections.Generic;
using EvoS.Framework.DataAccess.Daos;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo;

public class GameServerKeyMongoDao() :
    MongoDao<string, GameServerKeyDao.GameServerKey>(
        "game_server_keys",
        new CreateIndexModel<GameServerKeyDao.GameServerKey>(
            Builders<GameServerKeyDao.GameServerKey>.IndexKeys
                .Descending(k => k.FirstSeenAt))),
    GameServerKeyDao
{
    public GameServerKeyDao.GameServerKey Find(string fingerprint)
    {
        return findById(fingerprint);
    }

    public List<GameServerKeyDao.GameServerKey> FindAll()
    {
        return c.Find(f.Empty).Sort(s.Descending("FirstSeenAt")).ToList();
    }

    public void Save(GameServerKeyDao.GameServerKey key)
    {
        insert(key.Fingerprint, key);
    }
}
