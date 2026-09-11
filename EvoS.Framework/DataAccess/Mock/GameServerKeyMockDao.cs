using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.DataAccess.Daos;

namespace EvoS.Framework.DataAccess.Mock
{
    public class GameServerKeyMockDao : GameServerKeyDao
    {
        private readonly ConcurrentDictionary<string, GameServerKeyDao.GameServerKey> _store = new();

        public GameServerKeyDao.GameServerKey Find(string fingerprint)
        {
            _store.TryGetValue(fingerprint, out GameServerKeyDao.GameServerKey key);
            return key;
        }

        public List<GameServerKeyDao.GameServerKey> FindAll()
        {
            return _store.Values.OrderByDescending(k => k.FirstSeenAt).ToList();
        }

        public void Save(GameServerKeyDao.GameServerKey key)
        {
            _store[key.Fingerprint] = key;
        }
    }
}
