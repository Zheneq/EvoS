using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages;

[Serializable]
[EvosMessage(379)]
public class GroupJoinRequest : WebSocketMessage
{
    public string FriendHandle;
}