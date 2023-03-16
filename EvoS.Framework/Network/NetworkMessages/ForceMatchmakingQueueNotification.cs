using System;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(747)]
    public class ForceMatchmakingQueueNotification : WebSocketMessage
    {
        public ActionType Action;
        public GameType GameType;

        [EvosMessage(748)]
        public enum ActionType
        {
            Unknown,
            Join,
            Leave
        }
    }
}
