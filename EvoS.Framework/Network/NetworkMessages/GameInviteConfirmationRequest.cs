using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages;

[Serializable]
[EvosMessage(701)]
public class GameInviteConfirmationRequest : WebSocketMessage
{
    public string GameCreatorHandle;
    public long GameCreatorAccountId;
    public int InitialRequestId;
}