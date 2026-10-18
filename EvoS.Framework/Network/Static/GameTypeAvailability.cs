using System;
using System.Collections.Generic;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(126)]
    public class GameTypeAvailability
    {
        [EvosMessage(167)]
        public Dictionary<int, RequirementCollection> QueueableGroupSizes;
        public bool IsActive;
        public int MinMatchesToAppearOnLeaderboard;
        public int MaxWillFillPerTeam;
        public int TeamAPlayers;
        public int TeamBPlayers;
        public int TeamABots;
        public int TeamBBots;
        public GameType BlockedExperienceAlternativeGameType;
        [EvosMessage(174)]
        public List<MatchmakingQueueConfig.QueueEntryExperience> BlockedExperienceEntries;
        public GameLeavingPenalty GameLeavingPenalty;
        [EvosMessage(170)]
        public List<TierDefinitions> PerTierDefinitions;
        public DateTime? PenaltyTimeout;
        public DateTime? ParoleTimeout;
        public RequirementCollection Requirements;
        [EvosMessage(130)]
        public List<GameSubType> SubTypes;
        [EvosMessage(127)]
        public Dictionary<ushort, DateTime> XPPenaltyTimeout;

        public GameTypeAvailability Clone()
        {
            return (GameTypeAvailability)MemberwiseClone();
        }
    }
}
