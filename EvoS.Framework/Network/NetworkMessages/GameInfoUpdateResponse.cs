using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(706)]
    public class GameInfoUpdateResponse : WebSocketResponseMessage
    {
        public LobbyGameInfo GameInfo;

        public LobbyTeamInfo TeamInfo;

        public LocalizationPayload LocalizedFailure;

        public GameInfoUpdateResponse Clone()
        {
            return (GameInfoUpdateResponse)MemberwiseClone();
        }
    }
}
