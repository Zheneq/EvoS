using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
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
using LobbyGameClientMessages;
using log4net;
using Prometheus;
using static EvoS.Framework.Misc.GameUtils;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : WebSocketBehaviorBase<WebSocketMessage>, IClientConnection, IHandlerRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));

        public long AccountId { get; set; }
        public string UserName { get; set; }
        public long SessionToken;
        public bool SessionCleaned = false; // tracks clean up methods execution for reconnection

        private readonly ISessionRegistry _sessionRegistry;
        private readonly IGroupRegistry _groupRegistry;
        private readonly IGameRegistry _gameRegistry;
        private readonly MatchmakingModule _matchmaking;
        private readonly GameLifecycleModule _gameLifecycle;

        public GameType SelectedGameType
        {
            get => _matchmaking.SelectedGameType;
            set => _matchmaking.SelectedGameType = value;
        }

        public ushort SelectedSubTypeMask
        {
            get => _matchmaking.SelectedSubTypeMask;
            set => _matchmaking.SelectedSubTypeMask = value;
        }

        public BotDifficulty AllyDifficulty
        {
            get => _matchmaking.AllyDifficulty;
            set => _matchmaking.AllyDifficulty = value;
        }

        public BotDifficulty EnemyDifficulty
        {
            get => _matchmaking.EnemyDifficulty;
            set => _matchmaking.EnemyDifficulty = value;
        }

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

            LogMessage(">", message);
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Send(stream.ToArray());
        }

        private void BroadcastImpl(WebSocketMessage message)
        {
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            BroadcastRaw(stream.ToArray());
            LogMessage(">>", message);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, string message)
        {
            response.Success = false;
            response.ErrorMessage = message;
            response.ResponseId = requestId;
            log.Info($"Sending error response: {message}");
            Send(response);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, Exception error = null)
        {
            response.Success = false;
            response.ErrorMessage = error?.Message;
            response.ResponseId = requestId;
            log.Info("Sending error response", error);
            Send(response);
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

        public bool IsInGroup() => !GroupManager.GetPlayerGroup(AccountId)?.IsSolo() ?? false;

        public int GetGroupSize() => GroupManager.GetPlayerGroup(AccountId)?.Members.Count ?? 1;

        public bool IsInQueue() => MatchmakingManager.IsQueued(GroupManager.GetPlayerGroup(AccountId));

        public LobbyServerPlayerInfo PlayerInfo => _gameLifecycle.PlayerInfo;

        public string Handle => LobbyServerUtils.GetHandle(AccountId);

        public event Action<LobbyServerProtocol, ChatNotification> OnChatNotification = delegate { };
        public event Action<LobbyServerProtocol, GroupChatRequest> OnGroupChatRequest = delegate { };

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
            _matchmaking = new MatchmakingModule(this, matchmakingManager);
            _gameLifecycle = new GameLifecycleModule(this, gameRegistry);

            RegisterHandler<ChatNotification>(HandleChatNotification);

            RegisterHandler<GroupChatRequest>(HandleGroupChatRequest);

            RegisterHandler<RankedHoverClickRequest>(HandlePlayerRankedHoverClickRequest);
            RegisterHandler<RankedBanRequest>(HandlePlayerRankedBanRequest);
            RegisterHandler<RankedSelectionRequest>(HandleRankedSelectionRequest);
            RegisterHandler<RankedTradeRequest>(HandleRankedTradeRequest);

            ILobbyModule[] modules = { new LoginModule(this), new StoreModule(this), new TelemetryModule(this), new AccountModule(this), new GroupModule(this, _groupRegistry), new FriendModule(this), _matchmaking, _gameLifecycle, new CharacterModule(this, _matchmaking, _gameLifecycle) };
            foreach (ILobbyModule module in modules)
            {
                module.Register(this);
            }
            new AdminModule(this).Register(this);
        }

        private void HandleRankedTradeRequest(RankedTradeRequest request)
        {
            RankedTradeResponse response = new RankedTradeResponse
            {
                ResponseId = request.RequestId,
                Success = false,
            };
            
            if (CurrentGame == null
                || !CurrentGame.IsDrafting
                || CurrentGame.PhaseSubType != FreelancerResolutionPhaseSubType.FREELANCER_TRADE)
            {
                Send(response); // TODO: error message?
                return;
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = CurrentGame.GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = CurrentGame.GetPlayerInfo(AccountId);

            Dictionary<int, CharacterType> teamSelections = player.TeamId == Team.TeamA
                ? rankedResolutionPhaseData.FriendlyTeamSelections
                : rankedResolutionPhaseData.EnemyTeamSelections;

            int tradeActionId = rankedResolutionPhaseData.TradeActions.FindIndex(
                p => p.OfferedCharacter == request.Trade.DesiredCharacter
                     && p.AskedPlayerId == player.PlayerId);
            lock (teamSelections)
            {
                switch (request.Trade.TradeAction)
                {
                    case RankedTradeData.TradeActionType.Reject:
                    {
                        if (tradeActionId >= 0)
                        {
                            rankedResolutionPhaseData.TradeActions.RemoveAt(tradeActionId);
                        }
                        else
                        {
                            Send(response); // TODO: error message?
                            return;
                        }

                        break;
                    }
                    case RankedTradeData.TradeActionType.AcceptOrOffer:
                    {
                        if (tradeActionId >= 0)
                        {
                            var existingTradeAction = rankedResolutionPhaseData.TradeActions[tradeActionId];
                            // Update the existing trade action to accepted
                            teamSelections[existingTradeAction.OfferingPlayerId] = existingTradeAction.DesiredCharacter;
                            teamSelections[existingTradeAction.AskedPlayerId] = existingTradeAction.OfferedCharacter;
                            rankedResolutionPhaseData.TradeActions.RemoveAll(p =>
                                p.OfferingPlayerId == player.PlayerId
                                || p.AskedPlayerId == player.PlayerId
                                || p.OfferingPlayerId == existingTradeAction.OfferingPlayerId
                                || p.AskedPlayerId == existingTradeAction.OfferingPlayerId);
                        }
                        else
                        {
                            // Handle the case where the trade request does not exist
                            CharacterType currentCharacterType = teamSelections[player.PlayerId];
                            CharacterType wantedCharacterType = request.Trade.DesiredCharacter;
                            int playerThatHasCharacter = teamSelections
                                .Where(item => item.Value == wantedCharacterType)
                                .Select(item => item.Key)
                                .FirstOrDefault();

                            LobbyServerPlayerInfo checkIsBot = CurrentGame.GetPlayerById(playerThatHasCharacter);

                            if (checkIsBot.IsAIControlled)
                            {
                                //automatic accept trade
                                teamSelections[playerThatHasCharacter] = currentCharacterType;
                                teamSelections[player.PlayerId] = wantedCharacterType;
                            }
                            else
                            {
                                RankedTradeData newTradeData = new RankedTradeData()
                                {
                                    AskedPlayerId = playerThatHasCharacter,
                                    TradeAction = RankedTradeData.TradeActionType.AcceptOrOffer,
                                    DesiredCharacter = teamSelections[playerThatHasCharacter],
                                    OfferedCharacter = currentCharacterType,
                                    OfferingPlayerId = player.PlayerId
                                };
                                rankedResolutionPhaseData.TradeActions.Add(newTradeData);
                            }
                        }

                        break;
                    }
                }
            }

            CurrentGame.SetRankedResolutionPhaseData(rankedResolutionPhaseData);
            CurrentGame.SendRankedResolutionSubPhase();

            response.Success = true;
            Send(response);
        }

        private void HandleRankedSelectionRequest(RankedSelectionRequest request)
        {
            // TODO also check ready state - do not update if already ready
            RankedSelectionResponse response = new RankedSelectionResponse
            {
                ResponseId = request.RequestId,
                Success = false,
            };
            
            if (CurrentGame == null
                || !CurrentGame.IsDrafting
                || (CurrentGame.PhaseSubType != FreelancerResolutionPhaseSubType.PICK_FREELANCER1
                    && CurrentGame.PhaseSubType != FreelancerResolutionPhaseSubType.PICK_FREELANCER2))
            {
                log.Warn($"Player {Handle} attempted to lock in draft selection in incorrect state "
                         + $"(game = {CurrentGame}, is drafting = {CurrentGame?.IsDrafting}, phase = {CurrentGame?.PhaseSubType}");
                response.LocalizedFailure = LocalizationPayload.Create("CannotReady@Global");
                Send(response);
                return;
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = CurrentGame.GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = CurrentGame.GetPlayerInfo(AccountId);

            Dictionary<int, CharacterType> teamSelections = player.TeamId == Team.TeamA
                ? rankedResolutionPhaseData.FriendlyTeamSelections
                : rankedResolutionPhaseData.EnemyTeamSelections;

            CharacterType characterType = request.Selection;

            lock (teamSelections)
            {
                if (teamSelections.ContainsKey(player.PlayerId))
                {
                    log.Warn($"Player {player.PlayerId} {Handle} attempted to lock in draft selection twice");
                    response.LocalizedFailure = LocalizationPayload.Create("CannotChangeCharactersOnceReadied@MonitorServer");
                    Send(response);
                    return;
                }

                HashSet<CharacterType> usedCharacterTypes = CurrentGame.GetUsedCharacterTypes();
                if (characterType == CharacterType.PendingWillFill
                    || characterType == CharacterType.TestFreelancer1
                    || characterType == CharacterType.TestFreelancer2)
                {
                    characterType = CurrentGame.AssignRandomCharacterForDraft(player, usedCharacterTypes, characterType);
                }

                List<RankedResolutionPlayerState> unselectedPlayerStates = rankedResolutionPhaseData.UnselectedPlayerStates;
                int stateIndex = unselectedPlayerStates.FindIndex(p => p.PlayerId == player.PlayerId);

                if (stateIndex >= 0)
                {
                    RankedResolutionPlayerState existingUnselectedPlayerStates = unselectedPlayerStates[stateIndex];
                    existingUnselectedPlayerStates.Intention = characterType;
                    existingUnselectedPlayerStates.OnDeckness = RankedResolutionPlayerState.ReadyState.Selected;
                    unselectedPlayerStates[stateIndex] = existingUnselectedPlayerStates;
                }

                List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
                int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);

                if (deckIndex >= 0 && !usedCharacterTypes.Contains(characterType))
                {
                    RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                    existingPlayersOnDeck.Intention = characterType;
                    existingPlayersOnDeck.OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected;
                    playersOnDeck[deckIndex] = existingPlayersOnDeck;

                    teamSelections.Add(player.PlayerId, characterType);

                    CurrentGame.UpdatePlayersInDeck();

                    CurrentGame.SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                    CurrentGame.SendRankedResolutionSubPhase();

                    if (CurrentGame.PlayersInDeck == 0)
                    {
                        CurrentGame.SkipRankedResolutionSubPhase();
                    }

                    response.Success = true;
                }
                else
                {
                    response.LocalizedFailure = LocalizationPayload.Create(
                        "CharacterTypeNotAllowed",
                        "Global",
                        LocalizationArg_Freelancer.Create(characterType));
                }
            }
            
            Send(response);
        }
        
        private void HandlePlayerRankedBanRequest(RankedBanRequest request)
        {
            // TODO also check ready state - do not update if already ready
            if (CurrentGame == null
                || !CurrentGame.IsDrafting
                || (CurrentGame.PhaseSubType != FreelancerResolutionPhaseSubType.PICK_BANS1
                    && CurrentGame.PhaseSubType != FreelancerResolutionPhaseSubType.PICK_BANS2))
            {
                Send(new RankedHoverClickResponse()
                {
                    //TODO: loc
                    ResponseId = request.RequestId,
                    Success = false,
                });
                return;
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = CurrentGame.GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = CurrentGame.GetPlayerInfo(AccountId);

            List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
            int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);

            HashSet<CharacterType> usedCharacterTypes = CurrentGame.GetUsedCharacterTypes();
            CharacterType characterType = request.Selection;
            if (deckIndex >= 0 && !usedCharacterTypes.Contains(characterType)) // TODO you can still lock in a duplicate by timing out
            {

                if (characterType == CharacterType.PendingWillFill
                    || characterType == CharacterType.TestFreelancer1
                    || characterType == CharacterType.TestFreelancer2)
                {
                    characterType = CurrentGame.AssignRandomCharacterForDraft(player, usedCharacterTypes, characterType);
                }

                if (player.TeamId == Team.TeamA)
                {
                    rankedResolutionPhaseData.FriendlyBans.Add(characterType);
                }
                else
                {
                    rankedResolutionPhaseData.EnemyBans.Add(characterType);
                }

                RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                existingPlayersOnDeck.Intention = characterType;
                existingPlayersOnDeck.OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected;
                playersOnDeck[deckIndex] = existingPlayersOnDeck;

                CurrentGame.SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                CurrentGame.SendRankedResolutionSubPhase();
            
                CurrentGame.SkipRankedResolutionSubPhase();

                Send(new RankedBanResponse()
                {
                    ResponseId = request.RequestId,
                    Success = true,
                });
            }
            else
            {
                Send(new RankedBanResponse()
                {
                    ResponseId = request.RequestId,
                    Success = false,
                });
                // TODO: error message?
            }

        }

        private void HandlePlayerRankedHoverClickRequest(RankedHoverClickRequest request)
        {
            // TODO also check ready state - do not update if already ready
            if (CurrentGame == null || !CurrentGame.IsDrafting)
            {
                Send(new RankedHoverClickResponse()
                {
                    ResponseId = request.RequestId,
                    Success = false,
                });
                return;
            }
            
            RankedResolutionPhaseData rankedResolutionPhaseData = CurrentGame.GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = CurrentGame.GetPlayerInfo(AccountId);

            List<RankedResolutionPlayerState> unselectedPlayerStates = rankedResolutionPhaseData.UnselectedPlayerStates;
            int stateIndex = unselectedPlayerStates.FindIndex(p => p.PlayerId == player.PlayerId);
            
            if (stateIndex >= 0)
            {
                RankedResolutionPlayerState existingUnselectedPlayerStates = unselectedPlayerStates[stateIndex];
                existingUnselectedPlayerStates.Intention = request.Selection;
                unselectedPlayerStates[stateIndex] = existingUnselectedPlayerStates;

                List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
                int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);
                
                if (deckIndex >= 0)
                {
                    RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                    existingPlayersOnDeck.Intention = request.Selection;
                    playersOnDeck[deckIndex] = existingPlayersOnDeck;
                }

                CurrentGame.SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                CurrentGame.SendRankedResolutionSubPhase();

                Send(new RankedHoverClickResponse()
                {
                    ResponseId = request.RequestId,
                    Success = true,
                });
            }
            else
            {
                Send(new RankedHoverClickResponse()
                {
                    ResponseId = request.RequestId,
                    Success = false,
                });
                // TODO: error message?
            }
        }

        protected override void HandleClose(WsCloseEventArgs e)
        {
            UnregisterAllHandlers();
            log.Info(string.Format(Messages.PlayerDisconnected, this.UserName));
            ConnectionEndStatus.WithLabels(e.Code.ToString()).Observe(1);

            CurrentGame?.OnPlayerDisconnectedFromLobby(AccountId);

            SessionManager.OnPlayerDisconnect(this);

            if (!SessionCleaned)
            {
                SessionCleaned = true;
                GroupManager.LeaveGroup(AccountId, false);
            }

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

        public void UpdateGroupReadyState() => _matchmaking.UpdateGroupReadyState();

        public void BroadcastRefreshGroup(bool resetReadyState = false)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            if (group == null)
            {
                RefreshGroup(resetReadyState);
            }
            else
            {
                foreach (long groupMember in group.Members)
                {
                    SessionManager.GetClientConnection(groupMember)?.RefreshGroup(resetReadyState);
                }
            }
        }

        public void RefreshGroup(bool resetReadyState = false)
        {
            if (resetReadyState)
            {
                _matchmaking.ResetReadyState();
            }
            LobbyPlayerGroupInfo info = GroupManager.GetGroupInfo(AccountId);

            Send(new GroupUpdateNotification
            {
                Members = info.Members,
                GameType = info.SelectedQueueType,
                SubTypeMask = info.SubTypeMask,
                AllyDifficulty = BotDifficulty.Medium,
                EnemyDifficulty = BotDifficulty.Medium,
                GroupId = GroupManager.GetGroupID(AccountId)
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

        public void HandleChatNotification(ChatNotification notification)
        {
            OnChatNotification(this, notification);
        }

        public void OnAccountVisualsUpdated()
        {
            BroadcastRefreshFriendList();
            BroadcastRefreshGroup();
            CurrentGame?.OnAccountVisualsUpdated(AccountId);
        }

        public void HandleGroupChatRequest(GroupChatRequest request)
        {
            OnGroupChatRequest(this, request);
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
            SendSystemMessage($"You are about to join {serverName}{serverVersion} for game {GameIdString(game.GameInfo)}.");
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
