using System;
using System.Collections.Generic;
using System.IO;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Account;
using CentralServer.LobbyServer.Admin;
using CentralServer.LobbyServer.Login;
using CentralServer.LobbyServer.Store;
using CentralServer.LobbyServer.Utils;
using CentralServer.Proxy;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;
using Prometheus;
using static EvoS.Framework.Misc.GameUtils;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : WebSocketBehaviorBase<WebSocketMessage>, IClientConnection, IHandlerRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));

        public long AccountId { get; private set; }
        public string UserName { get; private set; }
        public long SessionToken { get; private set; }

        private readonly ISessionRegistry _sessionRegistry;
        private readonly IGroupRegistry _groupRegistry;
        private readonly IGameRegistry _gameRegistry;
        private readonly IMatchmakingManager _matchmakingManager;
        private readonly MatchmakingModule _matchmaking;
        private readonly GameLifecycleModule _gameLifecycle;

        public GameType SelectedGameType => _matchmaking.SelectedGameType;

        public bool IsReady => _matchmaking.IsReady;

        protected ProxyConfiguration.Proxy Proxy = null;

        protected override string GetConnContext()
        {
            return "C " + AccountId;
        }

        protected override void HandleOpen()
        {
            Proxy = LobbyServerUtils.DetectProxy(Context);
            if (Proxy != null)
            {
                log.Info($"Detected proxy {Proxy.GetName()}");
            }
        }

        protected override WebSocketMessage DeserializeMessage(byte[] data, out int callbackId)
        {
            callbackId = 0;
            return (WebSocketMessage)EvosSerializer.Instance.Deserialize(new MemoryStream(data));
        }

        public void Send(WebSocketMessage message)
        {
            Wrap(SendImpl, message);
        }

        public void Broadcast(WebSocketMessage message)
        {
            Wrap(BroadcastImpl, message);
        }

        private void SendImpl(WebSocketMessage message)
        {
            if (!IsConnected)
            {
                log.Warn($"Attempted to send {message.GetType()} to a disconnected socket");
                return;
            }

            if (Proxy is not null && ProxyPatcher.Mapping.TryGetValue(message.GetType(), out var patcher))
            {
                message = patcher(message, Proxy);
            }

            if (ProtocolPatcher.Mapping.TryGetValue(message.GetType(), out var protocolPatcher))
            {
                message = protocolPatcher(message, _sessionRegistry.GetSessionInfo(AccountId)?.ProtocolVersion);
            }

            LogMessage(">", message);
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Send(stream.ToArray());
        }

        private void BroadcastImpl(WebSocketMessage message)
        {
            // The same payload goes to every client, so it must be safe for all of them
            if (ProtocolPatcher.Mapping.TryGetValue(message.GetType(), out var protocolPatcher))
            {
                message = protocolPatcher(message, null);
            }

            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            BroadcastRaw(stream.ToArray());
            LogMessage(">>", message);
        }

        public void Initialize(long accountId, string userName, long sessionToken)
        {
            AccountId = accountId;
            UserName = userName;
            SessionToken = sessionToken;
        }

        public string? ProxyName => Proxy?.Name;

        public void SetGameType(GameType gameType) => _matchmaking.SetGameType(gameType);

        public ushort GetSubTypeMask() => _matchmaking.GetSubTypeMask();

        public PlayerOnlineStatus Status { get; set; } = PlayerOnlineStatus.Online;

        public Game CurrentGame => _gameLifecycle.CurrentGame;

        public bool IsInGame() => _gameLifecycle.IsInGame();

        public bool IsInCharacterSelect() => _gameLifecycle.IsInCharacterSelect();

        public bool IsInGroup() => !_groupRegistry.GetPlayerGroup(AccountId)?.IsSolo() ?? false;

        public bool IsInQueue() => _matchmakingManager.IsQueued(_groupRegistry.GetPlayerGroup(AccountId));

        public CharacterType? ActiveCharacterType => _gameLifecycle.PlayerInfo?.CharacterType;

        public string Handle => LobbyServerUtils.GetHandle(AccountId);

        private static readonly Summary ConnectionEndStatus = Metrics
            .CreateSummary(
                "evos_connection_player_statuses",
                "Error code player connection are ended with.",
                new[] { "errorCode" },
                new SummaryConfiguration
                {
                    MaxAge = TimeSpan.FromHours(1)
                });

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            RegisterHandler<T>(handler);
        }

        public LobbyServerProtocol(ISessionRegistry sessionRegistry, IGroupRegistry groupRegistry, IGameRegistry gameRegistry, IMatchmakingManager matchmakingManager)
        {
            _sessionRegistry = sessionRegistry;
            _groupRegistry = groupRegistry;
            _gameRegistry = gameRegistry;
            _matchmakingManager = matchmakingManager;
            _matchmaking = new MatchmakingModule(this, _sessionRegistry, _groupRegistry, _matchmakingManager);
            _gameLifecycle = new GameLifecycleModule(this, _sessionRegistry, _groupRegistry, _gameRegistry);

            ILobbyModule[] modules = [
                new LoginModule(this, _sessionRegistry, _groupRegistry),
                new ChatModule(this),
                new StoreModule(this),
                new TelemetryModule(this, _sessionRegistry),
                new AccountModule(this),
                new GroupModule(this, _sessionRegistry, _groupRegistry, _gameRegistry),
                new FriendModule(this),
                _matchmaking,
                _gameLifecycle,
                new DraftModule(this),
                new CharacterModule(this, _groupRegistry, _matchmaking, _gameLifecycle),
                new AdminModule(this, _gameRegistry)
            ];
            foreach (ILobbyModule module in modules)
            {
                module.Register(this);
            }
        }

        protected override void HandleClose(WsCloseEventArgs e)
        {
            UnregisterAllHandlers();
            log.Info(string.Format(Messages.PlayerDisconnected, this.UserName));
            ConnectionEndStatus.WithLabels(e.Code.ToString()).Observe(1);

            CurrentGame?.OnPlayerDisconnectedFromLobby(AccountId);
            // A restarted client has no invite popups
            CustomGameManager.Invites.RemoveForInvitee(AccountId);

            _sessionRegistry.OnPlayerDisconnect(this);
            _groupRegistry.LeaveGroup(AccountId, false);

            BroadcastRefreshFriendList();
        }

        public void JoinGame(Game game) => _gameLifecycle.JoinGame(game);

        public bool LeaveGame(Game game) => _gameLifecycle.LeaveGame(game);

        public void BroadcastRefreshFriendList()
        {
            FriendManager.MarkForUpdate(AccountId);
        }

        public void RefreshFriendList()
        {
            Send(FriendManager.GetFriendStatusNotification(AccountId));
        }

        public void BroadcastRefreshGroup(bool resetReadyState = false)
        {
            GroupInfo group = _groupRegistry.GetPlayerGroup(AccountId);
            if (group == null)
            {
                RefreshGroup(resetReadyState);
            }
            else
            {
                foreach (long groupMember in group.Members)
                {
                    _sessionRegistry.GetClientConnection(groupMember)?.RefreshGroup(resetReadyState);
                }
            }
        }

        public void RefreshGroup(bool resetReadyState = false)
        {
            if (resetReadyState)
            {
                _matchmaking.ResetReadyState();
            }
            LobbyPlayerGroupInfo info = _groupRegistry.GetGroupInfo(AccountId);

            Send(new GroupUpdateNotification
            {
                Members = info.Members,
                GameType = info.SelectedQueueType,
                SubTypeMask = info.SubTypeMask,
                AllyDifficulty = BotDifficulty.Medium,
                EnemyDifficulty = BotDifficulty.Medium,
                GroupId = _groupRegistry.GetGroupID(AccountId)
            });
        }

        public void SendGameUnassignmentNotification()
        {
            Send(new GameAssignmentNotification
            {
                GameInfo = null,
                GameResult = GameResult.NoResult,
                Reconnection = false
            });
        }

        public void ResetReadyState() => _matchmaking.ResetReadyState();

        public void OnAccountVisualsUpdated()
        {
            BroadcastRefreshFriendList();
            BroadcastRefreshGroup();
            CurrentGame?.OnAccountVisualsUpdated(AccountId);
        }

        public void OnLeaveGroup()
        {
            _matchmaking.Unready();
            RefreshGroup();
            BroadcastRefreshFriendList();
        }

        public void OnJoinGroup()
        {
            _matchmaking.Unready();
            BroadcastRefreshFriendList();
        }

        public void OnGroupDisbanded()
        {
            BroadcastRefreshFriendList();
        }

        public void OnStartGame(Game game)
        {
            _matchmaking.Unready();
        }

        public void OnGameAssigned(Game game)
        {
            var serverName = CompilerExtensions.IsNullOrEmpty(game.Server.Name) ? "an unknown server" : game.Server.Name;
            var serverVersion = CompilerExtensions.IsNullOrEmpty(game.Server.BuildVersion) ? "" : $" v{game.Server.BuildVersion}";
            SendSystemMessage($"You are about to join {serverName}{serverVersion} for game {Maps.GetMapName.GetValueOrDefault(game.Map, "")} {GameIdString(game.GameInfo)}.");
        }

        public void SendSystemMessage(string text)
        {
            Send(new ChatNotification
            {
                ConsoleMessageType = ConsoleMessageType.SystemMessage,
                Text = text
            });
        }

        public void SendSystemMessage(LocalizationPayload text)
        {
            Send(new ChatNotification
            {
                ConsoleMessageType = ConsoleMessageType.SystemMessage,
                LocalizedText = text
            });
        }

    }
}
