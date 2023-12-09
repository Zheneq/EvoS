using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;
using WebSocketSharp;

namespace CentralServer.LobbyServer.Matchmaking
{
    public class MatchmakingQueue
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingQueue));

        private readonly string EloKey;
        
        Dictionary<string, LobbyGameInfo> Games = new Dictionary<string, LobbyGameInfo>();
        private readonly ConcurrentDictionary<long, DateTime> QueuedGroups = new ConcurrentDictionary<long, DateTime>();
        public LobbyMatchmakingQueueInfo MatchmakingQueueInfo;
        public GameType GameType => MatchmakingQueueInfo.GameType;

        public List<long> GetQueuedGroups()
        {
            return QueuedGroups.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }
        public List<long> GetQueuedPlayers()
        {
            return QueuedGroups.Keys.SelectMany(g => GroupManager.GetGroup(g).Members).ToList();
        }
        
        private static int GameID = 0;

        public MatchmakingQueue(GameType gameType)
        {
            EloKey = gameType.ToString();
            MatchmakingQueueInfo = new LobbyMatchmakingQueueInfo()
            {
                QueueStatus = QueueStatus.Idle,
                QueuedPlayers = 0,
                ShowQueueSize = true,
                AverageWaitTime = TimeSpan.FromSeconds(60),
                GameConfig = new LobbyGameConfig()
                {
                    GameType = gameType,
                }
            };
            ReloadConfig();
        }

        private void ReloadConfig()
        {
            GameType gameType = MatchmakingQueueInfo.GameConfig.GameType;
            MatchmakingQueueInfo.GameConfig.SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes;
        }

        public void UpdateSettings()
        {
            ReloadConfig();
            Update();
        }

        public LobbyMatchmakingQueueInfo AddGroup(long groupId, out bool added)
        {
            added = QueuedGroups.TryAdd(groupId, DateTime.UtcNow);
            MatchmakingQueueInfo.QueuedPlayers = GetPlayerCount();
            MatchmakingQueueInfo.AverageWaitTime = TimeSpan.FromSeconds(0);
            MatchmakingQueueInfo.QueueStatus = ServerManager.IsAnyServerAvailable() ? QueueStatus.WaitingForHumans : QueueStatus.AllServersBusy;
            log.Info($"Added group {groupId} to {GameType} queue");
            log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");

            return MatchmakingQueueInfo;
        }

        public bool RemoveGroup(long groupId)
        {
            bool removed = QueuedGroups.TryRemove(groupId, out _);
            if (removed)
            {
                log.Info($"Removed group {groupId} from {GameType} queue");
                log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");
            }
            return removed;
        }

        public bool IsQueued(long groupId)
        {
            return QueuedGroups.ContainsKey(groupId);
        }

        public int GetPlayerCount()
        {
            return QueuedGroups.Keys
                .Select(GroupManager.GetGroup)
                .Sum(group => group?.Members.Count ?? 0);
        }

        public void Update()
        {
            log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");

            // TODO UpdateSettings when file changes (and only then)
            ReloadConfig();

            if (MatchmakingManager.Enabled)
            {
                TryMatch();
            }
        }

        class MatchScratch
        {
            class Team
            {
                private readonly int _capacity;
                
                private readonly List<MatchmakingGroupInfo> _groups = new(5);
                private int _size = 0;

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
                public List<MatchmakingGroupInfo> Groups => _groups;

                public int GetHash()
                {
                    return _groups
                        .SelectMany(g => g.Members)
                        .Select(accountId => accountId.GetHashCode())
                        .Aggregate(1, (a, b) => a * b);
                }

                public bool Push(MatchmakingGroupInfo groupInfo)
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
                    MatchmakingGroupInfo groupInfo = _groups[^1];
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

            public bool Push(long groupId)
            {
                if (_usedGroupIds.Contains(groupId))
                {
                    return false;
                }
                MatchmakingGroupInfo groupInfo = new MatchmakingGroupInfo(groupId);
                if (_teamA.Push(groupInfo) || _teamB.Push(groupInfo))
                {
                    _usedGroupIds.Add(groupId);
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

        class Match
        {
            class Team
            {
                private readonly List<MatchmakingGroupInfo> _groups;
                private readonly List<PersistedAccountData> _accounts;
                private readonly float _elo;

                public Team(List<MatchmakingGroupInfo> groups, string eloKey)
                {
                    AccountDao dao = DB.Get().AccountDao;
                    _groups = groups;
                    _accounts = _groups
                        .SelectMany(g => g.Members)
                        .Select(accountId => dao.GetAccount(accountId))
                        .ToList();
                    _elo = _accounts.Select(acc =>
                    {
                        acc.ExperienceComponent.EloValues.GetElo(eloKey, out float elo, out _);
                        return elo;
                    }).Sum() / _accounts.Count;
                }
            }

            private readonly Team _teamA;
            private readonly Team _teamB;

            public Match(List<MatchmakingGroupInfo> teamA, List<MatchmakingGroupInfo> teamB, string eloKey)
            {
                _teamA = new Team(teamA, eloKey);
                _teamB = new Team(teamB, eloKey);
            }
        }

        private void TryMatch2()
        {
            foreach (GameSubType subType in MatchmakingQueueInfo.GameConfig.SubTypes)
            {
                List<MatchmakingGroupInfo> groups = new List<MatchmakingGroupInfo>();

                lock (GroupManager.Lock)
                {
                    List<long> queuedGroups = GetQueuedGroups();

                    MatchScratch matchScratch = new MatchScratch(subType);
                    Dictionary<long, Match> possibleMatches = new Dictionary<long, Match>();
                    FindMatches(matchScratch, queuedGroups, EloKey, possibleMatches);
                    
                }
            }
        }

        private static void FindMatches(
            MatchScratch matchScratch,
            List<long> queuedGroups,
            string eloKey,
            Dictionary<long, Match> possibleMatches)
        {
            foreach (long groupId in queuedGroups)
            {
                if (matchScratch.Push(groupId))
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
                        FindMatches(matchScratch, queuedGroups, eloKey, possibleMatches);
                    }
                    matchScratch.Pop();
                }
            }
        }

        private void TryMatch()
        {
            foreach (GameSubType subType in MatchmakingQueueInfo.GameConfig.SubTypes)
            {
                List<MatchmakingGroupInfo> groups = new List<MatchmakingGroupInfo>();

                lock (GroupManager.Lock)
                {
                    bool success = false;
                    foreach (long groupId in GetQueuedGroups())
                    {
                        // Add new groups secuentially
                        MatchmakingGroupInfo currentGroup = new MatchmakingGroupInfo(groupId);
                        groups.Add(currentGroup);
                        success = TryFormTeams(subType, ref groups);
                        if (success) break;
                    }

                    if (success)
                    {
                        List<GroupInfo> teamA = new List<GroupInfo>();
                        List<GroupInfo> teamB = new List<GroupInfo>();

                        foreach (MatchmakingGroupInfo group in groups)
                        {
                            if (group.Team == Team.TeamA)
                            {
                                teamA.Add(GroupManager.GetGroup(group.GroupID));
                            }
                            else if (group.Team == Team.TeamB)
                            {
                                teamB.Add(GroupManager.GetGroup(group.GroupID));
                            }
                        }

                        if (!CheckGameServerAvailable())
                        {
                            log.Warn("No available game server to start a match");
                            return;
                        }
                        foreach (GroupInfo group in teamA)
                        {
                            RemoveGroup(group.GroupId);
                        }
                        foreach (GroupInfo group in teamB)
                        {
                            RemoveGroup(group.GroupId);
                        }
                        _ = MatchmakingManager.StartGameAsync(
                            teamA.SelectMany(group => group.Members).ToList(), 
                            teamB.SelectMany(group => group.Members).ToList(), 
                            GameType,
                            MatchmakingQueueInfo.GameConfig.SubTypes[0]);
                    }
                }
            }
        }

        /// <summary>
        /// Tries to form a team with the list of groups specified. The teams are not balanced at all. it just looks for group size
        /// </summary>
        /// <param name="subType">GameSubtype of the game we are trying to make</param>
        /// <param name="matchmakingGroups">available groups to form a team</param>
        /// <returns>true if a team was formed, otherwise returns false</returns>
        private static bool TryFormTeams(GameSubType subType, ref List<MatchmakingGroupInfo> matchmakingGroups)
        {
            bool success = false;
            int teamANum = GetPlayersInTeam(Team.TeamA, matchmakingGroups);
            int teamBNum = GetPlayersInTeam(Team.TeamB, matchmakingGroups);

            // Full team? Success!
            if (teamANum == subType.TeamAPlayers && teamBNum == subType.TeamBPlayers) return true;

            foreach (MatchmakingGroupInfo group in matchmakingGroups)
            {
                // Team Invalid is used as "no team asigned"
                if (group.Team != Team.Invalid) continue;

                // Try to place the team either in Team A or B
                if (teamANum + group.Players <= subType.TeamAPlayers)
                {
                    group.Team = Team.TeamA;
                }
                else if (teamBNum + group.Players <= subType.TeamBPlayers)
                {
                    group.Team = Team.TeamB;
                }

                // If a team was assigned for this group, we try recursion to assign a new group
                if(group.Team != Team.Invalid)
                {
                    success = TryFormTeams(subType, ref matchmakingGroups);
                    if (success) return true;

                    // If we couln't form a match, this team is assigned as invalid for next iteration
                    group.Team = Team.Invalid;
                }
            }
            return success;
        }

        /// <summary>
        /// Counts how many players are in the groups list that are assigned to the specified team
        /// </summary>
        /// <param name="team"></param>
        /// <param name="groups"></param>
        /// <returns>number of player in team</returns>
        private static int GetPlayersInTeam(Team team, List<MatchmakingGroupInfo> groups)
        {
            int count = 0;
            foreach (MatchmakingGroupInfo groupInfo in groups)
            {
                if (groupInfo.Team == team)
                {
                    count += groupInfo.Players;
                }
            }

            return count;
        }

        public bool CheckGameServerAvailable()
        {
            if (ServerManager.IsAnyServerAvailable()) return true;

            // If there is no game server already connected, we check if we can launch one
            string gameServer = EvosConfiguration.GetGameServerExecutable();
            if (gameServer.IsNullOrEmpty()) return false;
            
            // TODO: this will start a new game server every time the queue is run, which can cause multiple server that aren't needed to start
            using (var process = new Process())
            {
                try
                {
                    process.StartInfo = new ProcessStartInfo(gameServer, EvosConfiguration.GetGameServerExecutableArgs());
                    process.Start();
                }
                catch(Exception e)
                {
                    log.Error("Failed to start a game server", e);
                }   
            }
            return false;
        }

        public LobbyGameInfo CreateGameInfo()
        {
            GameTypeAvailability gameAvailability = GameModeManager.GetGameTypeAvailabilities()[GameType];

            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = gameAvailability.TeamAPlayers + gameAvailability.TeamBPlayers,
                AcceptTimeout = GameType == GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                ActiveHumanPlayers = 0,
                ActivePlayers = 0,
                ActiveSpectators = 0,
                CreateTimestamp = DateTime.UtcNow.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.AllowDuplicateCharacters & GameOptionFlag.EnableTeamAIOutput & GameOptionFlag.NoInputIdleDisconnect,
                    GameType = GameType,
                    IsActive = true,
                    RoomName = $"Evos-{GameType.ToString()}-{GameID++}",
                    SubTypes = gameAvailability.SubTypes,
                    TeamAPlayers = gameAvailability.TeamAPlayers,
                    TeamABots = gameAvailability.TeamABots,
                    TeamBPlayers = gameAvailability.TeamBPlayers,
                    TeamBBots = gameAvailability.TeamBBots,
                    Map = SelectMap(gameAvailability.SubTypes[0]),
                    ResolveTimeoutLimit = 1600,
                    Spectators = 0
                },
                GameResult = GameResult.NoResult,
                GameServerAddress = "",
                GameStatus = GameStatus.Assembling,
                GameServerHost = "",
                IsActive = true,
                LoadoutSelectTimeout = GameType==GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                SelectTimeout = GameType==GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                // TODO: there are more options that may be usefull
            };

            return gameInfo;
        }

        public static string SelectMap(GameSubType gameSubType)
        {
            List<GameMapConfig> maps = gameSubType.GameMapConfigs.Where(x => x.IsActive).ToList();
            Random rand = new Random();
            int index = rand.Next(0, maps.Count);
            string selected = maps[index].Map;
            log.Info($"Selected {selected} out of {maps.Count} maps");
            return selected;
        }

        public void SetGameStatus(string roomName, GameStatus gameStatus)
        {
            Games[roomName].GameStatus = gameStatus;
        }

    }
}
