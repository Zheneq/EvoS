using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.DataAccess.Mongo;

public class UserMetadataMongoDao() : MongoDao<long, UserMetadataDao.UserMetadata>("user_metadata"), UserMetadataDao
{
    public UserMetadataDao.UserMetadata Get(long accountId)
    {
        return findById(accountId);
    }
        
    public void Update(UserMetadataDao.UserMetadata data)
    {
        insert(data.AccountId, data);
    }
        
    private static readonly FieldDefinition<EvosOptionsNotification> FOptions = new(x => x.Options);
    public void UpsertOptions(long accountId, EvosOptionsNotification options)
    {
        var result = UpdateField(accountId, options, FOptions);
        if (result.MatchedCount == 0)
        {
            Update(new UserMetadataDao.UserMetadata
            {
                AccountId = accountId,
                Options = options,
            });
        }
    }
        
    private static readonly FieldDefinition<UserMetadataDao.LastSessionInfo> FLastSession = new(x => x.LastSessionInfo);
    public void UpsertLastSession(long accountId, string proxy, BuildVersionInfo version)
    {
        var value = new UserMetadataDao.LastSessionInfo
        {
            Proxy = proxy,
            Version = version,
        };
        var result = UpdateField(accountId, value, FLastSession);
        if (result.MatchedCount == 0)
        {
            Update(new UserMetadataDao.UserMetadata
            {
                AccountId = accountId,
                LastSessionInfo = value,
            });
        }
    }
}