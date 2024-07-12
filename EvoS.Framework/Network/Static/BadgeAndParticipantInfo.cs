using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(249)]
    public class BadgeAndParticipantInfo
    {
        public static int ParticipantOrderDisplayPriority(TopParticipantSlot slot)
        {
            switch (slot)
            {
                case TopParticipantSlot.MostDecorated:
                    return 4;
                case TopParticipantSlot.Deadliest:
                    return 3;
                case TopParticipantSlot.Supportiest:
                    return 2;
                case TopParticipantSlot.Tankiest:
                    return 1;
                default:
                    return 0;
            }
        }

        public int PlayerId;
        public Team TeamId;
        public int TeamSlot;
        [EvosMessage(254)]
        public List<BadgeInfo> BadgesEarned;
        [EvosMessage(250)]
        public List<TopParticipantSlot> TopParticipationEarned;
        [EvosMessage(29)]
        public Dictionary<StatDisplaySettings.StatType, PercentileInfo> GlobalPercentiles;
        [EvosMessage(36)]
        public Dictionary<int, PercentileInfo> FreelancerSpecificPercentiles;
        public CharacterType FreelancerPlayed;
        [NonSerialized]
        public int ActorIndex;
    }
}
