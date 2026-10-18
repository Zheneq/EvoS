using System;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(743)]
    public class MatchmakingQueueStatusNotification : WebSocketMessage
    {
        public LobbyMatchmakingQueueInfo MatchmakingQueueInfo;

        public MatchmakingQueueStatusNotification Clone()
        {
            return (MatchmakingQueueStatusNotification)MemberwiseClone();
        }
    }
}
