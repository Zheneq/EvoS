using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Quest;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Account;
using CentralServer.LobbyServer.Admin;
using CentralServer.LobbyServer.Store;
using CentralServer.LobbyServer.TrustWar;
using CentralServer.LobbyServer.Utils;
using CentralServer.Proxy;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Exceptions;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using LobbyGameClientMessages;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Prometheus;
using static EvoS.Framework.DataAccess.Daos.MiscDao;
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

        private static readonly Lazy<string> CachedPatchNotes = new(FetchGithubPatchNotes);

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

        public void SendLobbyServerReadyNotification()
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            FactionCompetitionNotification factionCompetitionNotification = new();

            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                TrustWarEntry trustWar = TrustWarManager.getTrustWarEntry();
                factionCompetitionNotification = new FactionCompetitionNotification()
                {
                    ActiveIndex = 1,
                    Scores = new Dictionary<int, long>() {
                        { 0, trustWar.Points[0] },
                        { 1, trustWar.Points[1] },
                        { 2, trustWar.Points[2] }
                    }
                };
            }


            LobbyServerReadyNotification notification = new LobbyServerReadyNotification
            {
                AccountData = account.CloneForClient(),
                AlertMissionData = new LobbyAlertMissionDataNotification(),
                CharacterDataList = account.CharacterData.Values.ToList(),
                CommerceURL = "http://127.0.0.1/AtlasCommerce",
                EnvironmentType = EnvironmentType.External,
                FactionCompetitionStatus = factionCompetitionNotification,
                FriendStatus = FriendManager.GetFriendStatusNotification(AccountId),
                GroupInfo = GroupManager.GetGroupInfo(AccountId),
                SeasonChapterQuests = QuestManager.GetSeasonQuestDataNotification(),
                ServerQueueConfiguration = GetServerQueueConfigurationUpdateNotification(),
                Status = GetLobbyStatusNotification(account)
            };

            Send(notification);
        }

        private ServerQueueConfigurationUpdateNotification GetServerQueueConfigurationUpdateNotification()
        {
            return new ServerQueueConfigurationUpdateNotification
            {
                FreeRotationAdditions = new Dictionary<CharacterType, RequirementCollection>(),
                GameTypeAvailabilies = GameModeManager.GetGameTypeAvailabilities(),
                TierInstanceNames = new List<LocalizationPayload>(),
                AllowBadges = true,
                NewPlayerPvPQueueDuration = 0
            };
        }

        private LobbyStatusNotification GetLobbyStatusNotification(PersistedAccountData account)
        {
            return new LobbyStatusNotification
            {
                AllowRelogin = false,
                ClientAccessLevel = AccessUtils.GetClientAccessLevel(account),
                ErrorReportRate = new TimeSpan(0, 3, 0),
                GameplayOverrides = GameConfig.GetGameplayOverrides(),
                HasPurchasedGame = true,
                PacificNow = DateTime.UtcNow, // TODO ?
                UtcNow = DateTime.UtcNow,
                ServerLockState = ServerLockState.Unlocked,
                ServerMessageOverrides = GetServerMessageOverrides()
            };
        }

        private static string FetchGithubPatchNotes()
        {
            if (LobbyConfiguration.GetPatchNotesCommitsUrl().IsNullOrEmpty())
            {
                return null;
            }

            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Evos/1.0)");
                var request = new HttpRequestMessage(HttpMethod.Get, LobbyConfiguration.GetPatchNotesCommitsUrl());
                var response = httpClient.Send(request);
                using var reader = new StreamReader(response.Content.ReadAsStream());
                string json = reader.ReadToEnd();
                JArray array = JArray.Parse(json);
                StringBuilder parsed = new StringBuilder();
                foreach (JObject obj in array)
                {
                    string sha = obj["sha"].ToString();
                    string author = obj["commit"]["author"]["name"].ToString();
                    string message = obj["commit"]["message"].ToString();
                    List<string> parts = message.Split('\n').ToList();
                    string title = parts[0];
                    parts.RemoveAt(0);
                    message = String.Join('\n', parts);
                    parsed.AppendLine($"<size=20>[{sha.Substring(0, 7)}] <color=#ff66ff>{author}</color></size>");
                    parsed.AppendLine($"<size=30><b>{title}</b></size>");
                    parsed.AppendLine($"{message}\n\n\n");
                }

                return parsed.ToString();
            }
            catch (Exception e)
            {
                log.Info($"Could not get github commits {e.Message}");
            }

            return null;
        }

        private ServerMessageOverrides GetServerMessageOverrides()
        {
            string adminMessage = AdminMessageManager.PopAdminMessage(AccountId);
            if (adminMessage is not null)
            {
                log.Info($"Sending admin message: {adminMessage}");
            }

            return new ServerMessageOverrides
            {
                MOTDPopUpText = adminMessage ?? GetMotdPopUpText(), // Popup message when client connects to lobby
                MOTDText = GetMotdText(), // "alert" text
                ReleaseNotesHeader = LobbyConfiguration.GetPatchNotesHeader(),
                ReleaseNotesDescription = LobbyConfiguration.GetPatchNotesDescription(),
                ReleaseNotesText = CachedPatchNotes.Value ?? LobbyConfiguration.GetPatchNotesText()
            };
        }

        private static ServerMessage GetMotdText()
        {
            if (DB.Get().MiscDao.GetEntry(EvosServerMessageType.MessageOfTheDay.ToString()) is ServerMessageEntry msg
                && !msg.Message.IsEmpty())
            {
                return msg.Message;
            }
            return LobbyConfiguration.GetMOTDText();
        }

        private static ServerMessage GetMotdPopUpText()
        {
            if (DB.Get().MiscDao.GetEntry(EvosServerMessageType.MessageOfTheDayPopup.ToString()) is ServerMessageEntry msg
                && !msg.Message.IsEmpty())
            {
                return msg.Message.FillMissingLocalizations(); // otherwise is just won't show if there is no loc for the active language
            }
            return LobbyConfiguration.GetMOTDPopUpText();
        }

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

            RegisterHandler<RegisterGameClientRequest>(HandleRegisterGame);
            RegisterHandler<ChatNotification>(HandleChatNotification);

            RegisterHandler<GroupChatRequest>(HandleGroupChatRequest);

            RegisterHandler<RankedHoverClickRequest>(HandlePlayerRankedHoverClickRequest);
            RegisterHandler<RankedBanRequest>(HandlePlayerRankedBanRequest);
            RegisterHandler<RankedSelectionRequest>(HandleRankedSelectionRequest);
            RegisterHandler<RankedTradeRequest>(HandleRankedTradeRequest);

            ILobbyModule[] modules = { new StoreModule(this), new TelemetryModule(this), new AccountModule(this), new GroupModule(this, _groupRegistry), new FriendModule(this), _matchmaking, _gameLifecycle, new CharacterModule(this, _matchmaking, _gameLifecycle) };
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

        public void HandleRegisterGame(RegisterGameClientRequest request)
        {
            if (request == null)
            {
                SendErrorResponse(new RegisterGameClientResponse(), 0, Messages.LoginFailed);
                CloseConnection();
                return;
            }

            try
            {
                SessionManager.OnPlayerConnect(this, request);

                log.Info(string.Format(Messages.LoginSuccess, this.UserName));
                LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(request.SessionInfo.AccountId);
                RegisterGameClientResponse response = new RegisterGameClientResponse
                {
                    AuthInfo = request.AuthInfo, // Send original, if some data is missing on a new instance the game fails
                    SessionInfo = sessionInfo,
                    ResponseId = request.RequestId
                };

                // Overwrite the values we need
                response.AuthInfo.Password = null;
                response.AuthInfo.AccountId = AccountId;
                response.AuthInfo.Handle = sessionInfo.Handle;
                response.AuthInfo.TicketData = new SessionTicketData
                {
                    AccountID = AccountId,
                    SessionToken = sessionInfo.SessionToken,
                    ReconnectionSessionToken = sessionInfo.ReconnectSessionToken
                }.ToStringWithSignature();

                Send(response);
                SendLobbyServerReadyNotification();

                // Send 'Connected to lobby server' notification to chat
                foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
                {
                    LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                    if (player != null && !player.IsInGame())
                    {
                        player.SendSystemMessage($"<link=name>{sessionInfo.Handle}</link> connected to lobby server");
                    }
                }
                
                DB.Get().UserMetadataDao.UpsertLastSession(AccountId, Proxy?.Name, sessionInfo.BuildVersionInfo);
            }
            catch (RegisterGameException e)
            {
                SendErrorResponse(new RegisterGameClientResponse(), request.RequestId, e);
                CloseConnection();
                return;
            }
            catch (Exception e)
            {
                SendErrorResponse(new RegisterGameClientResponse(), request.RequestId);
                log.Error("Exception while registering game client", e);
                CloseConnection();
                return;
            }
            BroadcastRefreshFriendList();
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
