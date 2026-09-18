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
using CharacterManager = EvoS.DirectoryServer.Character.CharacterManager;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : WebSocketBehaviorBase<WebSocketMessage>, IClientConnection, IHandlerRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));

        public long AccountId { get; set; }
        public string UserName { get; set; }
        public long SessionToken;
        public bool SessionCleaned = false; // tracks clean up methods execution for reconnection

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

        protected void SetAllyDifficulty(BotDifficulty difficulty) => _matchmaking.SetAllyDifficulty(difficulty);

        protected void SetEnemyDifficulty(BotDifficulty difficulty) => _matchmaking.SetEnemyDifficulty(difficulty);

        public ushort GetSubTypeMask() => _matchmaking.GetSubTypeMask();

        public PlayerOnlineStatus Status = PlayerOnlineStatus.Online;

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

        public LobbyServerProtocol()
        {
            _matchmaking = new MatchmakingModule(this);
            _gameLifecycle = new GameLifecycleModule(this);

            RegisterHandler<RegisterGameClientRequest>(HandleRegisterGame);
            RegisterHandler<PlayerUpdateStatusRequest>(HandlePlayerUpdateStatusRequest);
            RegisterHandler<PlayerInfoUpdateRequest>(HandlePlayerInfoUpdateRequest);
            RegisterHandler<ChatNotification>(HandleChatNotification);

            RegisterHandler<UseOverconRequest>(HandleUseOverconRequest);
            RegisterHandler<UseGGPackRequest>(HandleUseGGPackRequest);
            RegisterHandler<GroupChatRequest>(HandleGroupChatRequest);
            RegisterHandler<RejoinGameRequest>(HandleRejoinGameRequest);
            RegisterHandler<DEBUG_AdminSlashCommandNotification>(HandleDEBUG_AdminSlashCommandNotification);

            RegisterHandler<SubscribeToCustomGamesRequest>(HandleSubscribeToCustomGamesRequest);
            RegisterHandler<UnsubscribeFromCustomGamesRequest>(HandleUnsubscribeFromCustomGamesRequest);

            RegisterHandler<RankedHoverClickRequest>(HandlePlayerRankedHoverClickRequest);
            RegisterHandler<RankedBanRequest>(HandlePlayerRankedBanRequest);
            RegisterHandler<RankedSelectionRequest>(HandleRankedSelectionRequest);
            RegisterHandler<RankedTradeRequest>(HandleRankedTradeRequest);

            RegisterHandler<UpdateRemoteCharacterRequest>(HandleUpdateRemoteCharacterRequest);

            RegisterHandler<FriendUpdateRequest>(HandleFriendUpdate);

            ILobbyModule[] modules = { new StoreModule(this), new TelemetryModule(this), new AccountModule(this), new GroupModule(this), _matchmaking, _gameLifecycle };
            foreach (ILobbyModule module in modules)
            {
                module.Register(this);
            }
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

        private void HandleSelectRibbonRequest(SelectRibbonRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            if (account == null || !(account.AccountComponent.UnlockedRibbonIDs.Contains(request.RibbonID) || request.RibbonID == -1))
            {
                Send(new SelectRibbonResponse()
                {
                    Success = false,
                    ResponseId = request.RequestId,
                });
                return;
            }

            account.AccountComponent.SelectedRibbonID = request.RibbonID;
            DB.Get().AccountDao.UpdateAccountComponent(account);

            OnAccountVisualsUpdated();

            Send(new SelectRibbonResponse()
            {
                CurrentRibbonID = request.RibbonID,
                Success = true,
                ResponseId = request.RequestId,
            });
        }

        private void HandleUpdateRemoteCharacterRequest(UpdateRemoteCharacterRequest request)
        {
            UpdateRemoteCharacterResponse response = new UpdateRemoteCharacterResponse
            {
                ResponseId = request.RequestId,
                Success = false,
            };

            if (request.RemoteSlotIndexes.Length == 0 || request.Characters.Length == 0)
            {
                log.Warn("No characters or slots provided in the request.");
                Send(response);
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            int maxSlots = 3;
            int[] slots = request.RemoteSlotIndexes;
            CharacterType[] characterTypes = request.Characters;
            int totalSlots = Math.Min(slots.Length, characterTypes.Length);

            bool updated = UpdateCharacterSlots(account, slots, characterTypes, totalSlots, maxSlots);

            if (updated)
            {
                DB.Get().AccountDao.UpdateAccount(account);
                response.Success = true;
                PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification(account);
                Send(updateNotification);
            }

            Send(response);
        }

        private bool UpdateCharacterSlots(PersistedAccountData account, int[] slots, CharacterType[] characterTypes, int totalSlots, int maxSlots)
        {
            bool updated = false;

            for (int i = 0; i < totalSlots; i++)
            {
                int slotIndex = slots[i];
                CharacterType newCharacter = characterTypes[i];

                // Skip if slot exceeds the maximum allowed slots
                if (slotIndex >= maxSlots)
                {
                    log.Warn($"Slot index {slotIndex} exceeds the maximum allowed slots.");
                    continue;
                }

                // Never allow these
                if (newCharacter == CharacterType.PendingWillFill
                    || newCharacter == CharacterType.TestFreelancer1
                    || newCharacter == CharacterType.TestFreelancer2)
                {
                    Send(new ChatNotification
                    {
                        ConsoleMessageType = ConsoleMessageType.SystemMessage,
                        Text = "This character is not allowed (for draft purposes only)"
                    });
                    continue;
                }

                // Expand the LastRemoteCharacters list if necessary
                while (account.AccountComponent.LastRemoteCharacters.Count <= slotIndex)
                {
                    account.AccountComponent.LastRemoteCharacters.Add(CharacterType.None);
                }

                // Update if the character in the slot has changed
                if (account.AccountComponent.LastRemoteCharacters[slotIndex] != newCharacter)
                {
                    account.AccountComponent.LastRemoteCharacters[slotIndex] = newCharacter;
                    updated = true;
                    log.Info($"Updated slot {slotIndex} to character {newCharacter} for account {AccountId}.");
                }
            }

            return updated;
        }

        private void HandleDEBUG_AdminSlashCommandNotification(DEBUG_AdminSlashCommandNotification notification)
        {
            log.Info($"DEBUG_AdminSlashCommandNotification: {notification.Command}");
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (account == null)
            {
                return;
            }
            log.Info($"DEBUG_AdminSlashCommandNotification: dev={account.AccountComponent.IsDev()} devMode={EvosConfiguration.GetDevMode()}");
            if (account.AccountComponent.IsDev() || EvosConfiguration.GetDevMode())
            {
                Game game = GameManager.GetGameWithPlayer(AccountId);
                if (game != null)
                {
                    Team team = game.TeamInfo.TeamPlayerInfo.Find(x => x.AccountId == AccountId).TeamId;
                    switch (notification.Command)
                    {
                        case "End Game (Win)":

                            game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamAWon : GameResult.TeamBWon);
                            break;
                        case "End Game (Loss)":
                            game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamBWon : GameResult.TeamAWon);
                            break;
                        case "End Game (No Result)":
                        case "End Game (With Parameters)":
                        case "End Game (Tie)":
                            // End the game with a tie result for other specified commands
                            game.Server.AdminShutdown(GameResult.TieGame);
                            break;
                        case "Cooldowns":
                            game.Server.AdminClearCooldown();
                            break;
                    }
                }
                else
                {
                    log.Info("DEBUG_AdminSlashCommandNotification: game not found");
                }
            }
        }

        private void HandleSetDevTagRequest(SetDevTagRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (account == null)
            {
                return;
            }
            if (account.AccountComponent.IsDev())
            {
                account.AccountComponent.DisplayDevTag = request.active;
                Send(new SetDevTagResponse()
                {
                    Success = true,
                });
            }
            else
            {
                Send(new SetDevTagResponse()
                {
                    Success = false,
                });
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
            FriendManager.MarkForUpdate(this);
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

        public void HandleOptionsNotification(OptionsNotification notification)
        {
            HandleEvosOptionsNotification(EvosOptionsNotification.Of(notification));
        }

        public void HandleEvosOptionsNotification(EvosOptionsNotification notification)
        {
            DB.Get().UserMetadataDao.UpsertOptions(AccountId, notification);
        }

        public void HandleEvosOptionsNotificationLegacy(EvosOptionsNotificationLegacy notification)
        {
            DB.Get().UserMetadataDao.UpsertOptions(AccountId, notification.ToCurrent());
        }

        public void HandleCustomKeyBindNotification(CustomKeyBindNotification notification)
        {
            DB.Get().AccountDao.GetAccount(AccountId).AccountComponent.KeyCodeMapping = notification.CustomKeyBinds;
        }

        public void HandlePlayerUpdateStatusRequest(PlayerUpdateStatusRequest request)
        {
            log.Info($"{this.UserName} is now {request.StatusString}");
            PlayerUpdateStatusResponse response = FriendManager.OnPlayerUpdateStatusRequest(this, request);

            Send(response);
        }

        public void HandlePlayerMatchDataRequest(PlayerMatchDataRequest request)
        {
            PlayerMatchDataResponse response = new PlayerMatchDataResponse
            {
                MatchData = DB.Get().MatchHistoryDao.Find(AccountId),
                ResponseId = request.RequestId
            };
            Send(response);
        }

        public void HandlePlayerInfoUpdateRequest(PlayerInfoUpdateRequest request)
        {
            LobbyPlayerInfoUpdate update = request.PlayerInfoUpdate;
            LobbyServerPlayerInfo playerInfo = update.PlayerId == 0 ? PlayerInfo : CurrentGame?.GetPlayerById(update.PlayerId);
            bool updateSelectedCharacter = playerInfo == null && update.PlayerId == 0;

            PersistedAccountData account;
            if (playerInfo is not null)
            {
                account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);
            }
            else
            {
                account = DB.Get().AccountDao.GetAccount(AccountId);
                playerInfo = LobbyServerPlayerInfo.Of(account);
            }

            // TODO validate what player has purchased

            // building character info to validate it for current game
            CharacterType characterType = update.CharacterType ?? playerInfo.CharacterType;

            log.Debug($"HandlePlayerInfoUpdateRequest characterType={characterType} " +
                     $"(update={update.CharacterType} " +
                     $"server={playerInfo.CharacterType} " +
                     $"account={account?.AccountComponent.LastCharacter})");

            bool characterDataUpdate = false;
            CharacterComponent characterComponent;
            LobbyCharacterInfo characterInfo;
            if (account is not null)
            {
                characterComponent = (CharacterComponent)account.CharacterData[characterType].CharacterComponent.Clone();
                characterDataUpdate = ApplyCharacterDataUpdate(characterComponent, update);
                characterInfo = LobbyCharacterInfo.Of(account.CharacterData[characterType], characterComponent);
            }
            else
            {
                characterComponent = CharacterManager.GetCharacterComponent(0, characterType);
                ApplyCharacterDataUpdate(characterComponent, update);
                characterInfo = LobbyCharacterInfo.Of(new PersistedCharacterData(characterType), characterComponent);
            }

            if (CurrentGame != null)
            {
                if (!CurrentGame.UpdateCharacterInfo(AccountId, characterInfo, update))
                {
                    Send(new PlayerInfoUpdateResponse
                    {
                        Success = false,
                        ResponseId = request.RequestId
                    });
                    return;
                }
            }
            else
            {
                playerInfo.CharacterInfo = characterInfo;
            }

            // persisting changes
            if (account is not null && account.AccountId == AccountId)
            {
                if (updateSelectedCharacter && update.CharacterType.HasValue)
                {
                    account.AccountComponent.LastCharacter = update.CharacterType.Value;
                    DB.Get().AccountDao.UpdateLastCharacter(account);
                }

                if (characterDataUpdate)
                {
                    account.CharacterData[characterType].CharacterComponent = characterComponent;
                    DB.Get().AccountDao.UpdateCharacterComponent(account, characterType);
                }

                if (GroupManager.GetPlayerGroup(AccountId).IsSolo() && request.GameType != null && request.GameType.HasValue)
                {
                    SetGameType(request.GameType.Value);
                }

                // Unselect a dublicated remotecharacter to none if we pick it ourself
                // This always runs not sure i can catch the difrence between Coop/PvP and Custom
                // And Deathmatch vs ControlAllBots
                // Before we send PlayerAccountDataUpdateNotification
                int index = account.AccountComponent.LastRemoteCharacters.IndexOf(characterType);

                if (index != -1)
                {
                    account.AccountComponent.LastRemoteCharacters[index] = CharacterType.None;
                }

                // without this client instantly resets character type back to what it was
                if (update.CharacterType != null && update.CharacterType.HasValue)
                {
                    PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification(account);
                    Send(updateNotification);
                }

                if (update.AllyDifficulty != null && update.AllyDifficulty.HasValue)
                    SetAllyDifficulty(update.AllyDifficulty.Value);
                if (update.ContextualReadyState != null && update.ContextualReadyState.HasValue)
                    SetContextualReadyState(update.ContextualReadyState.Value);
                if (update.EnemyDifficulty != null && update.EnemyDifficulty.HasValue)
                    SetEnemyDifficulty(update.EnemyDifficulty.Value);
            }

            Send(new PlayerInfoUpdateResponse
            {
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
                CharacterInfo = account?.AccountId == AccountId ? playerInfo.CharacterInfo : null,
                OriginalPlayerInfoUpdate = update,
                ResponseId = request.RequestId
            });
            BroadcastRefreshGroup();
        }

        private static bool ApplyCharacterDataUpdate(
            CharacterComponent characterComponent,
            LobbyPlayerInfoUpdate update)
        {
            bool characterDataUpdate = false;

            if (update.CharacterSkin.HasValue)
            {
                characterComponent.LastSkin = update.CharacterSkin.Value;
                characterDataUpdate = true;
            }
            if (update.CharacterCards.HasValue)
            {
                characterComponent.LastCards = update.CharacterCards.Value;
                characterDataUpdate = true;
            }
            if (update.CharacterMods.HasValue)
            {
                characterComponent.LastMods = update.CharacterMods.Value;
                characterDataUpdate = true;
            }
            if (update.CharacterAbilityVfxSwaps.HasValue)
            {
                characterComponent.LastAbilityVfxSwaps = update.CharacterAbilityVfxSwaps.Value;
                characterDataUpdate = true;
            }
            if (update.CharacterLoadoutChanges.HasValue)
            {
                characterComponent.CharacterLoadouts = update.CharacterLoadoutChanges.Value.CharacterLoadoutChanges;
                characterDataUpdate = true;
            }
            if (update.LastSelectedLoadout.HasValue)
            {
                characterComponent.LastSelectedLoadout = update.LastSelectedLoadout.Value;
                characterDataUpdate = true;
            }

            return characterDataUpdate;
        }

        public void HandleCheckAccountStatusRequest(CheckAccountStatusRequest request)
        {
            CheckAccountStatusResponse response = new CheckAccountStatusResponse()
            {
                QuestOffers = new QuestOfferNotification() { OfferDailyQuest = false },
                ResponseId = request.RequestId
            };
            Send(response);

            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

                for (int i = 0; i < 3; i++)
                {
                    Send(new PlayerFactionContributionChangeNotification()
                    {
                        CompetitionId = 1,
                        FactionId = i,
                        AmountChanged = 0,
                        TotalXP = TrustWarManager.GetTotalXPByFactionID(account, i),
                        AccountID = account.AccountId,
                    });
                }
            }
        }

        public void HandleCheckRAFStatusRequest(CheckRAFStatusRequest request)
        {
            CheckRAFStatusResponse response = new CheckRAFStatusResponse()
            {
                ReferralCode = "sampletext",
                ResponseId = request.RequestId
            };
            Send(response);
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

        protected void SetContextualReadyState(ContextualReadyState contextualReadyState) =>
            _matchmaking.SetContextualReadyState(contextualReadyState);

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

        public void HandleSelectBannerRequest(SelectBannerRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            //  Modify the correct type of banner
            if (InventoryManager.BannerIsForeground(request.BannerID))
            {
                account.AccountComponent.SelectedForegroundBannerID = request.BannerID;
            }
            else
            {
                account.AccountComponent.SelectedBackgroundBannerID = request.BannerID;
            }

            // Update the account
            DB.Get().AccountDao.UpdateAccountComponent(account);

            OnAccountVisualsUpdated();

            // Send response
            Send(new SelectBannerResponse()
            {
                BackgroundBannerID = account.AccountComponent.SelectedBackgroundBannerID,
                ForegroundBannerID = account.AccountComponent.SelectedForegroundBannerID,
                ResponseId = request.RequestId
            });
        }

        public void HandleSelectTitleRequest(SelectTitleRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            if (account.AccountComponent.UnlockedTitleIDs.Contains(request.TitleID) || request.TitleID == -1)
            {
                account.AccountComponent.SelectedTitleID = request.TitleID;
                DB.Get().AccountDao.UpdateAccountComponent(account);

                OnAccountVisualsUpdated();
            }

            Send(new SelectTitleResponse
            {
                CurrentTitleID = account.AccountComponent.SelectedTitleID,
                ResponseId = request.RequestId
            });
        }

        public void HandleUseOverconRequest(UseOverconRequest request)
        {
            UseOverconResponse response = new UseOverconResponse()
            {
                ActorId = request.ActorId,
                OverconId = request.OverconId,
                ResponseId = request.RequestId
            };

            Send(response);

            if (CurrentGame != null)
            {
                response.ResponseId = 0;
                foreach (LobbyServerProtocol client in CurrentGame.GetClients())
                {
                    if (client.AccountId != AccountId)
                    {
                        client.Send(response);
                    }
                }
            }
        }

        public void HandleUseGGPackRequest(UseGGPackRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            UseGGPackResponse response = new UseGGPackResponse()
            {
                GGPackUserName = account.Handle,
                GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
                GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
                GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
                GGPackUserTitle = account.AccountComponent.SelectedTitleID,
                GGPackUserTitleLevel = 1,
                ResponseId = request.RequestId
            };
            Send(response);

            if (CurrentGame != null)
            {
                CurrentGame.OnPlayerUsedGGPack(AccountId);
                foreach (LobbyServerProtocol client in CurrentGame.GetClients())
                {
                    if (client.AccountId != AccountId)
                    {
                        UseGGPackNotification useGGPackNotification = new UseGGPackNotification()
                        {
                            GGPackUserName = account.Handle,
                            GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
                            GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
                            GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
                            GGPackUserTitle = account.AccountComponent.SelectedTitleID,
                            GGPackUserTitleLevel = 1,
                            NumGGPacksUsed = CurrentGame.GameInfo.ggPackUsedAccountIDs[AccountId]
                        };
                        client.Send(useGGPackNotification);
                    }

                }
            }
        }

        //Allows to get rid of the flashy New tag next to store for existing users
        public void HandleUpdateUIStateRequest(UpdateUIStateRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            log.Info($"Player {AccountId} requested UIState {request.UIState} {request.StateValue}");
            account.AccountComponent.UIStates[request.UIState] = request.StateValue;
            DB.Get().AccountDao.UpdateAccountComponent(account);
        }

        private void HandleSubscribeToCustomGamesRequest(SubscribeToCustomGamesRequest request)
        {
            CustomGameManager.Subscribe(this);
        }

        private void HandleUnsubscribeFromCustomGamesRequest(UnsubscribeFromCustomGamesRequest request)
        {
            CustomGameManager.Unsubscribe(this);
        }

        private void HandleSetRegionRequest(SetRegionRequest request)
        {
        }

        private void HandleLoadingScreenToggleRequest(LoadingScreenToggleRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            Dictionary<int, bool> bgs = account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState;
            if (bgs.ContainsKey(request.LoadingScreenId))
            {
                bgs[request.LoadingScreenId] = request.NewState;
                DB.Get().AccountDao.UpdateAccountComponent(account);
                Send(new LoadingScreenToggleResponse
                {
                    LoadingScreenId = request.LoadingScreenId,
                    CurrentState = request.NewState,
                    Success = true,
                    ResponseId = request.RequestId
                });
            }
            else
            {
                Send(new LoadingScreenToggleResponse
                {
                    LoadingScreenId = request.LoadingScreenId,
                    Success = false,
                    ResponseId = request.RequestId
                });
            }
        }

        private void HandleSendRAFReferralEmailsRequest(SendRAFReferralEmailsRequest request)
        {
            Send(new SendRAFReferralEmailsResponse
            {
                Success = false,
                ResponseId = request.RequestId
            });
        }

        public void HandleGroupChatRequest(GroupChatRequest request)
        {
            OnGroupChatRequest(this, request);
        }

        public void HandleRejoinGameRequest(RejoinGameRequest request)
        {
            if (request.PreviousGameInfo == null || request.Accept == false)
            {
                Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
                return;
            }

            log.Info($"{UserName} wants to reconnect to game {request.PreviousGameInfo.GameServerProcessCode}");

            Game game = GameManager.GetGameWithPlayer(AccountId);

            if (game == null || game.Server == null || !game.Server.IsConnected)
            {
                // no longer in a game
                Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
                log.Info($"Game {request.PreviousGameInfo.GameServerProcessCode} not found");
                return;
            }

            LobbyServerPlayerInfo playerInfo = game.GetPlayerInfo(AccountId);
            if (playerInfo == null)
            {
                // no longer in a game
                Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = false });
                log.Info($"{UserName} was not in game {request.PreviousGameInfo.GameServerProcessCode}");
                return;
            }

            Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = true });
            log.Info($"Reconnecting {UserName} to game {game.GameInfo.GameServerProcessCode} ({game.ProcessCode})");
            ResetReadyState();
            game.ReconnectPlayer(this);
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

        private void HandleFriendUpdate(FriendUpdateRequest request)
        {
            long friendAccountId = LobbyServerUtils.ResolveAccountId(request.FriendAccountId, request.FriendHandle);
            if (friendAccountId == 0)
            {
                string failure = FriendManager.GetFailTerm(request.FriendOperation);
                string context = failure != null ? "FriendList" : "Global";
                failure ??= "FailedMessage";
                Send(FriendUpdateResponse.of(
                    request,
                    LocalizationPayload.Create(failure, context,
                        LocalizationArg_LocalizationPayload.Create(
                            LocalizationPayload.Create("PlayerNotFound", "Invite",
                                LocalizationArg_Handle.Create(request.FriendHandle))))
                ));
                log.Info($"Attempted to {request.FriendOperation} {request.FriendHandle}#{request.FriendAccountId}, but such a user was not found");
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (friendAccountId == AccountId)
            {
                log.Info($"{account.Handle} attempted to {request.FriendOperation} themselves");
                Send(
                    FriendUpdateResponse.of(
                        request, 
                        LocalizationPayload.Create(
                            "CannotFriendYourself",
                            "FriendUpdateResponse")));
                return;
            }
            
            PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);

            if (account == null || friendAccount == null)
            {
                Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                log.Info($"Failed to find account {AccountId} and/or {friendAccountId}");
                return;
            }

            SocialComponent socialComponent = account.SocialComponent;
            switch (request.FriendOperation)
            {
                case FriendOperation.Block:
                {
                    bool updated = socialComponent.Block(friendAccountId);
                    log.Info($"{account.Handle} blocked {friendAccount.Handle}{(updated ? "" : ", but they were already blocked")}");
                    if (updated)
                    {
                        DB.Get().AccountDao.UpdateSocialComponent(account);
                        Send(FriendUpdateResponse.of(request));
                        RefreshFriendList();
                    }
                    else
                    {
                        Send(FriendUpdateResponse.of(
                            request,
                            LocalizationPayload.Create("FailedFriendBlock", "FriendList",
                                LocalizationArg_LocalizationPayload.Create(
                                    LocalizationPayload.Create("PlayerAlreadyBlocked", "FriendUpdateResponse",
                                        LocalizationArg_Handle.Create(request.FriendHandle))))
                        ));
                    }
                    return;
                }
                case FriendOperation.Unblock:
                {
                    Unblock(request, account, friendAccount);
                    return;
                }
                case FriendOperation.Add:
                {
                    if (socialComponent.FriendInfo.ContainsKey(friendAccountId))
                    {
                        log.Info($"{account.Handle} attempted to add {friendAccount.Handle} to friend list but they are already friends");
                        Send(
                            FriendUpdateResponse.of(
                                request, 
                                LocalizationPayload.Create(
                                    "PlayerAlreadyYourFriend",
                                    "FriendUpdateResponse",
                                    LocalizationArg_Handle.Create(friendAccount.Handle))));
                        return;
                    }

                    if (friendAccount.SocialComponent.GetOutgoingFriendRequests().Contains(AccountId))
                    {
                        log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list while having an incoming friend request from them");
                        Send(
                            FriendManager.AddFriend(AccountId, friendAccountId)
                                ? FriendUpdateResponse.of(request)
                                : FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    if (account.SocialComponent.GetOutgoingFriendRequests().Contains(friendAccountId))
                    {
                        log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list while already having an outgoing friend request to them. Ignoring");
                        Send(FriendUpdateResponse.of(request));
                        return;
                    }

                    log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list");
                    if (!FriendManager.AddFriendRequest(AccountId, friendAccountId))
                    {
                        Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    Send(FriendUpdateResponse.of(request));
                    return;
                }
                case FriendOperation.Remove:
                {
                    if (socialComponent.IsBlocked(friendAccountId)) // UI bug
                    {
                        if (!Unblock(request, account, friendAccount))
                        {
                            Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                            return;
                        }

                        return;
                    }
                    
                    if (socialComponent.FriendInfo.ContainsKey(friendAccountId))
                    {
                        log.Info($"{account.Handle} removed {friendAccount.Handle} from friend list");
                        if (!FriendManager.RemoveFriend(AccountId, friendAccountId))
                        {
                            Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                            return;
                        }
                        
                        Send(FriendUpdateResponse.of(request));
                        return;
                    }
                    
                    if (socialComponent.GetOutgoingFriendRequests().Contains(friendAccountId))
                    {
                        if (!FriendManager.RemoveFriendRequest(AccountId, friendAccountId))
                        {
                            log.Info($"{account.Handle} attempted to cancel their friend request to {friendAccount.Handle} but the request was not found");
                            Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                            return;
                        }
                        
                        log.Info($"{account.Handle} cancelled their friend request to {friendAccount.Handle}");
                        Send(FriendUpdateResponse.of(request));
                        return;
                    }
                    
                    if (socialComponent.GetIncomingFriendRequests().Contains(friendAccountId))
                    {
                        if (!FriendManager.RemoveFriendRequest(friendAccountId, AccountId))
                        {
                            log.Info($"{account.Handle} attempted to cancel friend request from {friendAccount.Handle} but the request was not found");
                            Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                            return;
                        }
                        
                        log.Info($"{account.Handle} cancelled friend request from {friendAccount.Handle}");
                        Send(FriendUpdateResponse.of(request));
                        return;
                    }
                    
                    log.Info($"{account.Handle} attempted to remove {friendAccount.Handle} from friend list but they aren't friends");
                    if (LobbyConfiguration.AreAllOnlineFriends() && LobbyServerUtils.IsVanilla(AccountId))
                    {
                        SendSystemMessage("We are all friends here. You cannot deny that.");
                        return;
                    }
                    
                    Send(
                        FriendUpdateResponse.of(
                            request, 
                            LocalizationPayload.Create(
                                "NotFriendsWithPlayer",
                                "FriendUpdateResponse",
                                LocalizationArg_Handle.Create(friendAccount.Handle))));
                    return;
                }
                case FriendOperation.Accept:
                {
                    if (!FriendManager.RemoveFriendRequest(friendAccountId, AccountId))
                    {
                        log.Info($"{account.Handle} attempted to accept friend request from {friendAccount.Handle} but the request was not found");
                        Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    if (!FriendManager.AddFriend(friendAccountId, AccountId))
                    {
                        log.Info($"{account.Handle} failed to accept friend request from {friendAccount.Handle}");
                        Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    log.Info($"{account.Handle} accepted friend request from {friendAccount.Handle}");
                    Send(FriendUpdateResponse.of(request));
                    return;
                }
                case FriendOperation.Reject:
                {
                    if (!FriendManager.RemoveFriendRequest(friendAccountId, AccountId))
                    {
                        log.Info($"{account.Handle} attempted to reject friend request from {friendAccount.Handle} but the request was not found");
                        Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }
                    
                    log.Info($"{account.Handle} rejected friend request from {friendAccount.Handle}");
                    Send(FriendUpdateResponse.of(request));
                    return;
                }
                case FriendOperation.Note:
                {
                    if (!FriendManager.AreFriends(AccountId, friendAccountId))
                    {
                        log.Error($"Failed to save {account.Handle}'s note for {friendAccount.Handle}: not a friend");
                        Send(
                            FriendUpdateResponse.of(
                                request, 
                                LocalizationPayload.Create(
                                    "NotFriendsWithPlayer",
                                    "FriendUpdateResponse",
                                    LocalizationArg_Handle.Create(friendAccount.Handle))));
                        return;
                    }
                    
                    if (!FriendManager.SetFriendNote(AccountId, friendAccountId, request.StringData))
                    {
                        log.Info($"Failed to save {account.Handle}'s note for {friendAccount.Handle}");
                        Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }
                    
                    log.Info($"{account.Handle} note for {friendAccount.Handle}: {
                        FriendManager.GetFriendNote(AccountId, friendAccountId)}");
                    Send(FriendUpdateResponse.of(request));
                    return;
                }
                default:
                {
                    log.Warn($"{account.Handle} attempted to {request.FriendOperation} {friendAccount.Handle}, " +
                             $"but this operation is not supported yet");
                    Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }
            }
        }

        private bool Unblock(FriendUpdateRequest request, PersistedAccountData account, PersistedAccountData friendAccount)
        {
            bool updated = account.SocialComponent.Unblock(friendAccount.AccountId);
            log.Info($"{account.Handle} unblocked {friendAccount.Handle}{(updated ? "" : ", but they weren't blocked")}");
            if (updated)
            {
                DB.Get().AccountDao.UpdateSocialComponent(account);
            }

            Send(FriendUpdateResponse.of(request));
            RefreshFriendList();
            return updated;
        }
    }
}
