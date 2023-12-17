using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Group;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public class Matchmaker
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Matchmaker));

    private readonly GameType _gameType;
    private readonly GameSubType _subType;
    private readonly string _eloKey;
    private MatchmakingConfiguration Conf;

    public Matchmaker(GameType gameType, GameSubType subType, string eloKey, MatchmakingConfiguration conf)
    {
        _gameType = gameType;
        _subType = subType;
        _eloKey = eloKey;
        Conf = conf;
    }

    class MatchScratch
    {
        class Team
        {
            private readonly int _capacity;
            
            private readonly List<MatchmakingGroup> _groups = new(5);
            private int _size;

            public Team(int capacity)
            {
                _capacity = capacity;
            }

            public Team(Team other) : this(other._capacity)
            {
                _groups = other._groups.ToList();
                _size = other._size;
            }

            public bool IsFull => _size == _capacity;
            public List<MatchmakingGroup> Groups => _groups;

            public int GetHash()
            {
                return _groups
                    .SelectMany(g => g.Members)
                    .Select(accountId => accountId.GetHashCode())
                    .Aggregate(1, (a, b) => a * b);
            }

            public bool Push(MatchmakingGroup groupInfo)
            {
                if (_capacity <= _size || _capacity - _size >= groupInfo.Players)
                {
                    return false;
                }
                _size += groupInfo.Players;
                _groups.Add(groupInfo);
                return true;
            }

            public bool Pop(out long groupId)
            {
                groupId = -1;
                if (_groups.Count <= 0)
                {
                    return false;
                }
                MatchmakingGroup groupInfo = _groups[^1];
                _size -= groupInfo.Players;
                _groups.RemoveAt(_groups.Count - 1);
                groupId = groupInfo.GroupID;
                return true;
            }
        }
        
        private readonly Team _teamA;
        private readonly Team _teamB;
        private readonly HashSet<long> _usedGroupIds = new(10);

        public MatchScratch(GameSubType subType)
        {
            _teamA = new Team(subType.TeamAPlayers);
            _teamB = new Team(subType.TeamBPlayers);
        }

        private MatchScratch(Team teamA, Team teamB, HashSet<long> usedGroupIds)
        {
            _teamA = teamA;
            _teamB = teamB;
            _usedGroupIds = usedGroupIds;
        }

        public Match ToMatch(string eloKey)
        {
            return new Match(_teamA.Groups, _teamB.Groups, eloKey);
        }

        public long GetHash()
        {
            int a = _teamA.GetHash();
            int b = _teamB.GetHash();
            return (long)Math.Min(a, b) << 32 | (uint)Math.Max(a, b);
        }

        public bool Push(MatchmakingGroup groupInfo)
        {
            if (_usedGroupIds.Contains(groupInfo.GroupID))
            {
                return false;
            }
            if (_teamA.Push(groupInfo) || _teamB.Push(groupInfo))
            {
                _usedGroupIds.Add(groupInfo.GroupID);
                return true;
            }
            return false;
        }

        public void Pop()
        {
            if (_teamA.Pop(out long groupId) || _teamB.Pop(out groupId))
            {
                _usedGroupIds.Remove(groupId);
                return;
            }
            
            throw new Exception("Matchmaking failure");
        }

        public bool IsMatch()
        {
            return _teamA.IsFull && _teamB.IsFull;
        }
    }
    
    
    public class MatchmakingGroup
    {
        public long GroupID;
        public int Players;
        public Team Team;
        public DateTime QueueTime;

        public MatchmakingGroup(long groupID, DateTime queueTime = default)
        {
            GroupID = groupID;
            Players = GroupManager.GetGroup(groupID).Members.Count;
            Team = Team.Invalid;
            QueueTime = queueTime;
        }

        public List<long> Members => GroupManager.GetGroup(GroupID).Members;
    }

    public class Match
    {
        public class Team
        {
            private readonly string _eloKey;
            public List<MatchmakingGroup> Groups { get; }
            public List<PersistedAccountData> Accounts { get; }
            public List<long> AccountIds => Accounts.Select(acc => acc.AccountId).ToList();
            public float Elo { get; }
            public float MinElo => Accounts.Select(GetElo).Min();
            public float MaxElo => Accounts.Select(GetElo).Max();

            public Team(List<MatchmakingGroup> groups, string eloKey)
            {
                _eloKey = eloKey;
                AccountDao dao = DB.Get().AccountDao;
                Groups = groups;
                Accounts = Groups
                    .SelectMany(g => g.Members)
                    .Select(accountId => dao.GetAccount(accountId))
                    .ToList();
                Elo = Accounts.Select(GetElo).Sum() / Accounts.Count;
            }

            private float GetElo(PersistedAccountData acc)
            {
                acc.ExperienceComponent.EloValues.GetElo(_eloKey, out float elo, out _);
                return elo;
            }
        }

        public Team TeamA { get; }
        public Team TeamB { get; }
        public IEnumerable<MatchmakingGroup> Groups => TeamA.Groups.Concat(TeamB.Groups);

        public Match(List<MatchmakingGroup> teamA, List<MatchmakingGroup> teamB, string eloKey)
        {
            TeamA = new Team(teamA, eloKey);
            TeamB = new Team(teamB, eloKey);
        }
    }
        
    public List<Match> GetMatchesRanked(List<MatchmakingGroup> queuedGroups)
    {
        MatchScratch matchScratch = new MatchScratch(_subType);
        Dictionary<long, Match> possibleMatches = new Dictionary<long, Match>();
        FindMatches(matchScratch, queuedGroups, possibleMatches, _eloKey); // TODO save possible matches between runs, update it iteratively
        log.Info($"Found {possibleMatches.Count} possible matches in " +
                 $"{_gameType}#{_subType.LocalizedName}: " +
                 $"({string.Join(",", queuedGroups.Select(g => g.Players.ToString()))}");
        if (possibleMatches.Count > 0)
        {
            List<Match> matches = RankMatches(FilterMatches(possibleMatches));
            return matches;
        }

        return new();
    }
    
    private void FindMatches(
        MatchScratch matchScratch,
        List<MatchmakingGroup> queuedGroups,
        Dictionary<long, Match> possibleMatches,
        string eloKey)
    {
        foreach (MatchmakingGroup groupInfo in queuedGroups)
        {
            if (matchScratch.Push(groupInfo))
            {
                if (matchScratch.IsMatch())
                {
                    long hash = matchScratch.GetHash();
                    if (!possibleMatches.ContainsKey(hash))
                    {
                        possibleMatches.Add(hash, matchScratch.ToMatch(eloKey));
                    }
                }
                else
                {
                    FindMatches(matchScratch, queuedGroups, possibleMatches, eloKey);
                }
                matchScratch.Pop();
            }
        }
    }

    private List<Match> FilterMatches(Dictionary<long, Match> possibleMatches)
    {
        return possibleMatches.Values
            .Where(FilterMatch)
            .ToList();
    }

    private bool FilterMatch(Match match)
    {
        DateTime now = DateTime.UtcNow;
        int cutoff = Convert.ToInt32(MathF.Floor(2.0f * match.Groups.Count() / 3)); // don't want to keep the first ones to queue waiting for too long
        double waitingTime = match.Groups
            .Select(g => (now - g.QueueTime).TotalSeconds)
            .Order()
            .TakeLast(cutoff)
            .Average();
        int maxEloDiff = Conf.MaxTeamEloDifferenceStart +
                         Convert.ToInt32((Conf.MaxTeamEloDifference - Conf.MaxTeamEloDifferenceStart)
                                         * Math.Clamp(waitingTime / Conf.MaxTeamEloDifferenceWaitTime.TotalSeconds, 0, 1));
        
        return Math.Abs(match.TeamA.Elo - match.TeamB.Elo) <= maxEloDiff;
    }

    private List<Match> RankMatches(List<Match> matches)
    {
        return matches
            .OrderByDescending(RankMatch)
            .ToList();
    }

    private float RankMatch(Match match)
    {
        float teamEloDifferenceFactor = 1 - Cap(Math.Abs(match.TeamA.Elo - match.TeamB.Elo) / Conf.MaxTeamEloDifference);
        float teammateEloDifferenceAFactor = 1 - Cap((match.TeamA.MaxElo - match.TeamA.MinElo) / Conf.TeammateEloDifferenceWeightCap);
        float teammateEloDifferenceBFactor = 1 - Cap((match.TeamB.MaxElo - match.TeamB.MinElo) / Conf.TeammateEloDifferenceWeightCap);
        DateTime now = DateTime.UtcNow;
        double waitTime = match.Groups.Select(g => (now - g.QueueTime).TotalSeconds).Max();
        float waitTimeFactor = Cap((float)(waitTime / Conf.WaitingTimeWeightCap.TotalSeconds));
        
        // TODO team composition factor
        // TODO recently canceled matches factor
        // TODO mutual blocks factor
        // TODO non-linearity?

        return
            teamEloDifferenceFactor * Conf.TeamEloDifferenceWeight
            + teammateEloDifferenceAFactor * Conf.TeammateEloDifferenceWeight * 0.5f
            + teammateEloDifferenceBFactor * Conf.TeammateEloDifferenceWeight * 0.5f
            + waitTimeFactor * Conf.WaitingTimeWeight;
    }

    private static float Cap(float factor)
    {
        return Math.Min(factor, 1);
    }

    private static readonly object EloLock = new();
    public void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary)
    {
        if (gameSummary.GameResult != GameResult.TeamAWon && gameSummary.GameResult != GameResult.TeamBWon)
        {
            return;
        }
        
        AccountDao accountDao = DB.Get().AccountDao;
        List<PersistedAccountData> teamA = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamA())
            .Select(pgs => accountDao.GetAccount(pgs.AccountId))
            .ToList();
        List<PersistedAccountData> teamB = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamB())
            .Select(pgs => accountDao.GetAccount(pgs.AccountId))
            .ToList();
        
        lock (EloLock)
        {
            foreach (PersistedAccountData acc in teamA.Concat(teamB))
            {
                UpdateConfidence(acc);
            }
            float eloChange = GetEloChange(teamA, teamB, gameSummary.GameResult == GameResult.TeamAWon ? 1 : 0);
            AwardEloTeam(teamA, eloChange);
            AwardEloTeam(teamB, -eloChange);
        }
    }

    private static readonly List<TimeSpan> ConfidenceRetention = new()
    {
        TimeSpan.FromDays(30),
        TimeSpan.FromDays(90),
        TimeSpan.FromDays(180),
    };
    private static readonly List<int> ConfidenceUpgrade = new()
    {
        10,
        25,
    };

    private void UpdateConfidence(PersistedAccountData player)
    {
        List<PersistedCharacterMatchData> matches = DB.Get().MatchHistoryDao.Find(player.AccountId);
        PersistedCharacterMatchData lastMatch = matches
            .FirstOrDefault(m => m.MatchComponent.GameType == _gameType);

        int confidenceLevelDelta = -100;
        if (lastMatch is not null)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan timeSinceLastMatch = now - lastMatch.MatchComponent.MatchTime;
            for (int i = ConfidenceRetention.Count - 1; i >= 0; i--)
            {
                if (timeSinceLastMatch > ConfidenceRetention[i])
                {
                    confidenceLevelDelta = -i;
                    break;
                }
            }

            int eloConfidenceLevel = GetEloConfidenceLevel(player);
            if (eloConfidenceLevel < ConfidenceUpgrade.Count)
            {
                int num = matches
                    .Count(m => m.MatchComponent.GameType == _gameType
                                && now - lastMatch.MatchComponent.MatchTime < ConfidenceRetention[0]);
                if (num >= ConfidenceUpgrade[eloConfidenceLevel])
                {
                    confidenceLevelDelta = 1;
                }
            }
        }
        
        player.ExperienceComponent.EloValues.ApplyDelta(_eloKey, 0, confidenceLevelDelta);
    }

    private float GetTeamElo(List<PersistedAccountData> team)
    {
        return team.Select(GetElo).Sum() / team.Count;
    }

    private const float ELO_BASE_POT = 64.0f;
    private float GetEloChange(List<PersistedAccountData> teamA, List<PersistedAccountData> teamB, int result)
    {

        float k = ELO_BASE_POT * (teamA.Select(GetEloConfidenceFactor).Sum() / (2 * teamA.Count) +
                                  teamB.Select(GetEloConfidenceFactor).Sum() / (2 * teamB.Count));
        return k * (result - GetPrediction(teamA, teamB));
    }

    private float GetPrediction(List<PersistedAccountData> teamA, List<PersistedAccountData> teamB)
    {
        return GetPrediction(GetTeamElo(teamA), GetTeamElo(teamB));
    }

    private static float GetPrediction(float teamAElo, float teamBElo)
    {
        return 1.0f / (1 + MathF.Pow(10, (teamBElo - teamAElo) / 400.0f));
    }
    
    private float GetElo(PersistedAccountData acc)
    {
        acc.ExperienceComponent.EloValues.GetElo(_eloKey, out float elo, out _);
        return elo;
    }
    
    private static readonly List<float> EloConfidenceFactor = new() { 1.0f, 0.75f, 0.5f };
    private float GetEloConfidenceFactor(PersistedAccountData acc)
    {
        int cf = GetEloConfidenceLevel(acc);
        return EloConfidenceFactor[Math.Clamp(cf, 0, EloConfidenceFactor.Count-1)];
    }

    private int GetEloConfidenceLevel(PersistedAccountData acc)
    {
        acc.ExperienceComponent.EloValues.GetElo(_eloKey, out _, out int cf);
        return Math.Max(cf, 0);
    }

    private void AwardElo(PersistedAccountData acc, float delta)
    {
        acc.ExperienceComponent.EloValues.ApplyDelta(_eloKey, delta, 0);
    }

    private void AwardEloTeam(List<PersistedAccountData> team, float eloDelta)
    {
        float avgConf = team.Select(GetEloConfidenceFactor).Sum() / team.Count;
        foreach (PersistedAccountData acc in team)
        {
            AwardElo(acc, eloDelta * GetEloConfidenceFactor(acc) / avgConf);
        }
    }
}