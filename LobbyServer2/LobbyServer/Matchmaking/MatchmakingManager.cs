using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking
{
    /// <summary>
    /// Manages the matchmaking process. it contains queues where all the player are assigned and when there are enough
    /// players launches a new game
    /// </summary>
    public static class MatchmakingManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingManager));
        private static readonly object queueUpdateRunning = new object();
        
        // List of matchmaking queues by game mode (practive, coop, pvp, ranked and custom)
        private static Dictionary<GameType, MatchmakingQueue> Queues = new Dictionary<GameType, MatchmakingQueue>()
        {
            { GameType.Practice, new MatchmakingQueue(GameType.Practice) },
            { GameType.Coop, new MatchmakingQueue(GameType.Coop) },
            { GameType.PvP, new MatchmakingQueue(GameType.PvP) },
            { GameType.Ranked, new MatchmakingQueue(GameType.Ranked) },
            { GameType.Custom, new MatchmakingQueue(GameType.Custom) }
        };

        /// <summary>
        /// Updates all the queues
        /// </summary>
        public static void Update()
        {
            lock (queueUpdateRunning)
            {
                foreach (var queue in Queues.Values)
                {
                    queue.Update();
                }
            }
            
        }

        /// <summary>
        /// Adds a player to a queue
        /// </summary>
        /// <param name="gameType">selected gamemode</param>
        /// <param name="group">group</param>
        public static bool AddGroupToQueue(GameType gameType, GroupInfo group)
        {
            // Get the queue
            MatchmakingQueue queue = Queues[gameType];

            // Add player to the queue
            LobbyMatchmakingQueueInfo info = queue.AddGroup(group.GroupId, out bool added);

            if (added)
            {
                // Send 'Assigned to queue notification' to the players
                GroupManager.Broadcast(group, new MatchmakingQueueAssignmentNotification() { MatchmakingQueueInfo = info });

                // Update the queue
                queue.Update();
                
                foreach (long member in group.Members)
                {
                    SessionManager.GetClientConnection(member)?.BroadcastRefreshFriendList();
                }
            }

            return added;
        }

        public static bool RemoveGroupFromQueue(GroupInfo group, bool suppressWarnings = false)
        {
            bool removed = false;
            foreach (MatchmakingQueue queue in Queues.Values)
            {
                removed |= queue.RemoveGroup(group.GroupId);
            }
            if (!removed && !suppressWarnings)
            {
                log.Warn($"Attempted to remove group {group.GroupId} by {group.Leader} from the queue but failed");
            }
            if (removed)
            {
                foreach (long member in group.Members)
                {
                    SessionManager.GetClientConnection(member)?.BroadcastRefreshFriendList();
                }
            }
            return removed;
        }

        public static bool IsQueued(GroupInfo group)
        {
            if (group == null)
            {
                return false;
            }
            foreach (MatchmakingQueue queue in Queues.Values)
            {
                if (queue.IsQueued(group.GroupId))
                {
                    return true;
                }
            }
            return false;
        }

        public static void StartPractice(LobbyServerProtocolBase client)
        {
            MatchmakingQueueConfig queueConfig = new MatchmakingQueueConfig();
            LobbyGameInfo practiceGameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = 1,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = 1,
                ActivePlayers = 1,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = GameType.PvP,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = Maps.Skyway_Deathmatch,
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[GameType.Practice].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = 1,
                    TeamBBots = 2,
                    TeamBPlayers = 0,
                }
            };

            LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo();
            teamInfo.TeamPlayerInfo = new List<LobbyServerPlayerInfo>
            {
                SessionManager.GetPlayerInfo(client.AccountId),
                CharacterManager.GetPunchingDummyPlayerInfo(),
                CharacterManager.GetPunchingDummyPlayerInfo()
            };
            teamInfo.TeamPlayerInfo[0].TeamId = Team.TeamA;
            teamInfo.TeamPlayerInfo[0].PlayerId = 1;
            teamInfo.TeamPlayerInfo[1].TeamId = Team.TeamB;
            teamInfo.TeamPlayerInfo[1].PlayerId = 2;
            teamInfo.TeamPlayerInfo[2].TeamId = Team.TeamB;
            teamInfo.TeamPlayerInfo[2].PlayerId = 3;

            BridgeServerProtocol server = ServerManager.GetServer(practiceGameInfo, teamInfo);
            if (server == null)
            {
                log.Warn("No available server for practice gamemode");
            }
            else
            {
                practiceGameInfo.GameServerAddress = server.URI;
                practiceGameInfo.GameServerProcessCode = server.ProcessCode;
                practiceGameInfo.GameStatus = GameStatus.Launching;

                GameAssignmentNotification notification1 = new GameAssignmentNotification
                {
                    GameInfo = practiceGameInfo,
                    GameResult = GameResult.NoResult,
                    Observer = false,
                    PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[0], 0, queueConfig),
                    Reconnection = false,
                    GameplayOverrides = client.GetGameplayOverrides()
                };

                client.Send(notification1);

                practiceGameInfo.GameStatus = GameStatus.Launched;
                GameInfoNotification notification2 = new GameInfoNotification()
                {
                    TeamInfo = LobbyTeamInfo.FromServer(teamInfo, 0, queueConfig),
                    GameInfo = practiceGameInfo,
                    PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[0], 0, queueConfig)
                };

                client.Send(notification2);
            }
        }

        public static void StartGame(List<long> teamA, List<long> teamB, GameType gameType, GameSubType gameSubType)
        {
            log.Info($"Starting {gameType} game...");
            MatchmakingQueueConfig queueConfig = new MatchmakingQueueConfig();

            MatchmakingQueue lobbyQueue = Queues[gameType];
            GameSubType subType = lobbyQueue.MatchmakingQueueInfo.GameConfig.SubTypes[0];
            LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo { TeamPlayerInfo = new List<LobbyServerPlayerInfo>() };
            List<LobbyServerProtocol> clients = new List<LobbyServerProtocol>();
            // Fill team A
            foreach (long accountId in teamA)
            {
                LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
                if (client == null)
                {
                    log.Error($"Tried to add {accountId} to a game but they are not connected!");
                    continue;
                }
                clients.Add(client);
                LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(client.AccountId);
                playerInfo.TeamId = Team.TeamA;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                log.Info($"adding player {client.UserName}, {client.AccountId} to team A");
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            // Fill team B
            foreach (long accountId in teamB)
            {
                LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
                if (client == null)
                {
                    log.Error($"Tried to add {accountId} to a game but they are not connected!");
                    continue;
                }
                clients.Add(client);
                LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(client.AccountId);
                playerInfo.TeamId = Team.TeamB;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                log.Info($"adding player {client.UserName}, {client.AccountId} to team B");
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = clients.Count,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = clients.Count,
                ActivePlayers = clients.Count,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = MatchmakingQueue.SelectMap(gameSubType),
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = teamInfo.TeamAPlayerInfo.Count(),
                    TeamBBots = 0,
                    TeamBPlayers = teamInfo.TeamBPlayerInfo.Count(),
                }
            };

            BridgeServerProtocol server = ServerManager.GetServer(gameInfo, teamInfo);
            if (server == null)
            {
                log.Info($"No available server for {gameType} gamemode");
                return;
            }
            else
            {
                server.clients = clients;
                gameInfo.GameServerAddress = server.URI;
                gameInfo.GameServerProcessCode = server.ProcessCode;
                gameInfo.GameStatus = GameStatus.Launching;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocol client = clients[i];
                    GameAssignmentNotification notification = new GameAssignmentNotification
                    {
                        GameInfo = gameInfo,
                        GameResult = GameResult.NoResult,
                        Observer = false,
                        PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[i], 0, queueConfig),
                        Reconnection = false,
                        GameplayOverrides = client.GetGameplayOverrides()
                    };

                    client.Send(notification);
                    client.CurrentServer = server;
                }

                gameInfo.GameStatus = GameStatus.Launched;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameInfoNotification notification = new GameInfoNotification()
                    {
                        TeamInfo = LobbyTeamInfo.FromServer(teamInfo, 0, queueConfig),
                        GameInfo = gameInfo,
                        PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[i], 0, queueConfig)
                    };

                    client.Send(notification);
                }

                clients.ForEach(c => c.OnStartGame(server));

                log.Info($"Game {gameType} started");
            }
        }

        public static void StartGameVBots(List<LobbyServerProtocolBase> clients, GameType gameType)
        {
            MatchmakingQueueConfig queueConfig = new MatchmakingQueueConfig();
            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = clients.Count,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = clients.Count,
                ActivePlayers = clients.Count,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = Maps.Skyway_Deathmatch,
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = clients.Count,
                    TeamBBots = 2,
                    TeamBPlayers = 0,
                }
            };

            LobbyServerTeamInfo teamInfo = new LobbyServerTeamInfo();
            teamInfo.TeamPlayerInfo = new List<LobbyServerPlayerInfo>();

            for (int i = 0; i < clients.Count; i++)
            {
                LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(clients[i].AccountId);
                playerInfo.TeamId = Team.TeamA;
                playerInfo.PlayerId = i + 1;
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            for (int i = 0; i < 2; i++)
            {
                LobbyServerPlayerInfo playerInfo = CharacterManager.GetPunchingDummyPlayerInfo();
                playerInfo.TeamId = Team.TeamB;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count + 1;
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            BridgeServerProtocol server = ServerManager.GetServer(gameInfo, teamInfo);
            if (server == null)
            {
                log.Warn("No available server for practice gamemode");
            }
            else
            {
                gameInfo.GameServerAddress = server.URI;
                gameInfo.GameServerProcessCode = server.ProcessCode;
                gameInfo.GameStatus = GameStatus.Launching;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameAssignmentNotification notification = new GameAssignmentNotification
                    {
                        GameInfo = gameInfo,
                        GameResult = GameResult.NoResult,
                        Observer = false,
                        PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[i], 0, queueConfig),
                        Reconnection = false,
                        GameplayOverrides = client.GetGameplayOverrides()
                    };

                    client.Send(notification);
                }

                gameInfo.GameStatus = GameStatus.Launched;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameInfoNotification notification = new GameInfoNotification()
                    {
                        TeamInfo = LobbyTeamInfo.FromServer(teamInfo, 0, queueConfig),
                        GameInfo = gameInfo,
                        PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[i], 0, queueConfig)
                    };

                    client.Send(notification);
                }
            }
        }
    }
}