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
        private readonly IGroupRegistry _groups;

        public MatchmakingManager(IGroupRegistry groups)
        {
            _groups = groups;
            _queues = new Dictionary<GameType, MatchmakingQueue>
            {
                { GameType.Coop, new MatchmakingQueue(GameType.Coop, false) },
                { GameType.PvP, new MatchmakingQueue(GameType.PvP, true) },
                // { GameType.Ranked, new MatchmakingQueue(GameType.Ranked) },
                // { GameType.Custom, new MatchmakingQueue(GameType.Custom) }
            };
            groups.OnMembersChanged += UnqueueChangedGroup;
        }

        public static MatchmakingManager Instance { get; internal set; } = new MatchmakingManager(GroupManager.Instance);

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
        public static Task StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB, GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex, Dictionary<long, DateTime> queueEntryTimes = null) => Instance.StartGameAsyncCore(teamA, teamB, gameType, gameSubTypes, subTypeIndex, queueEntryTimes);
        public static void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players) => Instance.OnGameEndedCore(gameInfo, gameSummary, gameSubType, players);
        public static void UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true) => Instance.UpdateSelectedSubTypesCore(groupInfo, resetReadyStateIfUpdated);
        public static void UpdateSelectedSubTypesForAccount(long accountId) => Instance.UpdateSelectedSubTypesForAccountCore(accountId);

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
        Task IMatchmakingManager.StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB, GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex, Dictionary<long, DateTime> queueEntryTimes) => StartGameAsyncCore(teamA, teamB, gameType, gameSubTypes, subTypeIndex, queueEntryTimes);
        void IMatchmakingManager.OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players) => OnGameEndedCore(gameInfo, gameSummary, gameSubType, players);
        void IMatchmakingManager.UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated) => UpdateSelectedSubTypesCore(groupInfo, resetReadyStateIfUpdated);
        void IMatchmakingManager.UpdateSelectedSubTypesForAccount(long accountId) => UpdateSelectedSubTypesForAccountCore(accountId);

        // --- Core instance methods ---

        private List<MatchmakingQueue> GetQueuesCore() => _queues.Values.ToList();

        // null for game types that aren't queued, e.g. Practice
        private MatchmakingQueue GetQueueCore(GameType gameType) => _queues.GetValueOrDefault(gameType);

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
                if (queue.RemoveGroup(group.GroupId))
                {
                    removed = true;
                    NotifyGroupLeftQueue(group.GroupId);
                }
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

        // Whatever the group was queued for no longer applies: it leaves the queue,
        // and its sub types are filtered again for its new members or leader
        private void UnqueueChangedGroup(GroupInfo groupInfo)
        {
            RemoveGroupFromQueueCore(groupInfo, true);
            if (!groupInfo.IsEmpty())
            {
                UpdateSelectedSubTypesCore(groupInfo);
            }
        }

        // Looks the group up again: it is gone if it left the queue because its last member left it
        private void NotifyGroupLeftQueue(long groupId)
        {
            GroupInfo groupInfo = _groups.GetGroup(groupId);
            if (groupInfo is null)
            {
                log.Info($"Group {groupId} left the queue but does not exist");
                return;
            }
            UpdateSelectedSubTypesCore(groupInfo, false);
            _groups.Broadcast(groupInfo, new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null });
            ClientNotifier.Get().BroadcastRefreshGroup(groupInfo.Leader, false);
        }

        private void UpdateSelectedSubTypesCore(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true)
        {
            IClientConnection leaderConn = SessionManager.GetClientConnection(groupInfo.Leader);
            if (leaderConn is null)
            {
                log.Error($"UpdateSelectedSubTypes for group {groupInfo.GroupId} with missing leader {groupInfo.Leader}");
                return;
            }

            // Game types that aren't queued (e.g. Practice) have nothing to filter the selected sub types against
            MatchmakingQueue queue = GetQueueCore(leaderConn.SelectedGameType);
            ushort newMask = queue?.FilterSubTypeMask(groupInfo, leaderConn.GetSubTypeMask()) ?? leaderConn.GetSubTypeMask();

            ushort oldMask = groupInfo.SubTypeMask;
            if (oldMask != newMask)
            {
                groupInfo.SubTypeMask = newMask;
                if (resetReadyStateIfUpdated)
                {
                    log.Info($"UpdateSelectedSubTypes resetting group {groupInfo.GroupId} ready state "
                             + $"(was {queue?.DebugFormatSubTypeMask(oldMask) ?? oldMask.ToString()}, "
                             + $"now {queue?.DebugFormatSubTypeMask(newMask) ?? newMask.ToString()})");
                    leaderConn.BroadcastRefreshGroup(true);
                }
            }
        }

        private void UpdateSelectedSubTypesForAccountCore(long accountId)
        {
            GroupInfo groupInfo = _groups.GetPlayerGroup(accountId);
            if (groupInfo is not null)
            {
                UpdateSelectedSubTypesCore(groupInfo);
            }
        }
    }
}
