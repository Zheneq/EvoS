using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(205)]
    public class RankedSelectionRequest : WebSocketMessage
    {
        public CharacterType Selection;
    }
}
