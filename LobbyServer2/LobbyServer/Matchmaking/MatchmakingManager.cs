using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
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

        public static List<MatchmakingQueue> GetQueues()
        {
            return Queues.Values.ToList();
        }

        private static bool _enabled = true;
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    log.Info($"Matchmaking queue is {(value ? "enabled" : "disabled")}");
                }

                _enabled = value;

                if (_enabled)
                {
                    Update();
                }
            }
        }

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
                GroupManager.Broadcast(group, new MatchmakingQueueToPlayersNotification() 
                {
                    GameType = gameType,
                    MessageToSend = MatchmakingQueueToPlayersNotification.MatchmakingQueueMessage.QueueConfirmed,    
                });

                // Update the queue
                queue.Update();

                foreach (long member in group.Members)
                {
                    SessionManager.GetClientConnection(member)?.BroadcastRefreshFriendList();
                    SessionManager.GetClientConnection(member)?.Send(new MatchmakingQueueToPlayersNotification()
                    {
                        AccountId = member,
                        MessageToSend = MatchmakingQueueToPlayersNotification.MatchmakingQueueMessage.QueueConfirmed,
                        GameType = gameType,
                        SubTypeMask = 1
                    });
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
            /*
            MatchmakingQueueConfig queueConfig = new MatchmakingQueueConfig();
            LobbyGameInfo practiceGameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = 1,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = 1,
                ActivePlayers = 1,
                CreateTimestamp = DateTime.UtcNow.Ticks,
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

            BridgeServerProtocol server = ServerManager.GetServer();
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
                    GameplayOverrides = GameConfig.GetGameplayOverrides()
                };

                client.Send(notification1);

                server.StartGame(practiceGameInfo, teamInfo);

                practiceGameInfo.GameStatus = GameStatus.Launched;
                GameInfoNotification notification2 = new GameInfoNotification()
                {
                    TeamInfo = LobbyTeamInfo.FromServer(teamInfo, 0, queueConfig),
                    GameInfo = practiceGameInfo,
                    PlayerInfo = LobbyPlayerInfo.FromServer(teamInfo.TeamPlayerInfo[0], 0, queueConfig)
                };

                client.Send(notification2);
            }
            */
        }

        public static async Task StartGameAsync(List<long> teamA, List<long> teamB, GameType gameType, GameSubType gameSubType)
        {
            log.Info($"Starting {gameType} game...");
            PvpGame game = GameManager.CreatePvpGame();
            if (game == null)
            {
                log.Info($"Failed to create {gameType} game");
                return;
            }
            SendUnassignQueueNotification(game.GetClients()); // TODO check if it's really needed
            await game.StartGameAsync(teamA, teamB, gameType, gameSubType);
        }

        public static void SendUnassignQueueNotification(List<LobbyServerProtocol> clients)
        {
            foreach (LobbyServerProtocol client in clients)
            {
                SendUnassignQueueNotification(client);
            }
        }

        public static void SendUnassignQueueNotification(LobbyServerProtocol client)
        {
            client.Send(new MatchmakingQueueAssignmentNotification
            {
                MatchmakingQueueInfo = null,
                Reason = "MatchFound@NewFrontEndScene"
            });
        }
    }
}