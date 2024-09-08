using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(201)]
    public class RankedHoverClickRequest : WebSocketMessage
    {
        public CharacterType Selection;
    }
}
