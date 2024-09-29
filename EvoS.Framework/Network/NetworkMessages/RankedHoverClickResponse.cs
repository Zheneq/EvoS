using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(200)]
    public class RankedHoverClickResponse : WebSocketResponseMessage
    {
        public LocalizationPayload LocalizedFailure;
    }
}
