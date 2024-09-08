using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(207)]
    public class RankedBanRequest : WebSocketMessage
    {
        public CharacterType Selection;
    }
}
