using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(206)]
    public class RankedBanResponse : WebSocketResponseMessage
    {
        public LocalizationPayload LocalizedFailure;
    }
}
