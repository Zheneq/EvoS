using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(120)]
    public class ServerQueueConfigurationUpdateNotification : WebSocketMessage
    {
        [EvosMessage(123)]
        public Dictionary<GameType, GameTypeAvailability> GameTypeAvailabilies; // Available game types (practice, vs bots, pvp, ranked, custom)
        [EvosMessage(177)]
        public Dictionary<CharacterType, RequirementCollection> FreeRotationAdditions;
        [EvosMessage(121)]
        public List<LocalizationPayload> TierInstanceNames;
        public bool AllowBadges;
        public int NewPlayerPvPQueueDuration;

        public ServerQueueConfigurationUpdateNotification Clone()
        {
            return (ServerQueueConfigurationUpdateNotification)MemberwiseClone();
        }
    }
}
