using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class MatchmakingQueueConfig
    {
        [JsonIgnore]
        public bool IsMatchmakingGroupSizeDependant => MatchmakingElo.Contains(EloKeyFlags.GROUP);
        public float PercentMaxTeamEloDifference;
        public List<EloKeyFlags> MatchmakingElo;
        public List<QueueEntryExperience> BlockedExperienceEntries;
        public GameType BlockedExperienceAlternativeGameType;
        public MissingLastTurnRankedOutcomeType MissingLastTurnRankingImpact;
        public Dictionary<RelaxCheckpoint, TimeSpan> QueueRelaxOverrides;
        public float QueueMinEloWidth;
        public float QueueMaxEloWidth;
        public TimeSpan QueueEloConeStartTime;
        public TimeSpan QueueEloConeEndTime;
        public int QueueMaxGroupImbalance;
        public TimeSpan? QueueGroupImbalanceStartTime;
        public TimeSpan? QueueGroupImbalanceEndTime;
        public int QueueSizeWhenEnoughPlayers;
        public int QueueSizeWhenTooManyPlayers;
        public double MaxSecondsToSpendBurningFullQueue;
        public SkillMeasureEnum SkillMeasure;
        // public Dictionary<int, GroupSizeSpecification> GroupRules;
        public bool ShowQueueSize;
        public int MaxWillFillPerTeam;
        public bool ReconsiderMaxWaitEntryWithEveryAddition;
        public int LeaderboardMinMatchesForRanking;
        public float PlacementKFactor;
        public float PlacementPointMultipleOnWin;
        public float PlacementPointMultipleOnLoss;
        public float MaximumELOWhenInPlacement;
        public int MMRDeltaNeededForMaxBonus;
        public TimeSpan LeaderboardExpirationTime;

        // public List<TierInfo> LeaderboardTiers;

        public Dictionary<int, int> LastSeasonsMMRToInitialPoints;
        public TimeSpan PreBanPhaseAnimationLength;
        public TimeSpan SelectSubPhaseBan1Timeout;
        public TimeSpan SelectSubPhaseBan2Timeout;
        public TimeSpan SelectSubPhaseFreelancerSelectTimeout;
        public TimeSpan SelectSubPhaseTradeTimeout;
        public bool BotsMasqueradeAsHumans;

        // public Penalties Penalties;

        public float GameLeavingPointsToGenerateATicket;
        public float GameLeavingPointsRedeemedEveryDay;
        public bool DebugRunTestHarness;

        public enum EloKeyFlags
        {
            GROUP,
            RELATIVE,
            SOFTENED_PUBLIC,
            SOFTENED_INDIVIDUAL,
            QUEUE
        }

        [EvosMessage(176)]
        public enum QueueEntryExperience
        {
            NewPlayer,
            Expert,
            MixedGroup
        }

        public enum MissingLastTurnRankedOutcomeType
        {
            WinsCountAsLosses,
            WinsCountAsWins,
            WinsIgnored
        }

        public enum RelaxCheckpoint
        {
            UNDEFINED,
            StartMatchmaking,
            AllowExpertCollision,
            AllowNoobCollision,
            IgnoreEloDifference,
            PadWithBots,
            RelaxCoopDifficultyRequest,
            AllowNoobExpertMixing,
            AllowRoleImbalance,
            AllowMissingRoles,
            AllowMajorTeamEloImbalance,
            AllowRepeatSameOpponents,
            AbandonWillFillParity,
            AbandonRegionUniqueness,
            ConsiderNonDefaultGroupComp,
            MaxTime
        }

        public enum SkillMeasureEnum
        {
            HANDICAPPED,
            ACCOUNT,
            CHARACTER,
            HANDICAPPED_OR_HIGHEST
        }

        public MatchmakingQueueConfig()
        {
            PercentMaxTeamEloDifference = 6.3f;
            QueueMinEloWidth = 100f;
            QueueMaxEloWidth = 600f;
            QueueEloConeStartTime = TimeSpan.Zero;
            QueueEloConeEndTime = TimeSpan.FromMinutes(4.0);
            QueueSizeWhenEnoughPlayers = 80;
            QueueSizeWhenTooManyPlayers = 1000;
            MaxSecondsToSpendBurningFullQueue = 0.33;
            SkillMeasure = SkillMeasureEnum.HANDICAPPED;
            ShowQueueSize = true;
            ReconsiderMaxWaitEntryWithEveryAddition = false;
            MatchmakingElo = new List<EloKeyFlags>();
            BlockedExperienceEntries = new List<QueueEntryExperience>();
            BlockedExperienceAlternativeGameType = GameType.None;
            LeaderboardExpirationTime = TimeSpan.FromDays(14.0);
            LeaderboardMinMatchesForRanking = 10;
            PlacementKFactor = 40f;
            PlacementPointMultipleOnWin = 1f;
            PlacementPointMultipleOnLoss = 1f;
            MMRDeltaNeededForMaxBonus = 100;
            MaximumELOWhenInPlacement = 1975f;
            PreBanPhaseAnimationLength = TimeSpan.FromSeconds(0.0);
            SelectSubPhaseBan1Timeout = TimeSpan.FromSeconds(60.0);
            SelectSubPhaseBan2Timeout = TimeSpan.FromSeconds(30.0);
            SelectSubPhaseFreelancerSelectTimeout = TimeSpan.FromSeconds(30.0);
            SelectSubPhaseTradeTimeout = TimeSpan.FromSeconds(30.0);
            BotsMasqueradeAsHumans = false;
            // this.Penalties = null;
            GameLeavingPointsToGenerateATicket = 10f;
            GameLeavingPointsRedeemedEveryDay = 0.5f;
            MissingLastTurnRankingImpact = MissingLastTurnRankedOutcomeType.WinsCountAsLosses;
            DebugRunTestHarness = false;
            LastSeasonsMMRToInitialPoints = null;
        }
    }
}
