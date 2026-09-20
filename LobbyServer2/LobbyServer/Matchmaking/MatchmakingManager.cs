using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using static MoreLinq.Extensions.ShuffleExtension;

namespace CentralServer.LobbyServer.Matchmaking
{
    /// <summary>
    /// Manages the matchmaking process. it contains queues where all the player are assigned and when there are enough
    /// players launches a new game
    /// </summary>
    public class MatchmakingManager : IMatchmakingManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingManager));
        private readonly object _queueUpdateRunning = new object();
        private bool _enabled = true;
        private Dictionary<GameType, MatchmakingQueue> _queues;

        public MatchmakingManager()
        {
            _queues = new Dictionary<GameType, MatchmakingQueue>
            {
                // { GameType.Practice, new MatchmakingQueue(GameType.Practice) },
                { GameType.Coop, new MatchmakingQueue(GameType.Coop, false) },
                { GameType.PvP, new MatchmakingQueue(GameType.PvP, true) },
                // { GameType.Ranked, new MatchmakingQueue(GameType.Ranked) },
                // { GameType.Custom, new MatchmakingQueue(GameType.Custom) }
            };
        }

        public static MatchmakingManager Instance { get; internal set; } = new MatchmakingManager();

        // --- Static shims ---

        public static List<MatchmakingQueue> GetQueues() => Instance.GetQueuesCore();
        public static MatchmakingQueue GetQueue(GameType gameType) => Instance.GetQueueCore(gameType);

        public static bool Enabled
        {
            get => Instance._enabled;
            set
            {
                if (Instance._enabled != value)
                {
                    log.Info($"Matchmaking queue is {(value ? "enabled" : "disabled")}");
                }
                Instance._enabled = value;
            }
        }

        public static void Update() => Instance.UpdateCore();
        public static bool AddGroupToQueue(GameType gameType, GroupInfo group) => Instance.AddGroupToQueueCore(gameType, group);
        public static bool RemoveGroupFromQueue(GroupInfo group, bool suppressWarnings = false) => Instance.RemoveGroupFromQueueCore(group, suppressWarnings);
        public static bool IsQueued(GroupInfo group) => Instance.IsQueuedCore(group);
        public static void StartPractice(LobbyServerProtocol client) => Instance.StartPracticeCore(client);
        public static Task StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB, GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex, Dictionary<long, DateTime> queueEntryTimes = null) => Instance.StartGameAsyncCore(teamA, teamB, gameType, gameSubTypes, subTypeIndex, queueEntryTimes);
        public static void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players) => Instance.OnGameEndedCore(gameInfo, gameSummary, gameSubType, players);

        // --- IMatchmakingManager explicit implementations ---

        List<MatchmakingQueue> IMatchmakingManager.GetQueues() => GetQueuesCore();
        MatchmakingQueue IMatchmakingManager.GetQueue(GameType gameType) => GetQueueCore(gameType);
        bool IMatchmakingManager.Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    log.Info($"Matchmaking queue is {(value ? "enabled" : "disabled")}");
                }
                _enabled = value;
            }
        }
        void IMatchmakingManager.Update() => UpdateCore();
        bool IMatchmakingManager.AddGroupToQueue(GameType gameType, GroupInfo group) => AddGroupToQueueCore(gameType, group);
        bool IMatchmakingManager.RemoveGroupFromQueue(GroupInfo group, bool suppressWarnings) => RemoveGroupFromQueueCore(group, suppressWarnings);
        bool IMatchmakingManager.IsQueued(GroupInfo group) => IsQueuedCore(group);
        void IMatchmakingManager.StartPractice(LobbyServerProtocol client) => StartPracticeCore(client);
        Task IMatchmakingManager.StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB, GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex, Dictionary<long, DateTime> queueEntryTimes) => StartGameAsyncCore(teamA, teamB, gameType, gameSubTypes, subTypeIndex, queueEntryTimes);
        void IMatchmakingManager.OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players) => OnGameEndedCore(gameInfo, gameSummary, gameSubType, players);

        // --- Core instance methods ---

        private List<MatchmakingQueue> GetQueuesCore() => _queues.Values.ToList();

        private MatchmakingQueue GetQueueCore(GameType gameType) => _queues[gameType];

        /// <summary>
        /// Updates all the queues
        /// </summary>
        private void UpdateCore()
        {
            lock (_queueUpdateRunning)
            {
                foreach (var queue in _queues.Values)
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
        private bool AddGroupToQueueCore(GameType gameType, GroupInfo group)
        {
            // Penalties are also checked in HandleJoinMatchmakingQueueRequest for a proper response payload,
            // but this is the only choke point for every path into the queue (e.g. group ready-up).
            foreach (long member in group.Members)
            {
                LocalizationPayload penalty = QueuePenaltyManager.CheckQueuePenalties(member, gameType, group.Leader);
                if (penalty is not null)
                {
                    ClientNotifier.Get().SendSystemMessage(group.Leader, penalty);
                    ClientNotifier.Get().BroadcastRefreshGroup(group.Leader, true);
                    return false;
                }
            }

            // Get the queue
            MatchmakingQueue queue = _queues[gameType];

            // Add player to the queue
            LobbyMatchmakingQueueInfo info = queue.AddGroup(group.GroupId, out bool added);

            if (added)
            {
                // Send 'Assigned to queue notification' to the players
                GroupManager.Broadcast(group, new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = info });

                foreach (long member in group.Members)
                {
                    ClientNotifier.Get().MarkFriendListForUpdate(member);
                    ClientNotifier.Get().Send(member, new MatchmakingQueueToPlayersNotification
                    {
                        AccountId = member,
                        MessageToSend = MatchmakingQueueToPlayersNotification.MatchmakingQueueMessage.QueueConfirmed,
                        GameType = gameType,
                        SubTypeMask = GroupManager.GetGroupSubTypeMask(group)
                    });
                }
            }
            else
            {
                GroupManager.Broadcast(group, new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null });
                ClientNotifier.Get().BroadcastRefreshGroup(group.Leader, true);
                foreach (long member in group.Members)
                {
                    ClientNotifier.Get().MarkFriendListForUpdate(member);
                }
            }

            return added;
        }

        private bool RemoveGroupFromQueueCore(GroupInfo group, bool suppressWarnings = false)
        {
            bool removed = false;
            foreach (MatchmakingQueue queue in _queues.Values)
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
                    ClientNotifier.Get().MarkFriendListForUpdate(member);
                }
            }
            return removed;
        }

        private bool IsQueuedCore(GroupInfo group)
        {
            if (group == null)
            {
                return false;
            }
            foreach (MatchmakingQueue queue in _queues.Values)
            {
                if (queue.IsQueued(group.GroupId))
                {
                    return true;
                }
            }
            return false;
        }

        private void StartPracticeCore(LobbyServerProtocol client)
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

        private async Task StartGameAsyncCore(
            List<MatchPlayerData> teamA,
            List<MatchPlayerData> teamB,
            GameType gameType,
            List<GameSubType> gameSubTypes,
            int subTypeIndex,
            Dictionary<long, DateTime> queueEntryTimes = null)
        {
            log.Info($"Starting {gameType} game...");
            PvpGame game = GameManager.CreatePvpGame();
            if (game == null)
            {
                log.Info($"Failed to create {gameType} game");
                return;
            }
            await game.StartGameAsync(teamA.Shuffle().ToList(), teamB.Shuffle().ToList(), gameType, gameSubTypes, subTypeIndex, queueEntryTimes);
        }

        private void OnGameEndedCore(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players)
        {
            if (_queues.TryGetValue(gameInfo.GameConfig.GameType, out var queue))
            {
                queue.OnGameEnded(gameInfo, gameSummary, gameSubType, players);
            }
        }
    }
}
