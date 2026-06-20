using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages;

[Serializable]
[EvosMessage(704)]
public class GameInvitationResponse : WebSocketResponseMessage
{
    public string InviteeHandle;
    public LocalizationPayload LocalizedFailure;
}