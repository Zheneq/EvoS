using System;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(744)]
    public class LobbyMatchmakingQueueInfo
    {
        public bool ShowQueueSize;
        public int QueuedPlayers;
        public float PlayersPerMinute;
        public TimeSpan AverageWaitTime = TimeSpan.FromMinutes(5.0);
        public LobbyGameConfig GameConfig;
        public QueueStatus QueueStatus;

        public GameType GameType => GameConfig?.GameType ?? GameType.None;

        public override string ToString()
        {
            return (GameConfig != null) ? GameConfig.ToString() : "unknown";
        }

        public LobbyMatchmakingQueueInfo Clone()
        {
            return (LobbyMatchmakingQueueInfo) MemberwiseClone();
        }

        public bool IsSame(GameType gameType)
        {
            return GameConfig != null && GameConfig.GameType == gameType;
        }

        public string WhatQueueIsWaitingForToMakeNextGame
        {
            get
            {
                switch (QueueStatus)
                {
                    case QueueStatus.Idle:
                    case QueueStatus.Success:
                    case QueueStatus.WaitingForHumans:
                    case QueueStatus.QueueDoesntHaveEnoughHumans:
                    case QueueStatus.SubQueueConflict:
                    case QueueStatus.MultipleRegions:
                        return "AFewPlayers";
                    case QueueStatus.WontMixNoobAndExpert:
                    case QueueStatus.EloRangeTooExtreme:
                    case QueueStatus.TeamEloTooExtreme:
                    case QueueStatus.NeedDifferentCoopDifficulties:
                    case QueueStatus.TierTooHighForPlacementPlayers:
                    case QueueStatus.TierBreadthTooExtreme:
                        return "VarietyOfPlayersSkills";
                    case QueueStatus.WaitingForRoleAssassin:
                    case QueueStatus.WaitingOnImbalancedRoleAssassin:
                        return "QueueNeedsMoreOfRoleX";
                    case QueueStatus.WaitingForRoleTank:
                    case QueueStatus.WaitingOnImbalancedRoleTank:
                        return "QueueNeedsMoreOfRoleX";
                    case QueueStatus.WaitingForRoleSupport:
                    case QueueStatus.WaitingOnImbalancedRoleSupport:
                        return "QueueNeedsMoreOfRoleX";
                    case QueueStatus.WaitingOnImbalancedRoleGeneric:
                        return "VarietyOfRoles";
                    case QueueStatus.BlockedByExpertCollisions:
                    case QueueStatus.BlockedByNoobCollisions:
                        return "VarietyOfFreelancers";
                    case QueueStatus.WaitingOnImbalancedGroups:
                    case QueueStatus.WaitingToBreakGroups:
                    case QueueStatus.WaitingOnPerfectGroupComposition:
                        return "VarietyOfGroupSizes";
                    case QueueStatus.AllServersBusy:
                        return "ServerResources";
                    case QueueStatus.NeedDifferentOpponents:
                        return "DifferentOpponents";
                    case QueueStatus.TooManyWillFills:
                        return "TooManyWillFillFreelancers";
                    case QueueStatus.NotEnoughWillFills:
                        return "NotEnoughWillFillFreelancers";
                }

                return "BetterMatchmakingCode";
            }
        }

        
    }
}
