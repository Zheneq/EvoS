using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public abstract class Matchmaker
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Matchmaker));

    protected readonly GameType _gameType;
    protected readonly GameSubType _subType;

    protected Matchmaker(
        GameType gameType,
        GameSubType subType)
    {
        _gameType = gameType;
        _subType = subType;
    }
    
    
    public class MatchmakingGroup
    {
        public long GroupID;
        public DateTime QueueTime;
        public List<QueuePlayerData> Members;
        
        public MatchmakingGroup(long groupId, List<QueuePlayerData> members, DateTime queueTime)
        {
            GroupID = groupId;
            Members = members;
            QueueTime = queueTime;
        }

        public bool Is(GroupInfo groupInfo)
        {
            if (GroupID != groupInfo.GroupId) return false;
            if (Members.Count != groupInfo.Members.Count) return false;
            return Members.All(data => groupInfo.Members.Contains(data.AccountId));
        }
        
        public int Players => Members.Count;
        public int Slots => Members.Select(data => data.NumControlledCharacters).Sum();
    }

    public class Match
    {
        public class Team
        {
            public List<MatchmakingGroup> Groups { get; }
            public List<MatchPlayerData> MatchPlayerDataList { get; }
            public Dictionary<long, MatchPlayerData> MatchPlayerDatas { get; }
            public List<long> AccountIds => MatchPlayerDatas.Values.Select(acc => acc.AccountId).ToList();
            public float Elo { get; }
            public float MinElo => MatchPlayerDatas.Values.Select(data => data.GetElo()).Min();
            public float MaxElo => MatchPlayerDatas.Values.Select(data => data.GetElo()).Max();

            public Team(AccountDao dao, List<MatchmakingGroup> groups)
            {
                Groups = groups;
                MatchPlayerDataList = Groups
                    .SelectMany(g => g.Members)
                    .Select(data =>
                    {
                        var account = dao.GetAccount(data.AccountId);
                        return new MatchPlayerData(
                            account.AccountId,
                            account.Handle,
                            data.EloKey,
                            (EloValues) account.ExperienceComponent.EloValues.Clone(),
                            account.AccountComponent.GetLastCharacters(data.NumControlledCharacters),
                            account.SocialComponent.BlockedAccounts);
                    })
                    .ToList();
                MatchPlayerDatas = MatchPlayerDataList.ToDictionary(acc => acc.AccountId);
                Elo = Matchmaking.Elo.GetTeamElo(MatchPlayerDatas.Values.ToList());
            }

            public override string ToString()
            {
                return $"{string.Join(", ", Groups.Select(g =>
                    '[' + string.Join(", ", g.Members.Select(FormatAccount)) + ']'))} <{{{Elo}}}>";
            }

            private string FormatAccount(QueuePlayerData data)
            {
                return FormatAccount(MatchPlayerDatas[data.AccountId]);
            }

            private static string FormatAccount(MatchPlayerData data)
            {
                return $"{data.Handle} <{data.GetElo():0}|{data.GetEloConfidenceLevel()}>";
            }
        }

        public Team TeamA { get; }
        public Team TeamB { get; }
        public IEnumerable<MatchmakingGroup> Groups => TeamA.Groups.Concat(TeamB.Groups);

        public Match(AccountDao accountDao, List<MatchmakingGroup> teamA, List<MatchmakingGroup> teamB)
        {
            TeamA = new Team(accountDao, teamA);
            TeamB = new Team(accountDao, teamB);
        }

        public override string ToString()
        {
            Team team1;
            Team team2;
            if (TeamA.Elo > TeamB.Elo)
            {
                team1 = TeamA;
                team2 = TeamB;
            }
            else
            {
                team1 = TeamB;
                team2 = TeamA;
            }
            float prediction = Elo.GetPrediction(team1.Elo, team2.Elo);
            return $"{team1} [{prediction * 100:0}%] vs {team2} [{(1 - prediction) * 100:0}%]";
        }
    }

    public class ScoredMatch : IComparable<ScoredMatch>
    {
        public ScoredMatch(Match match, float score, string description)
        {
            Match = match;
            Score = score;
            Description = description;
        }

        public Match Match { get; }
        public float Score { get; }
        public string Description { get; }
        
        public int CompareTo(ScoredMatch other)
        {
            return Score.CompareTo(other.Score);
        }

        public override string ToString()
        {
            return $"{Score} {Match}";
        }

        public string ToDetailedString()
        {
            return $"{Score} {Description} {Match}";
        }
    }
        
    public virtual List<ScoredMatch> GetMatchesRanked(List<MatchmakingGroup> queuedGroups, DateTime now)
    {
        if (queuedGroups.Count == 0)
        {
            return new();
        }

        List<Match> possibleMatches = FindMatches(queuedGroups).ToList();
        if (possibleMatches.Count > 0)
        {
            log.Debug($"Found {possibleMatches.Count} possible matches in " +
                      $"{_gameType}#{_subType.LocalizedName}: " +
                      $"({string.Join(",", queuedGroups.Select(g => g.Players + (g.Players != g.Slots ? $" ({g.Slots} slots)" : "")))})");
            List<Match> filteredMatches = FilterMatches(possibleMatches, now);
            log.Info($"Found {filteredMatches.Count} allowed matches in " +
                     $"{_gameType}#{_subType.LocalizedName} after filtering");
            if (filteredMatches.Count == 0 && possibleMatches.Count > 0)
            {
                if (IgnoreFiltering(queuedGroups, now))
                {
                    log.Info("Ignoring filtering");
                    filteredMatches = possibleMatches;
                }
            }
            if (filteredMatches.Count > 0)
            {
                List<ScoredMatch> matches = RankMatches(filteredMatches, now);
                HashSet<long> playersInQueue = queuedGroups
                    .SelectMany(g => g.Members)
                    .Select(d => d.AccountId)
                    .ToHashSet();
                foreach (ScoredMatch scoredMatch in matches)
                {
                    if (playersInQueue.Count == 0)
                    {
                        break;
                    }
                    foreach (QueuePlayerData data in scoredMatch.Match.Groups.SelectMany(g => g.Members))
                    {
                        if (playersInQueue.Remove(data.AccountId))
                        {
                            log.Debug($"Best match for {data.AccountId}/{LobbyServerUtils.GetUserName(data.AccountId)}: {scoredMatch.ToDetailedString()}");
                        }
                    }
                }
                
                log.Info($"Best match: {matches[0].ToDetailedString()}");
                
                return matches;
            }
        }

        return new();
    }

    protected virtual bool IgnoreFiltering(List<MatchmakingGroup> queuedGroups, DateTime now)
    {
        return false;
    }

    protected virtual IEnumerable<Match> FindMatches(List<MatchmakingGroup> queuedGroups)
    {
        yield break;
    }

    protected virtual List<Match> FilterMatches(IEnumerable<Match> possibleMatches, DateTime now)
    {
        return possibleMatches
            .Where(m => FilterMatch(m, now))
            .ToList();
    }

    protected virtual bool FilterMatch(Match match, DateTime now)
    {
        return true;
    }

    protected virtual List<ScoredMatch> RankMatches(List<Match> matches, DateTime now)
    {
        return matches
            .Select(m => RankMatch(m, now))
            .OrderByDescending(m => m.Score)
            .ToList();
    }

    protected virtual ScoredMatch RankMatch(Match match, DateTime now)
    {
        return new ScoredMatch(match, 0, "no ranking");
    }
}