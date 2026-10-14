using System;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(746)]
    public class MatchmakingQueueAssignmentNotification : WebSocketMessage
    {
        public LobbyMatchmakingQueueInfo MatchmakingQueueInfo;
        public string Reason;

        public MatchmakingQueueAssignmentNotification()
        {
            RequestId = 0;
            ResponseId = 0;
        }

        public MatchmakingQueueAssignmentNotification Clone()
        {
            return (MatchmakingQueueAssignmentNotification)MemberwiseClone();
        }
    }
}
