using EvoS.Framework.Network;

namespace EvoS.Framework.Constants.Enums
{
    [EvosMessage(355)]
    public enum FriendStatus
    {
        Unknown,
        Friend,
        RequestSent,
        RequestReceived,
        Removed,
        Blocked,
        OnlineNonFriend
    }
}
