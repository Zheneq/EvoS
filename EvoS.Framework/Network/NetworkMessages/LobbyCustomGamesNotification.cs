using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;


namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(764)]
    public class LobbyCustomGamesNotification : WebSocketMessage
    {
        [EvosMessage(765)]
        public List<LobbyGameInfo> CustomGameInfos;

        public LobbyCustomGamesNotification Clone()
        {
            return (LobbyCustomGamesNotification)MemberwiseClone();
        }
    }
}
