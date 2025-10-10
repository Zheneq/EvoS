using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Group;
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
        public List<long> Members;
        
        public MatchmakingGroup(long groupId, List<long> members, DateTime queueTime)
        {
            GroupID = groupId;
            Members = members;
            QueueTime = queueTime;
        }

        public MatchmakingGroup(GroupInfo groupInfo, DateTime queueTime = default)
            : this(groupInfo.GroupId, groupInfo.Members.ToList(), queueTime)
        {
        }

        public bool Is(GroupInfo groupInfo)
        {
            if (GroupID != groupInfo.GroupId) return false;
            if (Members.Count != groupInfo.Members.Count) return false;
            return Members.All(accId => groupInfo.Members.Contains(accId));
        }
        
        public int Players => Members.Count;
    }

    public class Match
    {
        public class Team
        {
            private readonly string _eloKey;
            public List<MatchmakingGroup> Groups { get; }
            public Dictionary<long, PersistedAccountData> Accounts { get; }
            public List<long> AccountIds => Accounts.Values.Select(acc => acc.AccountId).ToList();
            public float Elo { get; }
            public float MinElo => Accounts.Values.Select(GetElo).Min();
            public float MaxElo => Accounts.Values.Select(GetElo).Max();

            public Team(AccountDao dao, List<MatchmakingGroup> groups, string eloKey)
            {
                _eloKey = eloKey;
                Groups = groups;
                Accounts = Groups
                    .SelectMany(g => g.Members)
                    .Select(dao.GetAccount)
                    .ToDictionary(acc => acc.AccountId);
                Elo = Accounts.Values.Select(GetElo).Sum() / Accounts.Count;
            }

            private float GetElo(PersistedAccountData acc)
            {
                acc.ExperienceComponent.EloValues.GetElo(_eloKey, out float elo, out _);
                return elo;
            }

            private int GetEloConfidenceLevel(PersistedAccountData acc)
            {
                acc.ExperienceComponent.EloValues.GetElo(_eloKey, out _, out int eloConfLevel);
                return eloConfLevel;
            }

            public override string ToString()
            {
                return $"{string.Join(", ", Groups.Select(g =>
                    '[' + string.Join(", ", g.Members.Select(FormatAccount)) + ']'))} <{{{Elo}}}>";
            }

            private string FormatAccount(long accId)
            {
                if (!Accounts.TryGetValue(accId, out var acc))
                {
                    return "#{accId}";
                }

                return $"{acc.Handle} <{GetElo(acc):0}|{GetEloConfidenceLevel(acc)}>";
            }
        }

        public Team TeamA { get; }
        public Team TeamB { get; }
        public IEnumerable<MatchmakingGroup> Groups => TeamA.Groups.Concat(TeamB.Groups);

        public Match(AccountDao accountDao, List<MatchmakingGroup> teamA, List<MatchmakingGroup> teamB, string eloKey)
        {
            TeamA = new Team(accountDao, teamA, eloKey);
            TeamB = new Team(accountDao, teamB, eloKey);
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
        public ScoredMatch(Match match, float score)
        {
            Match = match;
            Score = score;
        }

        public Match Match { get; }
        public float Score { get; }
        
        public int CompareTo(ScoredMatch other)
        {
            return Score.CompareTo(other.Score);
        }

        public override string ToString()
        {
            return $"{Score} {Match}";
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
                      $"({string.Join(",", queuedGroups.Select(g => g.Players.ToString()))})");
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
                log.Info($"Best match: {matches[0]}");
                RankMatch(matches[0].Match, now, true);
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
            .Select(m => new ScoredMatch(m, RankMatch(m, now)))
            .OrderByDescending(m => m.Score)
            .ToList();
    }

    protected virtual float RankMatch(Match match, DateTime now, bool infoLog = false)
    {
        return 0;
    }
}