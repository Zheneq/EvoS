using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos;

public interface UserMetadataDao
{
    UserMetadata Get(long accountId);
    void Update(UserMetadata data);
    void UpsertOptions(long accountId, EvosOptionsNotification options);
    public void UpsertLastSession(long accountId, string proxy, BuildVersionInfo version);
        
    public class UserMetadata
    {
        [BsonId]
        public long AccountId;
        public EvosOptionsNotification Options;
        public LastSessionInfo LastSessionInfo;
    }

    public class LastSessionInfo
    {
        public string Proxy;
        public BuildVersionInfo Version;
    }
}