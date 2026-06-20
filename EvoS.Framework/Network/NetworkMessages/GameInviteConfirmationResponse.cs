using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages;

[Serializable]
[EvosMessage(700)]
public class GameInviteConfirmationResponse : WebSocketResponseMessage
{
    public bool Accepted;
    public long GameCreatorAccountId;
    public int InitialRequestId;
    public LocalizationPayload LocalizedFailure;
}