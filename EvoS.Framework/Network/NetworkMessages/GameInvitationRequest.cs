using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(705)]
    public class GameInvitationRequest : WebSocketMessage
    {
        public string InviteeHandle;
    }
}
