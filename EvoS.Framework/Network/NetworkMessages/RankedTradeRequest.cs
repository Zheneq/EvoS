using EvoS.Framework.Network;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(203)]
    public class RankedTradeRequest : WebSocketMessage
    {
        public RankedTradeData Trade;
    }
}
