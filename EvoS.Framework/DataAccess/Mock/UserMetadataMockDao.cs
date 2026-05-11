using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.DataAccess.Mock;

public class UserMetadataMockDao: UserMetadataDao
{
    public UserMetadataDao.UserMetadata Get(long accountId)
    {
        return null;
    }

    public void Update(UserMetadataDao.UserMetadata data)
    {
    }

    public void UpsertOptions(long AccountId, EvosOptionsNotification options)
    {
    }

    public void UpsertLastSession(long accountId, string proxy, BuildVersionInfo version)
    {
    }
}