using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages;

[Serializable]
[EvosMessage(378)]
public class GroupJoinResponse : WebSocketResponseMessage
{
    public string FriendHandle;
    public LocalizationPayload LocalizedFailure;
}