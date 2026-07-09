using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Matchmaking;

public abstract class MatchmakerBase: Matchmaker
{
    protected readonly AccountDao _accountDao;
    
    protected MatchmakerBase(
        AccountDao accountDao,
        GameType gameType,
        GameSubType subType)
        : base(gameType, subType)
    {
        _accountDao = accountDao;
    }

    protected override IEnumerable<Match> FindMatches(List<MatchmakingGroup> queuedGroups)
    {
        return FindMatches(new MatchScratch(_subType), queuedGroups, new HashSet<long>());
    }
    
    private IEnumerable<Match> FindMatches(
        MatchScratch matchScratch,
        List<MatchmakingGroup> queuedGroups,
        HashSet<long> processed)
    {
        foreach (MatchmakingGroup groupInfo in queuedGroups)
        {
            if (matchScratch.Push(groupInfo))
            {
                if (matchScratch.IsMatch())
                {
                    long hash = matchScratch.GetHash();
                    if (processed.Add(hash))
                    {
                        yield return matchScratch.ToMatch(_accountDao);
                    }
                }
                else
                {
                    foreach (Match match in FindMatches(matchScratch, queuedGroups, processed))
                    {
                        yield return match;
                    }
                }
                matchScratch.Pop();
            }
        }
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
                    .Select(data => data.AccountId)
                    .Order()
                    .Select(accountId => accountId.GetHashCode())
                    .Aggregate(17, (a, b) => a * 31 + b);
            }

            public bool Push(MatchmakingGroup groupInfo)
            {
                if (_capacity <= _size || _capacity - _size < groupInfo.Slots)
                {
                    return false;
                }
                _size += groupInfo.Slots;
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
                _size -= groupInfo.Slots;
                _groups.RemoveAt(_groups.Count - 1);
                groupId = groupInfo.GroupID;
                return true;
            }

            public override string ToString()
            {
                return
                    $"groups {string.Join(",", _groups.Select(g => g.GroupID))} " +
                    $"<{string.Join(",", _groups.SelectMany(g => g.Members))}>";
            }
        }
        
        private readonly Team _teamA;
        private readonly Team _teamB;
        private readonly HashSet<long> _usedGroupIds = new(10);

        public MatchScratch(GameSubType subType)
        {
            _teamA = new Team(subType.TeamAHumanPlayers);
            _teamB = new Team(subType.TeamBHumanPlayers);
        }

        private MatchScratch(Team teamA, Team teamB, HashSet<long> usedGroupIds)
        {
            _teamA = teamA;
            _teamB = teamB;
            _usedGroupIds = usedGroupIds;
        }

        public Match ToMatch(AccountDao accountDao)
        {
            return new Match(accountDao, _teamA.Groups.ToList(), _teamB.Groups.ToList());
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
            if (_teamB.Pop(out long groupId) || _teamA.Pop(out groupId))
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

        public override string ToString()
        {
            return $"{_teamA} vs {_teamB}";
        }
    }
}