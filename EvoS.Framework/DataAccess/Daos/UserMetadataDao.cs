using EvoS.Framework.Network.NetworkMessages;
using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos;

public interface UserMetadataDao
{
    UserMetadata Get(long accountId);
    void Update(UserMetadata data);
    void UpsertOptions(long accountId, EvosOptionsNotification options);
        
    public class UserMetadata
    {
        [BsonId]
        public long AccountId;
        public EvosOptionsNotification Options;
    }
}