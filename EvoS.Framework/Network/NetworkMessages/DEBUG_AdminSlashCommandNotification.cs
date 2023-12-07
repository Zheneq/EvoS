using EvoS.Framework.Network;
using EvoS.Framework.Network.WebSocket;
using System;

namespace LobbyGameClientMessages
{
    [Serializable]
    [EvosMessage(90)]
    public class DEBUG_AdminSlashCommandNotification : WebSocketMessage
    {
        public string Command;

        public string Arguments;
    }
}
