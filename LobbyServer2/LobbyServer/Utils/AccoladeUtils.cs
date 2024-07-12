using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Utils
{
    public static class AccoladeUtils
    {

        public static List<int> AwardBadge(LobbyGameSummary gameSummary, GameBalanceVars.GameResultBadge badge)
        {
            if (badge.ComparisonGroup == GameBalanceVars.GameResultBadge.ComparisonType.Freelancer)
            {
                // we have no stats atm
                return new List<int>();
            }

            List<PlayerGameSummary> candidates = gameSummary.PlayerGameSummaryList
                .Where(badge.DoStatsMeetMinimumRequirementsForBadge)
                .Where(player => player.GetStat(badge.BadgePointCalcType) > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                return new List<int>();
            }

            if (badge.ComparisonGroup == GameBalanceVars.GameResultBadge.ComparisonType.None)
            {
                return candidates.Select(player => player.ActorIndex).ToList();
            }

            if (badge.ComparisonGroup == GameBalanceVars.GameResultBadge.ComparisonType.Game)
            {
                float max = candidates.Select(player => player.GetStat(badge.BadgePointCalcType)).Max() ?? 0;
                return candidates
                    .Where(player => player.GetStat(badge.BadgePointCalcType) == max)
                    .Select(player => player.ActorIndex)
                    .ToList();
            }

            // again, we have no global stats, so we are using game stats
            Dictionary<int, int> percentiles = GetPercentiles(gameSummary, badge.BadgePointCalcType);
            List<int> result = new List<int>();
            foreach ((int actorIndex, int percentile) in percentiles)
            {
                if (percentile > badge.GlobalPercentileToObtain)
                {
                    result.Add(actorIndex);
                }
            }

            return result;
        }

        public static Dictionary<int, List<BadgeInfo>> AwardBadgeGroup(
            LobbyGameSummary gameSummary,
            GameResultBadgeData.ConsolidatedBadgeGroup badgeGroup)
        {
            Dictionary<int, List<BadgeInfo>> badgeInfos = new Dictionary<int, List<BadgeInfo>>();

            foreach (int badgeId in badgeGroup.BadgeIDs)
            {
                GameBalanceVars.GameResultBadge badge = AccoladeBadges.GameResultBadges[badgeId];
                List<int> actorIndices = AwardBadge(gameSummary, badge);
                foreach (int actorIndex in actorIndices)
                {
                    if (!badgeInfos.ContainsKey(actorIndex))
                    {
                        badgeInfos[actorIndex] = new List<BadgeInfo>();
                    }
                    badgeInfos[actorIndex].Add(new BadgeInfo { BadgeId = badge.GetID() });
                }
            }

            foreach (List<BadgeInfo> badges in badgeInfos.Values)
            {
                badges.Sort((a, b) =>
                    AccoladeBadges.GameResultBadges[b.BadgeId].Quality
                    - AccoladeBadges.GameResultBadges[a.BadgeId].Quality);
                for (int i = badges.Count - 1; i > 0; i--)
                {
                    if (!AccoladeBadges.GameResultBadges[badges[i].BadgeId]
                            .CouldRecieveBothBadgesInOneGame(AccoladeBadges.GameResultBadges[badges[0].BadgeId]))
                    {
                        badges.RemoveAt(i);
                    }
                }
            }

            return badgeInfos;
        }

        public static Dictionary<int, List<BadgeInfo>> AwardBadges(LobbyGameSummary gameSummary)
        {
            Dictionary<int, List<BadgeInfo>> badgeInfos = gameSummary.PlayerGameSummaryList
                .ToDictionary(player => player.ActorIndex, x => new List<BadgeInfo>());

            HashSet<int> processedBadges = new HashSet<int>();
            foreach (GameResultBadgeData.ConsolidatedBadgeGroup badgeGroup in AccoladeBadges.BadgeGroups)
            {
                Dictionary<int, List<BadgeInfo>> groupBadgeInfos = AwardBadgeGroup(gameSummary, badgeGroup);
                foreach ((int actorIndex, List<BadgeInfo> playerBadges) in groupBadgeInfos)
                {
                    badgeInfos[actorIndex].AddRange(playerBadges);
                }
                foreach (int badgeID in badgeGroup.BadgeIDs)
                {
                    processedBadges.Add(badgeID);
                }
            }

            foreach (GameBalanceVars.GameResultBadge badge in AccoladeBadges.GameResultBadges.Values)
            {
                if (processedBadges.Contains(badge.GetID())) continue;
                List<int> groupBadgeInfos = AwardBadge(gameSummary, badge);
                foreach (int actorIndex in groupBadgeInfos)
                {
                    badgeInfos[actorIndex].Add(new BadgeInfo { BadgeId = badge.GetID() });
                }
                processedBadges.Add(badge.GetID());
            }

            return badgeInfos;
        }

        public static Dictionary<TopParticipantSlot, int> GetAccolades(Dictionary<int, List<BadgeInfo>> badgeInfos)
        {
            Dictionary<TopParticipantSlot, Dictionary<int, float>> slot2ActorIndex2Score =
                new Dictionary<TopParticipantSlot, Dictionary<int, float>>
                {
                    { TopParticipantSlot.Deadliest, new Dictionary<int, float>() },
                    { TopParticipantSlot.Supportiest, new Dictionary<int, float>() },
                    { TopParticipantSlot.Tankiest, new Dictionary<int, float>() },
                    { TopParticipantSlot.MostDecorated, new Dictionary<int, float>() },
                };

            foreach ((int actorIndex, List<BadgeInfo> badges) in badgeInfos)
            {
                foreach (BadgeInfo badgeInfo in badges)
                {
                    GameBalanceVars.GameResultBadge badge = AccoladeBadges.GameResultBadges[badgeInfo.BadgeId];
                    foreach ((TopParticipantSlot slot, Dictionary<int, float> playerScores) in slot2ActorIndex2Score)
                    {
                        if (badge.Role.IsFor(slot))
                        {
                            playerScores.TryAdd(actorIndex, 0);
                            playerScores[actorIndex] += badge.GetQualityWorth();
                        }
                    }
                }
            }

            return slot2ActorIndex2Score.ToDictionary(
                e => e.Key,
                e => e.Value
                    .OrderByDescending(p2s => p2s.Value)
                    .FirstOrDefault()
                    .Key);
        }

        public static List<BadgeAndParticipantInfo> ProcessGameSummary(LobbyGameSummary gameSummary)
        {
            List<BadgeAndParticipantInfo> result = new List<BadgeAndParticipantInfo>();
            if (gameSummary.GameResult != GameResult.TeamAWon
                && gameSummary.GameResult != GameResult.TeamBWon)
            {
                return result;
            }
            Dictionary<int, List<BadgeInfo>> badgeInfos = AwardBadges(gameSummary);
            Dictionary<TopParticipantSlot, int> accolades = GetAccolades(badgeInfos);

            foreach (PlayerGameSummary player in gameSummary.PlayerGameSummaryList)
            {
                List<TopParticipantSlot> topParticipationEarned = accolades
                    .Where(e => e.Value == player.ActorIndex)
                    .Select(e => e.Key)
                    .ToList();

                result.Add(new BadgeAndParticipantInfo
                {
                    PlayerId = player.PlayerId,
                    TeamId = player.IsInTeamA() ? Team.TeamA : Team.TeamB,
                    TeamSlot = player.TeamSlot,
                    BadgesEarned = badgeInfos[player.ActorIndex],
                    TopParticipationEarned = topParticipationEarned,
                    GlobalPercentiles = new Dictionary<StatDisplaySettings.StatType, PercentileInfo>(),
                    FreelancerSpecificPercentiles = new Dictionary<int, PercentileInfo>(),
                    FreelancerPlayed = player.CharacterPlayed,
                    ActorIndex = player.ActorIndex,
                });
            }

            return result;
        }

        public static bool IsFor(this GameBalanceVars.GameResultBadge.BadgeRole role, TopParticipantSlot slot)
        {
            switch (slot)
            {
                case TopParticipantSlot.MostDecorated:
                    return true;
                case TopParticipantSlot.Deadliest:
                    return role == GameBalanceVars.GameResultBadge.BadgeRole.Firepower;
                case TopParticipantSlot.Supportiest:
                    return role == GameBalanceVars.GameResultBadge.BadgeRole.Support;
                case TopParticipantSlot.Tankiest:
                    return role == GameBalanceVars.GameResultBadge.BadgeRole.Frontliner;
                default:
                    return false;
            }
        }


        public static Dictionary<int, int> GetPercentiles(LobbyGameSummary gameSummary, StatDisplaySettings.StatType statType)
        {
            Dictionary<int, float> values = gameSummary.PlayerGameSummaryList
                .ToDictionary(player => player.ActorIndex, player => player.GetStat(statType) ?? 0);

            Dictionary<int, int> percentiles = new Dictionary<int, int>();
            foreach ((int actorIndex, float value) in values)
            {
                percentiles[actorIndex] = 100 * values.Values.Count(x => x < value) / values.Count;
            }

            return percentiles;
        }
    }
}