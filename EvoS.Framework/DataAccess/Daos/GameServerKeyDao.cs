using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos;

public enum GameServerKeyStatus
{
    Pending,
    Approved,
    Declined,
    Revoked,
}

public interface GameServerKeyDao
{
    public GameServerKey Find(string fingerprint);
    public List<GameServerKey> FindAll();
    public void Save(GameServerKey key);

    public class GameServerKey
    {
        // SHA-256 fingerprint of the public key, computed lobby-side. Serves as identity.
        [BsonId]
        public required string Fingerprint;
        // RSA public key as RSA.ToXmlString(false)
        public required string PublicKey;
        public string Name;
        public required GameServerKeyStatus Status;
        public required DateTime FirstSeenAt;
        public DateTime? ApprovedAt;
        public long? ApprovedByAccountId;
        public DateTime? LastConnectedAt;
        public string LastAddress;
        public string LastBuildVersion;

        public bool IsApproved => Status == GameServerKeyStatus.Approved;
    }
}
