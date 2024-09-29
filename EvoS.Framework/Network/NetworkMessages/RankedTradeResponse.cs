using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(202)]
    public class RankedTradeResponse : WebSocketResponseMessage
    {
        public LocalizationPayload LocalizedFailure;
    }
}
