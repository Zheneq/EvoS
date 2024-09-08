using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(204)]
    public class RankedSelectionResponse : WebSocketResponseMessage
    {
        public LocalizationPayload LocalizedFailure;
    }
}
