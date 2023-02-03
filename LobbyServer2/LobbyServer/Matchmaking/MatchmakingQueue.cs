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
using EvoS.Framework.Network.Static;
using log4net;
using WebSocketSharp;

namespace CentralServer.LobbyServer.Matchmaking
{
    class MatchmakingQueue
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingQueue));
        
        Dictionary<string, LobbyGameInfo> Games = new Dictionary<string, LobbyGameInfo>();
        ConcurrentDictionary<long, byte> QueuedGroups = new ConcurrentDictionary<long, byte>();
        public LobbyMatchmakingQueueInfo MatchmakingQueueInfo;
        GameType GameType => MatchmakingQueueInfo.GameType;
        
        private static int GameID = 0;

        public MatchmakingQueue(GameType gameType)
        {
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
            added = QueuedGroups.TryAdd(groupId, 0);
            MatchmakingQueueInfo.QueuedPlayers = GetPlayerCount();
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

            foreach (GameSubType subType in MatchmakingQueueInfo.GameConfig.SubTypes)
            {
                List<MatchmakingGroupInfo> groups = new List<MatchmakingGroupInfo>();

                lock (GroupManager.Lock)
                {
                    bool success = false;
                    // TODO: this add teams one by one until it has enough to form a game, QueuedGroups is a Dictionary
                    // so Keys might be unsorted (no guarantee of oldest player in queue will find a game first)
                    foreach (int groupId in QueuedGroups.Keys)
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
                        MatchmakingManager.StartGame(
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
                CreateTimestamp = DateTime.Now.Ticks,
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
