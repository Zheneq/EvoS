using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Store;
using CentralServer.LobbyServer.TrustWar;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Exceptions;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using LobbyGameClientMessages;
using log4net;
using Newtonsoft.Json;
using Prometheus;
using WebSocketSharp;
using static EvoS.Framework.Misc.GameUtils;
using CharacterManager = EvoS.DirectoryServer.Character.CharacterManager;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : LobbyServerProtocolBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));

        private Game _currentGame;

        public PlayerOnlineStatus Status = PlayerOnlineStatus.Online;

        public Game CurrentGame
        {
            get => _currentGame;
            private set
            {
                if (_currentGame != value)
                {
                    _currentGame = value;
                    BroadcastRefreshFriendList();
                    BroadcastRefreshGroup();
                }
            }
        }

        public bool IsInGame() => CurrentGame != null;

        public bool IsInCharacterSelect() => CurrentGame != null && CurrentGame.GameStatus <= GameStatus.FreelancerSelecting;

        public bool IsInGroup() => !GroupManager.GetPlayerGroup(AccountId)?.IsSolo() ?? false;

        public int GetGroupSize() => GroupManager.GetPlayerGroup(AccountId)?.Members.Count ?? 1;

        public bool IsInQueue() => MatchmakingManager.IsQueued(GroupManager.GetPlayerGroup(AccountId));

        public bool IsReady { get; private set; }

        public LobbyServerPlayerInfo PlayerInfo => CurrentGame?.GetPlayerInfo(AccountId);

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

        public LobbyServerProtocol()
        {
            RegisterHandler<RegisterGameClientRequest>(HandleRegisterGame);
            RegisterHandler<OptionsNotification>(HandleOptionsNotification);
            RegisterHandler<CustomKeyBindNotification>(HandleCustomKeyBindNotification);
            RegisterHandler<PricesRequest>(HandlePricesRequest);
            RegisterHandler<PlayerUpdateStatusRequest>(HandlePlayerUpdateStatusRequest);
            RegisterHandler<PlayerMatchDataRequest>(HandlePlayerMatchDataRequest);
            RegisterHandler<SetGameSubTypeRequest>(HandleSetGameSubTypeRequest);
            RegisterHandler<PlayerInfoUpdateRequest>(HandlePlayerInfoUpdateRequest);
            RegisterHandler<PlayerGroupInfoUpdateRequest>(HandlePlayerGroupInfoUpdateRequest);
            RegisterHandler<CheckAccountStatusRequest>(HandleCheckAccountStatusRequest);
            RegisterHandler<CheckRAFStatusRequest>(HandleCheckRAFStatusRequest);
            RegisterHandler<PreviousGameInfoRequest>(HandlePreviousGameInfoRequest);
            RegisterHandler<PurchaseTintRequest>(HandlePurchaseTintRequest);
            RegisterHandler<LeaveGameRequest>(HandleLeaveGameRequest);
            RegisterHandler<JoinMatchmakingQueueRequest>(HandleJoinMatchmakingQueueRequest);
            RegisterHandler<LeaveMatchmakingQueueRequest>(HandleLeaveMatchmakingQueueRequest);
            RegisterHandler<ChatNotification>(HandleChatNotification);
            RegisterHandler<GroupInviteRequest>(HandleGroupInviteRequest);
            RegisterHandler<GroupJoinRequest>(HandleGroupJoinRequest);
            RegisterHandler<GroupConfirmationResponse>(HandleGroupConfirmationResponse);
            RegisterHandler<GroupSuggestionResponse>(HandleGroupSuggestionResponse);
            RegisterHandler<GroupLeaveRequest>(HandleGroupLeaveRequest);
            RegisterHandler<GroupKickRequest>(HandleGroupKickRequest);
            RegisterHandler<GroupPromoteRequest>(HandleGroupPromoteRequest);
            RegisterHandler<SelectBannerRequest>(HandleSelectBannerRequest);
            RegisterHandler<SelectTitleRequest>(HandleSelectTitleRequest);
            RegisterHandler<UseOverconRequest>(HandleUseOverconRequest);
            RegisterHandler<UseGGPackRequest>(HandleUseGGPackRequest);
            RegisterHandler<UpdateUIStateRequest>(HandleUpdateUIStateRequest);
            RegisterHandler<GroupChatRequest>(HandleGroupChatRequest);
            RegisterHandler<RejoinGameRequest>(HandleRejoinGameRequest);
            RegisterHandler<JoinGameRequest>(HandleJoinGameRequest);
            RegisterHandler<BalancedTeamRequest>(HandleBalancedTeamRequest);
            RegisterHandler<SetDevTagRequest>(HandleSetDevTagRequest);
            RegisterHandler<DEBUG_AdminSlashCommandNotification>(HandleDEBUG_AdminSlashCommandNotification);
            RegisterHandler<SelectRibbonRequest>(HandleSelectRibbonRequest);

            RegisterHandler<PurchaseModRequest>(HandlePurchaseModRequest);
            RegisterHandler<PurchaseTitleRequest>(HandlePurchaseTitleRequest);
            RegisterHandler<PurchaseTauntRequest>(HandlePurchaseTauntRequest);
            RegisterHandler<PurchaseChatEmojiRequest>(HandlePurchaseChatEmojiRequest);
            RegisterHandler<PurchaseLoadoutSlotRequest>(HandlePurchaseLoadoutSlotRequest);
            RegisterHandler<PaymentMethodsRequest>(HandlePaymentMethodsRequest);
            RegisterHandler<StoreOpenedMessage>(HandleStoreOpenedMessage);
            RegisterHandler<UIActionNotification>(HandleUIActionNotification);
            
            RegisterHandler<CrashReportArchiveNameRequest>(HandleCrashReportArchiveNameRequest);
            RegisterHandler<ClientStatusReport>(HandleClientStatusReport);
            RegisterHandler<ClientErrorSummary>(HandleClientErrorSummary);
            RegisterHandler<ClientErrorReport>(HandleClientErrorReport);
            RegisterHandler<ErrorReportSummaryResponse>(HandleErrorReportSummaryResponse);
            RegisterHandler<ClientFeedbackReport>(HandleClientFeedbackReport);
            RegisterHandler<ClientPerformanceReport>(HandleClientPerformanceReport);
            
            RegisterHandler<SubscribeToCustomGamesRequest>(HandleSubscribeToCustomGamesRequest);
            RegisterHandler<UnsubscribeFromCustomGamesRequest>(HandleUnsubscribeFromCustomGamesRequest);
            RegisterHandler<CreateGameRequest>(HandleCreateGameRequest);
            RegisterHandler<GameInfoUpdateRequest>(HandleGameInfoUpdateRequest);
            RegisterHandler<RankedLeaderboardOverviewRequest>(HandleRankedLeaderboardOverviewRequest);
            RegisterHandler<CalculateFreelancerStatsRequest>(HandleCalculateFreelancerStatsRequest);
            RegisterHandler<PlayerPanelUpdatedNotification>(HandlePlayerPanelUpdatedNotification);

            RegisterHandler<RankedHoverClickRequest>(HandlePlayerRankedHoverClickRequest);
            RegisterHandler<RankedBanRequest>(HandlePlayerRankedBanRequest);
            RegisterHandler<RankedSelectionRequest>(HandleRankedSelectionRequest);
            RegisterHandler<RankedTradeRequest>(HandleRankedTradeRequest);
            
            RegisterHandler<SetRegionRequest>(HandleSetRegionRequest);
            RegisterHandler<LoadingScreenToggleRequest>(HandleLoadingScreenToggleRequest);
            RegisterHandler<SendRAFReferralEmailsRequest>(HandleSendRAFReferralEmailsRequest);

            RegisterHandler<PurchaseBannerForegroundRequest>(HandlePurchaseEmblemRequest);
            RegisterHandler<PurchaseBannerBackgroundRequest>(HandlePurchaseBannerRequest);
            RegisterHandler<PurchaseAbilityVfxRequest>(HandlePurchasAbilityVfx);
            RegisterHandler<PurchaseInventoryItemRequest>(HandlePurchaseInventoryItemRequest);

            RegisterHandler<UpdateRemoteCharacterRequest>(HandleUpdateRemoteCharacterRequest);

            RegisterHandler<FriendUpdateRequest>(HandleFriendUpdate);
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
                Send(response); // TODO: error message
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
                    // TODO: error message!
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

        private void HandleBalancedTeamRequest(BalancedTeamRequest request)
        {
            bool success = CustomGameManager.BalanceTeams(AccountId, request.Slots);
            Send(new BalancedTeamResponse
            {
                Success = success,
                ResponseId = request.RequestId,
                Slots = request.Slots
            });
        }

        private void HandleJoinGameRequest(JoinGameRequest joinGameRequest)
        {
            ResetReadyState();
            Game game = CustomGameManager.JoinGame(
                AccountId,
                joinGameRequest.GameServerProcessCode,
                joinGameRequest.AsSpectator,
                out LocalizationPayload failure);
            if (game == null)
            {
                Send(new JoinGameResponse
                {
                    ResponseId = joinGameRequest.RequestId,
                    LocalizedFailure = failure,
                    Success = false
                });
                return;
            }

            JoinGame(game);
            Send(new JoinGameResponse
            {
                ResponseId = joinGameRequest.RequestId
            });
        }

        private void HandleGameInfoUpdateRequest(GameInfoUpdateRequest gameInfoUpdateRequest)
        {
            Game game = CustomGameManager.GetMyGame(AccountId);

            if (game.GameSubType.Mods.Contains(GameSubType.SubTypeMods.RankedFreelancerSelection)) {
                List<LobbyPlayerInfo> hasControllingPlayerId = gameInfoUpdateRequest.TeamInfo.TeamPlayerInfo.FindAll(p => p.ControllingPlayerId != 0);
                if (hasControllingPlayerId.Count > 0) {
                    bool success1 = CustomGameManager.BalanceTeams(AccountId, new List<BalanceTeamSlot>());
                    Send(new BalancedTeamResponse
                    {
                        Success = success1,
                        ResponseId = gameInfoUpdateRequest.RequestId,
                        Slots = new List<BalanceTeamSlot>()
                    });
                    Send(new ChatNotification
                    {
                        ConsoleMessageType = ConsoleMessageType.SystemMessage,
                        Text = "Controlling multiple characters is not allowed in this mode. "
                               + "If you want to control multiple characters, please select Deathmatch mode. "
                               + "Normal bots are allowed, however."
                    });
                    return;
                }
            }

            bool success = CustomGameManager.UpdateGameInfo(AccountId, gameInfoUpdateRequest.GameInfo, gameInfoUpdateRequest.TeamInfo);

            Send(new GameInfoUpdateResponse
            {
                Success = success,
                ResponseId = gameInfoUpdateRequest.RequestId,
                GameInfo = game?.GameInfo,
                TeamInfo = LobbyTeamInfo.FromServer(game?.TeamInfo, 0, new MatchmakingQueueConfig()),
            });
        }

        private void HandleCreateGameRequest(CreateGameRequest createGameRequest)
        {
            ResetReadyState();
            Game game = CustomGameManager.CreateGame(AccountId, createGameRequest.GameConfig, out LocalizationPayload error);
            if (game == null)
            {
                Send(new CreateGameResponse
                {
                    ResponseId = createGameRequest.RequestId,
                    LocalizedFailure = error,
                    Success = false,
                    AllowRetry = true,
                });
                return;
            }
            GroupManager.GetPlayerGroup(AccountId).Members
                .ForEach(groupMember => SessionManager.GetClientConnection(groupMember)?.JoinGame(game));
            Send(new CreateGameResponse
            {
                ResponseId = createGameRequest.RequestId,
                AllowRetry = true,
            });
        }

        protected override void HandleClose(CloseEventArgs e)
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

        public void JoinGame(Game game)
        {
            Game prevServer = CurrentGame;
            CurrentGame = game;
            log.Info($"{LobbyServerUtils.GetHandle(AccountId)} joined {game?.ProcessCode} (was in {prevServer?.ProcessCode ?? "lobby"})");
        }

        public bool LeaveGame(Game game)
        {
            if (game == null)
            {
                log.Error($"{AccountId} is asked to leave null server (current server = {CurrentGame?.ProcessCode ?? "null"})");
                return true;
            }
            if (CurrentGame == null)
            {
                log.Debug($"{AccountId} is asked to leave {game.ProcessCode} while they are not on any server");
                return true;
            }
            if (CurrentGame != game)
            {
                log.Debug($"{AccountId} is asked to leave {game.ProcessCode} while they are on {CurrentGame.ProcessCode}. Ignoring.");
                return false;
            }

            CurrentGame = null;
            log.Info($"{LobbyServerUtils.GetHandle(AccountId)} leaves {game.ProcessCode}");

            // forcing catalyst panel update -- otherwise it would show catas for the character from the last game
            Send(new ForcedCharacterChangeFromServerNotification
            {
                ChararacterInfo = DB.Get().AccountDao.GetAccount(AccountId).GetCharacterInfo(),
            });

            return true;
        }

        public void BroadcastRefreshFriendList()
        {
            FriendManager.MarkForUpdate(this);
        }

        public void RefreshFriendList()
        {
            Send(FriendManager.GetFriendStatusNotification(AccountId));
        }

        public void UpdateGroupReadyState()
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            if (group == null)
            {
                log.Error($"Attempted to update group ready state of {AccountId} who is not in a group");
                return;
            }
            LobbyServerProtocol leader = null;
            bool allAreReady = true;
            foreach (long groupMember in group.Members)
            {
                LobbyServerProtocol conn = SessionManager.GetClientConnection(groupMember);
                allAreReady &= conn?.IsReady ?? false;
                if (group.IsLeader(groupMember))
                {
                    leader = conn;
                }
            }

            bool isGroupQueued = MatchmakingManager.IsQueued(group);

            if (allAreReady && !isGroupQueued)
            {
                if (leader == null)
                {
                    log.Error($"Attempted to update group {group.GroupId} ready state with not connected leader {group.Leader}");
                    return;
                }
                MatchmakingManager.AddGroupToQueue(leader.SelectedGameType, group);
            }
            else if (!allAreReady && isGroupQueued)
            {
                MatchmakingManager.RemoveGroupFromQueue(group, true);
            }
        }

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
                IsReady = false;
                UpdateGroupReadyState();
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

        private void HandleGroupPromoteRequest(GroupPromoteRequest request)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            //Sadly message.AccountId returns 0 so look it up by name/handle
            long? accountId = SessionManager.GetOnlinePlayerByHandleOrUsername(request.Name);

            GroupPromoteResponse response = new GroupPromoteResponse
            {
                ResponseId = request.RequestId,
                Success = false
            };

            if (group.IsSolo())
            {
                response.LocalizedFailure = GroupMessages.NotInGroupMember;
            }
            else if (!group.IsLeader(AccountId))
            {
                response.LocalizedFailure = GroupMessages.NotTheLeader;
            }
            else if (AccountId == accountId)
            {
                response.LocalizedFailure = GroupMessages.AlreadyTheLeader;
            }
            else if (accountId.HasValue && GroupManager.PromoteMember(group, (long)accountId))
            {
                response.Success = true;
                BroadcastRefreshGroup();
            }
            else
            {
                response.LocalizedFailure = GroupMessages.PlayerIsNotInGroup(request.Name);
            }

            Send(response);
        }

        private void HandleGroupKickRequest(GroupKickRequest request)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            GroupKickResponse response = new GroupKickResponse
            {
                ResponseId = request.RequestId,
                MemberName = request.MemberName,
            };
            if (group is null || group.IsSolo())
            {
                response.LocalizedFailure = GroupMessages.NotInGroupMember;
                response.Success = false;
            }
            else if (!group.IsLeader(AccountId))
            {
                response.LocalizedFailure = GroupMessages.NotTheLeader;
                response.Success = false;
            }
            else
            {
                long? accountId = SessionManager.GetOnlinePlayerByHandleOrUsername(request.MemberName);
                if (!accountId.HasValue || !group.Members.Contains(accountId.Value))
                {
                    response.Success = false;
                }
                else
                {
                    response.Success = GroupManager.LeaveGroup(accountId.Value, false, true);
                }
                if (!response.Success)
                {
                    response.LocalizedFailure = GroupMessages.PlayerIsNotInGroup(request.MemberName);
                }
                else if (accountId.HasValue)
                {
                    GroupManager.BroadcastSystemMessage(group, GroupMessages.MemberKickedFromGroup(accountId.Value));
                }
            }
            Send(response);
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
        }

        public void HandleCustomKeyBindNotification(CustomKeyBindNotification notification)
        {
            DB.Get().AccountDao.GetAccount(AccountId).AccountComponent.KeyCodeMapping = notification.CustomKeyBinds;
        }

        public void HandlePricesRequest(PricesRequest request)
        {
            PricesResponse response = StoreManager.GetPricesResponse();
            response.ResponseId = request.RequestId;
            Send(response);
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

        public void HandleSetGameSubTypeRequest(SetGameSubTypeRequest request)
        {
            // SubType update comes before GameType update in PlayerInfoUpdateRequest
            SelectedSubTypeMask = request.SubTypeMask;
            Send(new SetGameSubTypeResponse { ResponseId = request.RequestId });
        }

        public void HandlePlayerGroupInfoUpdateRequest(PlayerGroupInfoUpdateRequest request)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            if (!group.IsLeader(AccountId))
            {
                Send(new PlayerGroupInfoUpdateResponse
                {
                    Success = false,
                    LocalizedFailure = GroupMessages.NotTheLeader,
                    ResponseId = request.RequestId
                });
                return;
            }

            foreach (long accountId in group.Members)
            {
                SessionManager.GetClientConnection(accountId)?.SetGameType(request.GameType);
            }

            Send(new PlayerGroupInfoUpdateResponse
            {
                Success = true,
                ResponseId = request.RequestId
            });
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

        public void HandlePreviousGameInfoRequest(PreviousGameInfoRequest request)
        {
            Game game = GameManager.GetGameWithPlayer(AccountId);
            LobbyGameInfo lobbyGameInfo = null;

            if (game != null && game.Server != null && game.Server.IsConnected)
            {
                if (!game.GetPlayerInfo(AccountId).ReplacedWithBots)
                {
                    game.DisconnectPlayer(AccountId);
                    log.Info($"{LobbyServerUtils.GetHandle(AccountId)} was in game {game.ProcessCode}, requesting disconnect");
                }
                else
                {
                    log.Info($"{LobbyServerUtils.GetHandle(AccountId)} was in game {game.ProcessCode}");
                }
                lobbyGameInfo = game.GameInfo;
            }
            else
            {
                log.Info($"{LobbyServerUtils.GetHandle(AccountId)} wasn't in any game");
            }

            PreviousGameInfoResponse response = new PreviousGameInfoResponse
            {
                PreviousGameInfo = lobbyGameInfo,
                ResponseId = request.RequestId
            };
            Send(response);
        }

        public void HandlePurchaseTintRequest(PurchaseTintRequest request)
        {
            Console.WriteLine("PurchaseTintRequest " + JsonConvert.SerializeObject(request));

            PurchaseTintResponse response = new PurchaseTintResponse()
            {
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                CharacterType = request.CharacterType,
                SkinId = request.SkinId,
                TextureId = request.TextureId,
                TintId = request.TintId,
                ResponseId = request.RequestId
            };
            Send(response);

            SkinHelper sk = new SkinHelper();
            sk.AddSkin(request.CharacterType, request.SkinId, request.TextureId, request.TintId);
            sk.Save();
        }

        public void HandleLeaveGameRequest(LeaveGameRequest request)
        {
            Game game = CurrentGame;
            log.Info($"{AccountId} leaves game {game?.ProcessCode}");
            if (game != null)
            {
                LeaveGame(game);
                game.DisconnectPlayer(AccountId);
            }
            Send(new LeaveGameResponse
            {
                Success = true,
                ResponseId = request.RequestId
            });
            Send(new GameStatusNotification
            {
                GameServerProcessCode = game?.ProcessCode,
                GameStatus = GameStatus.Stopped
            });
            SendGameUnassignmentNotification();
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

        protected void SetContextualReadyState(ContextualReadyState contextualReadyState)
        {
            log.Info($"SetContextualReadyState {contextualReadyState.ReadyState} {contextualReadyState.GameProcessCode}");

            LocalizationPayload failure = QueuePenaltyManager.CheckQueuePenalties(AccountId, SelectedGameType);
            if (failure is not null)
            {
                ResetReadyState();
                SendSystemMessage(failure);
                return;
            }

            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            IsReady = contextualReadyState.ReadyState == ReadyState.Ready;  // TODO can be Accepted and others
            if (group == null)
            {
                log.Error($"{LobbyServerUtils.GetHandle(AccountId)} is not in a group when setting contextual ready state");
                return;
            }
            if (CurrentGame != null)
            {
                if (CurrentGame.ProcessCode != contextualReadyState.GameProcessCode)
                {
                    log.Error($"Received ready state {contextualReadyState.ReadyState} " +
                              $"from {LobbyServerUtils.GetHandle(AccountId)} " +
                              $"for game {contextualReadyState.GameProcessCode} " +
                              $"while they are in game {CurrentGame.ProcessCode}");
                    return;
                }

                if (contextualReadyState.ReadyState == ReadyState.Ready) // TODO can be Accepted and others
                {
                    CurrentGame.SetPlayerReady(AccountId);
                }
                else
                {
                    CurrentGame.SetPlayerUnReady(AccountId);
                }
            }
            else
            {
                UpdateGroupReadyState();
                BroadcastRefreshGroup();
            }
        }

        private void ResetReadyState()
        {
            IsReady = false;
            UpdateGroupReadyState();
        }

        public void HandleJoinMatchmakingQueueRequest(JoinMatchmakingQueueRequest request)
        {
            try
            {
                GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
                if (!group.IsLeader(AccountId))
                {
                    log.Warn($"{UserName} attempted to join {request.GameType} queue " +
                             $"while not being the leader of their group");
                    Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
                    return;
                }

                LocalizationPayload failure = QueuePenaltyManager.CheckQueuePenalties(AccountId, SelectedGameType);
                if (failure is not null)
                {
                    Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId, LocalizedFailure = failure });
                    return;
                }

                IsReady = true;
                MatchmakingManager.AddGroupToQueue(request.GameType, group);
                Send(new JoinMatchmakingQueueResponse { Success = true, ResponseId = request.RequestId });
            }
            catch (Exception e)
            {
                Send(new JoinMatchmakingQueueResponse
                {
                    Success = false,
                    ResponseId = request.RequestId,
                    LocalizedFailure = LocalizationPayload.Create("ServerError@Global")
                });
                log.Error("Failed to process join queue request", e);
            }
        }

        public void HandleLeaveMatchmakingQueueRequest(LeaveMatchmakingQueueRequest request)
        {
            try
            {
                GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
                if (!group.IsLeader(AccountId))
                {
                    log.Warn($"{UserName} attempted to leave queue " +
                             $"while not being the leader of their group");
                    Send(new LeaveMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
                    return;
                }

                Send(new LeaveMatchmakingQueueResponse { Success = true, ResponseId = request.RequestId });
                IsReady = false;
                MatchmakingManager.RemoveGroupFromQueue(group);
            }
            catch (Exception e)
            {
                Send(new LeaveMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
                log.Error("Failed to process leave queue request", e);
            }
        }


        public void HandleChatNotification(ChatNotification notification)
        {
            OnChatNotification(this, notification);
        }

        public void HandleGroupInviteRequest(GroupInviteRequest request)
        {
            // Clean the recipient handle by removing (mentor icon) and (Dev) tag
            request.FriendHandle = Regex.Replace(request.FriendHandle, @"\p{C}|\(.*?\)", "");

            var response = new GroupInviteResponse
            {
                FriendHandle = request.FriendHandle,
                ResponseId = request.RequestId,
                Success = false
            };

            long friendAccountId = LobbyServerUtils.ResolveAccountId(0, request.FriendHandle);
            if (friendAccountId == 0)
            {
                log.Info($"Failed to find player {request.FriendHandle} to invite to a group");
                response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
                Send(response);
                return;
            }

            if (friendAccountId == AccountId)
            {
                log.Info($"{Handle} attempted to invite themself to a group");
                response.LocalizedFailure = GroupMessages.CantInviteYourself;
                Send(response);
                return;
            }

            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            if (group.Members.Contains(friendAccountId))
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} to a group when they are already there");
                response.LocalizedFailure = GroupMessages.AlreadyInYourGroup(friendAccountId);
                Send(response);
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            SocialComponent socialComponent = account?.SocialComponent;
            PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);
            SocialComponent friendSocialComponent = friendAccount?.SocialComponent;
            PersistedAccountData leaderAccount = DB.Get().AccountDao.GetAccount(group.Leader);

            if (account is null || friendAccount is null || leaderAccount is null)
            {
                log.Error($"Failed to send group invite request: "
                          + $"account={account?.Handle} "
                          + $"friendAccount={friendAccount?.Handle}.");
                Send(response);
                return;
            }

            if (socialComponent?.IsBlocked(friendAccountId) == true)
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} whom they blocked to a group");
                response.LocalizedFailure = GroupMessages.YouAreBlocking(friendAccountId);
                Send(response);
                return;
            }

            if (friendSocialComponent?.IsBlocked(AccountId) == true)
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} who blocked them to a group");
                response.Success = true; // shadow ban
                Send(response);
                return;
            }

            LobbyServerProtocol friend = SessionManager.GetClientConnection(friendAccountId);
            if (friend is null) // offline
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} who is offline to a group");
                response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
                Send(response);
                return;
            }

            if (group.Members.Count == LobbyConfiguration.GetMaxGroupSize())
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} into a full group");
                response.LocalizedFailure = GroupMessages.MemberFailedToJoinGroupIsFull(request.FriendHandle);
                Send(response);
                return;
            }

            GroupInfo friendGroup = GroupManager.GetPlayerGroup(friendAccountId);
            if (!friendGroup.IsSolo())
            {
                log.Info($"{Handle} attempted to invite {request.FriendHandle} who is already in a group");
                response.LocalizedFailure = GroupMessages.OtherPlayerInOtherGroup(request.FriendHandle);
                Send(response);
                return;
            }

            // TODO GROUPS AleadyInvitedPlayerToGroup@Invite? You've already invited {0}, please await their response.

            TimeSpan expirationTime = LobbyConfiguration.GetGroupConfiguration().InviteTimeout;
            if (group.Leader == AccountId)
            {
                GroupConfirmationRequest.JoinType joinType = GroupConfirmationRequest.JoinType.InviteToFormGroup;
                friend.Send(new GroupConfirmationRequest
                {
                    GroupId = group.GroupId,
                    LeaderName = account.UserName,
                    LeaderFullHandle = account.Handle,
                    JoinerName = friendAccount.Handle,
                    JoinerAccountId = friendAccount.AccountId,
                    ConfirmationNumber = GroupManager.CreateGroupRequest(
                        AccountId, friendAccount.AccountId, group.GroupId, joinType, expirationTime),
                    ExpirationTime = expirationTime,
                    Type = joinType
                });
                if (EvosConfiguration.GetPingOnGroupRequest() && !friend.IsInGroup() && !friend.IsInGame())
                {
                    friend.Send(new ChatNotification
                    {
                        SenderAccountId = AccountId,
                        SenderHandle = account.Handle,
                        ConsoleMessageType = ConsoleMessageType.WhisperChat,
                        LocalizedText = LocalizationPayload.Create("GroupRequest", "Global")
                    });
                }

                log.Info($"{AccountId}/{account.Handle} invited {friend.AccountId}/{request.FriendHandle} to group {group.GroupId}");
                response.Success = true;
                Send(response);

                GroupManager.BroadcastSystemMessage(
                    group,
                    GroupMessages.InvitedFriendToGroup(friendAccount.AccountId),
                    AccountId);
            }
            else
            {
                LobbyServerProtocol leaderSession = SessionManager.GetClientConnection(leaderAccount.AccountId);
                leaderSession.Send(new GroupSuggestionRequest
                {
                    LeaderAccountId = group.Leader,
                    SuggestedAccountFullHandle = request.FriendHandle,
                    SuggesterAccountName = account.Handle,
                    SuggesterAccountId = AccountId,
                });
                GroupManager.BroadcastSystemMessage(
                    group,
                    GroupMessages.InviteToGroupWithYou(AccountId, friendAccount.AccountId),
                    AccountId);
            }
        }

        public void HandleGroupJoinRequest(GroupJoinRequest request)
        {
            var response = new GroupJoinResponse
            {
                FriendHandle = request.FriendHandle,
                ResponseId = request.RequestId,
                Success = false
            };

            GroupInfo myGroup = GroupManager.GetPlayerGroup(AccountId);
            if (!myGroup.IsSolo())
            {
                log.Info($"{Handle} attempted to join {request.FriendHandle}'s group while being in another group.");
                response.LocalizedFailure = GroupMessages.CantJoinIfInGroup;
                Send(response);
                return;
            }

            long friendAccountId = SessionManager.GetOnlinePlayerByHandle(request.FriendHandle) ?? 0;
            if (friendAccountId == 0)
            {
                log.Info($"Failed to find player {request.FriendHandle} to request to join their group.");
                response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
                Send(response);
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            SocialComponent socialComponent = account?.SocialComponent;
            PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);
            SocialComponent friendSocialComponent = friendAccount?.SocialComponent;
            PersistedAccountData leaderAccount = DB.Get().AccountDao.GetAccount(myGroup.Leader);
            SocialComponent leaderSocialComponent = leaderAccount?.SocialComponent;

            if (account is null || friendAccount is null || leaderAccount is null)
            {
                log.Error($"Failed to send join group request: "
                          + $"account={account?.Handle} "
                          + $"friendAccount={friendAccount?.Handle} "
                          + $"leaderAccount={leaderAccount?.Handle}.");
                Send(response);
                return;
            }

            if (socialComponent?.IsBlocked(friendAccountId) == true)
            {
                log.Info($"{Handle} attempted to join {request.FriendHandle}'s group whom they blocked");
                response.LocalizedFailure = GroupMessages.YouAreBlocking(friendAccountId);
                Send(response);
                return;
            }

            if (friendSocialComponent?.IsBlocked(AccountId) == true)
            {
                log.Info($"{Handle} attempted to join {request.FriendHandle}'s group who blocked them");
                response.Success = true; // shadow ban
                Send(response);
                return;
            }

            if (leaderSocialComponent?.IsBlocked(AccountId) == true)
            {
                log.Info($"{Handle} attempted to join {leaderAccount.Handle}'s group who blocked them via {request.FriendHandle}");
                response.Success = true; // shadow ban
                Send(response);
                return;
            }

            GroupInfo friendGroup = GroupManager.GetPlayerGroup(friendAccountId);
            if (friendGroup.IsSolo())
            {
                log.Info($"{Handle} attempted to join {request.FriendHandle}'s ({friendAccountId}) group while they are solo.");
                response.LocalizedFailure = GroupMessages.OtherPlayerNotInGroup(friendAccountId);
                Send(response);
                return;
            }

            LobbyServerProtocol friend = SessionManager.GetClientConnection(friendAccountId);
            if (friend is null) // offline
            {
                log.Info($"{Handle} attempted to join {request.FriendHandle}'s group who is offline");
                response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
                Send(response);
                return;
            }

            if (friendGroup.Members.Count == LobbyConfiguration.GetMaxGroupSize())
            {
                log.Warn($"{AccountId} attempted to join {request.FriendHandle}'s full group");
                response.LocalizedFailure = GroupMessages.FailedToJoinGroupIsFull;
                Send(response);
                return;
            }

            TimeSpan expirationTime = LobbyConfiguration.GetGroupConfiguration().InviteTimeout;
            GroupConfirmationRequest.JoinType joinType = GroupConfirmationRequest.JoinType.RequestToJoinGroup;
            LobbyServerProtocol leaderSession = SessionManager.GetClientConnection(friendGroup.Leader);
            leaderSession.Send(new GroupConfirmationRequest
            {
                GroupId = friendGroup.GroupId,
                LeaderName = account.UserName,
                LeaderFullHandle = account.Handle,
                JoinerName = account.Handle,
                JoinerAccountId = AccountId,
                ConfirmationNumber = GroupManager.CreateGroupRequest(
                    AccountId, friendGroup.Leader, friendGroup.GroupId, joinType, expirationTime),
                ExpirationTime = expirationTime,
                Type = joinType
            });
            GroupManager.BroadcastSystemMessage(
                friendGroup,
                GroupMessages.RequestToJoinGroup(AccountId),
                leaderAccount.AccountId);

            response.Success = true;
            Send(response);
        }

        public void HandleGroupSuggestionResponse(GroupSuggestionResponse response)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            if (group is null)
            {
                return;
            }

            if (response.SuggestionStatus == GroupSuggestionResponse.Status.Denied)
            {
                GroupManager.BroadcastSystemMessage(
                    group,
                    GroupMessages.LeaderRejectedSuggestion); // no param for response.SuggesterAccountId
            }
            // nothing else to say as we don't know who was suggested
        }

        public void HandleGroupConfirmationResponse(GroupConfirmationResponse response)
        {
            GroupInfo myGroup = GroupManager.GetPlayerGroup(AccountId);
            GroupRequestInfo groupRequestInfo = GroupManager.PopGroupRequest(response.ConfirmationNumber);

            if (groupRequestInfo is null)
            {
                log.Error($"Player {AccountId} responded to not found request {response.ConfirmationNumber} "
                          + $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
                if (response.GroupId == myGroup.GroupId)
                {
                    SendSystemMessage(GroupMessages.MemberFailedToJoinGroupInviteExpired(response.JoinerAccountId));
                }
                else
                {
                    SendSystemMessage(GroupMessages.FailedToJoinGroupInviteExpired(response.JoinerAccountId));
                }
                return;
            }

            string typeForLog = groupRequestInfo.IsInvitation
                ? "invitation"
                : "request";
            if (groupRequestInfo.RequesteeAccountId != AccountId)
            {
                log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId}: {response.Acceptance}");
                SendSystemMessage(GroupMessages.FailedToJoinUnknownError);
                return;
            }

            LobbyServerProtocol requester = SessionManager.GetClientConnection(groupRequestInfo.RequesterAccountId);
            if (requester is null)
            {
                log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId} who is offline");
                if (groupRequestInfo.IsInvitation)
                {
                    SendSystemMessage(GroupMessages.FailedToJoinGroupCreatorOffline);
                }
                else
                {
                    GroupManager.BroadcastSystemMessage(
                        myGroup,
                        GroupMessages.MemberFailedToJoinGroupPlayerNotFound(groupRequestInfo.RequesterAccountId));
                }
                return;
            }

            GroupInfo requesterGroup = GroupManager.GetPlayerGroup(groupRequestInfo.RequesterAccountId);
            if (groupRequestInfo.IsInvitation)
            {
                if (groupRequestInfo.GroupId != requesterGroup.GroupId)
                {
                    log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                             + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                             + $"by {response.JoinerAccountId} who is already in another group");
                    SendSystemMessage(GroupMessages.FailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                    return;
                }

                if (!myGroup.IsSolo())
                {
                    log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                             + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                             + $"by {response.JoinerAccountId} but they are already in a group");
                    SendSystemMessage(GroupMessages.FailedToJoinGroupCantJoinIfInGroup);
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(AccountId));
                    return;
                }
            }
            else
            {
                if (groupRequestInfo.GroupId != myGroup.GroupId)
                {
                    log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                             + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                             + $"by {response.JoinerAccountId} but they are already in another group");
                    requester.SendSystemMessage(GroupMessages.FailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                    SendSystemMessage(GroupMessages.MemberFailedToJoinGroupInviteExpired(groupRequestInfo.RequesterAccountId));
                    return;
                }

                if (!requesterGroup.IsSolo())
                {
                    log.Info($"Player {AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                             + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                             + $"by {response.JoinerAccountId} who is already in a group");
                    SendSystemMessage(GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                    return;
                }
            }

            if (response.Acceptance != GroupInviteResponseType.PlayerAccepted)
            {
                log.Info($"Player {AccountId} rejected {typeForLog} {response.ConfirmationNumber} " +
                         $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
            }
            else
            {
                log.Info($"Player {AccountId} accepted {typeForLog} {response.ConfirmationNumber} " +
                         $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
            }

            switch (response.Acceptance)
            {
                case GroupInviteResponseType.PlayerRejected:
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.RejectedGroupInvite(AccountId));
                    break;
                case GroupInviteResponseType.OfferExpired:
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        groupRequestInfo.IsInvitation
                            ? GroupMessages.JoinGroupOfferExpired(AccountId)
                            : GroupMessages.FailedToJoinGroupInviteExpired(AccountId));
                    break;
                case GroupInviteResponseType.RequestorSpamming:
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.AlreadyRejectedInvite(AccountId));
                    break;
                case GroupInviteResponseType.PlayerInCustomMatch:
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.PlayerInACustomMatchAtTheMoment(AccountId));
                    break;
                case GroupInviteResponseType.PlayerStillAwaitingPreviousQuery:
                    GroupManager.BroadcastSystemMessage(
                        requesterGroup,
                        GroupMessages.PlayerStillConsideringYourPreviousInviteRequest(AccountId));
                    break;
                case GroupInviteResponseType.PlayerAccepted:

                    if (CurrentGame != null && !LobbyConfiguration.GetGroupConfiguration().CanInviteActiveOpponents)
                    {
                        Game game = GameManager.GetGameWithPlayer(response.JoinerAccountId);

                        if (game != null && game == CurrentGame)
                        {
                            LobbyServerPlayerInfo lobbyServerOtherPlayerInfo = game.TeamInfo.TeamPlayerInfo
                                .FirstOrDefault(p => p.AccountId == response.JoinerAccountId);
                            LobbyServerPlayerInfo lobbyServerPlayerInfo = game.TeamInfo.TeamPlayerInfo
                                .FirstOrDefault(p => p.AccountId == AccountId);

                            if (lobbyServerOtherPlayerInfo?.TeamId != lobbyServerPlayerInfo?.TeamId)
                            {
                                log.Info($"Player {AccountId} is trying to accept a group invite but is currently on the opposing team.");
                                GroupManager.BroadcastSystemMessage(
                                    requesterGroup,
                                    GroupMessages.FailedToJoinGroupCantInviteActiveOpponent);
                                break;
                            }
                        }
                    }

                    GroupManager.JoinGroup(
                        groupRequestInfo.GroupId,
                        groupRequestInfo.IsInvitation
                            ? groupRequestInfo.RequesteeAccountId
                            : groupRequestInfo.RequesterAccountId);
                    BroadcastRefreshFriendList();
                    requester.BroadcastRefreshFriendList();
                    break;
            }
        }

        public void HandleGroupLeaveRequest(GroupLeaveRequest request)
        {
            GroupManager.CreateGroup(AccountId);
            BroadcastRefreshFriendList();
        }

        // TODO
        // No message handler registered for GameInvitationRequest

        private void OnAccountVisualsUpdated()
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

        private void HandlePurchaseEmblemRequest(PurchaseBannerForegroundRequest request)
        {
            //Get the users account
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            // Never trust the client double check plus we need this info to deduct it from account
            int cost = InventoryManager.GetBannerCost(request.BannerForegroundId);

            log.Info($"Player {AccountId} trying to purchase emblem {request.BannerForegroundId} with {request.CurrencyType} for the price {cost}");

            if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
            {
                PurchaseBannerForegroundResponse failedResponse = new PurchaseBannerForegroundResponse()
                {
                    ResponseId = request.RequestId,
                    Result = PurchaseResult.Failed,
                    CurrencyType = request.CurrencyType,
                    BannerForegroundId = request.BannerForegroundId
                };

                Send(failedResponse);

                return;
            }

            account.AccountComponent.UnlockedBannerIDs.Add(request.BannerForegroundId);

            account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase emblem");

            DB.Get().AccountDao.UpdateBankComponent(account);
            DB.Get().AccountDao.UpdateAccountComponent(account);

            PurchaseBannerForegroundResponse response = new PurchaseBannerForegroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                BannerForegroundId = request.BannerForegroundId
            };

            Send(response);

            //Update account curency
            Send(new PlayerAccountDataUpdateNotification(account));

        }

        private void HandlePurchaseBannerRequest(PurchaseBannerBackgroundRequest request)
        {
            //Get the users account
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            // Never trust the client double check plus we need this info to deduct it from account
            int cost = InventoryManager.GetBannerCost(request.BannerBackgroundId);

            log.Info($"Player {AccountId} trying to purchase banner {request.BannerBackgroundId} with {request.CurrencyType} for the price {cost}");

            if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
            {
                PurchaseBannerBackgroundResponse failedResponse = new PurchaseBannerBackgroundResponse()
                {
                    ResponseId = request.RequestId,
                    Result = PurchaseResult.Failed,
                    CurrencyType = request.CurrencyType,
                    BannerBackgroundId = request.BannerBackgroundId
                };

                Send(failedResponse);

                return;
            }

            account.AccountComponent.UnlockedBannerIDs.Add(request.BannerBackgroundId);
            account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase banner");

            DB.Get().AccountDao.UpdateBankComponent(account);
            DB.Get().AccountDao.UpdateAccountComponent(account);

            PurchaseBannerBackgroundResponse response = new PurchaseBannerBackgroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                BannerBackgroundId = request.BannerBackgroundId
            };

            Send(response);

            //Update account curency
            Send(new PlayerAccountDataUpdateNotification(account));
        }

        private void HandlePurchasAbilityVfx(PurchaseAbilityVfxRequest request)
        {
            //Get the users account
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            // Never trust the client double check plus we need this info to deduct it from account
            int cost = InventoryManager.GetVfxCost(request.VfxId, request.AbilityId);

            log.Info($"Player {AccountId} trying to purchase vfx {request.VfxId} with {request.CurrencyType} for character {request.CharacterType} and ability {request.AbilityId} for price {cost}");

            if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
            {
                PurchaseAbilityVfxResponse failedResponse = new PurchaseAbilityVfxResponse()
                {
                    ResponseId = request.RequestId,
                    Result = PurchaseResult.Failed,
                    CurrencyType = request.CurrencyType,
                    CharacterType = request.CharacterType,
                    AbilityId = request.AbilityId,
                    VfxId = request.VfxId
                };

                Send(failedResponse);

                return;
            }

            PlayerAbilityVfxSwapData abilityVfxSwapData = new PlayerAbilityVfxSwapData()
            {
                AbilityId = request.AbilityId,
                AbilityVfxSwapID = request.VfxId
            };

            account.CharacterData[request.CharacterType].CharacterComponent.AbilityVfxSwaps.Add(abilityVfxSwapData);
            account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase vfx");

            DB.Get().AccountDao.UpdateBankComponent(account);
            DB.Get().AccountDao.UpdateCharacterComponent(account, request.CharacterType);

            PurchaseAbilityVfxResponse response = new PurchaseAbilityVfxResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                CharacterType = request.CharacterType,
                AbilityId = request.AbilityId,
                VfxId = request.VfxId
            };

            Send(response);

            // Update character
            Send(new PlayerCharacterDataUpdateNotification()
            {
                CharacterData = account.CharacterData[request.CharacterType],
            });

            //Update account curency
            Send(new PlayerAccountDataUpdateNotification(account));
        }

        private void HandlePurchaseInventoryItemRequest(PurchaseInventoryItemRequest request)
        {
            Send(new PurchaseInventoryItemResponse
            {
                Result = PurchaseResult.Failed,
                InventoryItemID = request.InventoryItemID,
                CurrencyType = request.CurrencyType,
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePurchaseModRequest(PurchaseModRequest request)
        {
            Send(new PurchaseModResponse
            {
                Character = request.Character,
                UnlockData = request.UnlockData,
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePurchaseTitleRequest(PurchaseTitleRequest request)
        {
            Send(new PurchaseTitleResponse
            {
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                TitleId = request.TitleId,
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePurchaseTauntRequest(PurchaseTauntRequest request)
        {
            Send(new PurchaseTauntResponse
            {
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                CharacterType = request.CharacterType,
                TauntId = request.TauntId,
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePurchaseChatEmojiRequest(PurchaseChatEmojiRequest request)
        {
            Send(new PurchaseChatEmojiResponse
            {
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                EmojiID = request.EmojiID,
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePurchaseLoadoutSlotRequest(PurchaseLoadoutSlotRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (account == null
                || !account.CharacterData.TryGetValue(request.Character, out PersistedCharacterData characterData)
                || characterData.CharacterComponent.CharacterLoadouts.Count >= 10) // hardcoded on the client side too
            {
                Send(new PurchaseLoadoutSlotResponse
                {
                    Character = request.Character,
                    Success = false,
                    ResponseId = request.RequestId
                });
                return;
            }

            List<CharacterLoadout> loadouts = characterData.CharacterComponent.CharacterLoadouts;
            loadouts.Add(new CharacterLoadout(
                new CharacterModInfo(),
                new CharacterAbilityVfxSwapInfo(),
                $"Loadout {loadouts.Count}",
                ModStrictness.AllModes));

            // DB.Get().AccountDao.UpdateBankComponent(account);
            DB.Get().AccountDao.UpdateCharacterComponent(account, request.Character);

            Send(new PurchaseLoadoutSlotResponse
            {
                Character = request.Character,
                Success = true,
                ResponseId = request.RequestId
            });
            Send(new PlayerCharacterDataUpdateNotification
            {
                CharacterData = account.CharacterData[request.Character],
            });
            Send(new PlayerAccountDataUpdateNotification(account));
        }

        private void HandlePaymentMethodsRequest(PaymentMethodsRequest request)
        {
        }

        private void HandleStoreOpenedMessage(StoreOpenedMessage msg)
        {
        }

        private void HandleUIActionNotification(UIActionNotification notify)
        {
        }

        private void HandleCrashReportArchiveNameRequest(CrashReportArchiveNameRequest request)
        {
            CrashReportArchiveNameResponse response = new CrashReportArchiveNameResponse
            {
                Success = false,
                ResponseId = request.RequestId
            };
            
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(AccountId);
            if (sessionInfo is not null)
            {
                BuildVersionInfo info = sessionInfo.BuildVersionInfo;
                if (info.IsPatched)
                {
                    response.Success = true;
                    response.ArchiveName = CrashReportManager.Add(AccountId).ToString();
                }
            }
            Send(response);
        }

        private void HandleClientStatusReport(ClientStatusReport msg)
        {
            string shortDetails = msg.StatusDetails != null ? msg.StatusDetails.Split('\n', 2)[0] : "";
            log.Info($"ClientStatusReport {msg.Status}: {shortDetails} ({msg.UserMessage})");
            CrashReportManager.ProcessClientStatusReport(AccountId, msg);
        }

        public void HandleClientErrorSummary(ClientErrorSummary msg)
        {
            foreach (var (key, count) in msg.ReportCount)
            {
                log.Info($"ClientErrorSummary {key}: {count}");
            }
            CrashReportManager.ProcessClientErrorSummary(AccountId, msg);
        }

        private void HandleClientErrorReport(ClientErrorReport msg)
        {
            log.Info($"ClientErrorReport {msg.StackTraceHash}: {msg.LogString} {msg.StackTrace} {msg.Time}");
            CrashReportManager.ProcessClientErrorReport(AccountId, msg);
        }

        private void HandleErrorReportSummaryResponse(ErrorReportSummaryResponse response)
        {
            log.Info($"ErrorReportSummaryResponse {response.ClientErrorReport.StackTraceHash}: {
                response.ClientErrorReport.LogString} {response.ClientErrorReport.StackTrace
                } {response.ClientErrorReport.Time}");
            CrashReportManager.ProcessErrorReportSummaryResponse(AccountId, response);
        }

        private void HandleClientFeedbackReport(ClientFeedbackReport message)
        {
            string context = CurrentGame is not null ? GameIdString(CurrentGame.GameInfo) : "";
            DB.Get().UserFeedbackDao.Save(new UserFeedbackDao.UserFeedback(AccountId, message, context));
            DiscordManager.Get().SendPlayerFeedback(AccountId, message);
        }

        private void HandleClientPerformanceReport(ClientPerformanceReport msg)
        {
            log.Info($"ClientPerformanceReport {msg.PerformanceInfo}");
        }

        private void HandleSubscribeToCustomGamesRequest(SubscribeToCustomGamesRequest request)
        {
            CustomGameManager.Subscribe(this);
        }

        private void HandleUnsubscribeFromCustomGamesRequest(UnsubscribeFromCustomGamesRequest request)
        {
            CustomGameManager.Unsubscribe(this);
        }

        private void HandleRankedLeaderboardOverviewRequest(RankedLeaderboardOverviewRequest request)
        {
            Send(new RankedLeaderboardOverviewResponse
            {
                GameType = GameType.PvP,
                TierInfoPerGroupSize = new Dictionary<int, PerGroupSizeTierInfo>(),
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandleCalculateFreelancerStatsRequest(CalculateFreelancerStatsRequest request)
        {
            Send(new CalculateFreelancerStatsResponse
            {
                GlobalPercentiles = new Dictionary<StatDisplaySettings.StatType, PercentileInfo>(),
                FreelancerSpecificPercentiles = new Dictionary<int, PercentileInfo>(),
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandlePlayerPanelUpdatedNotification(PlayerPanelUpdatedNotification msg)
        {
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
            IsReady = false;
            RefreshGroup();
        }

        public void OnJoinGroup()
        {
            IsReady = false;
        }

        public void OnStartGame(Game game)
        {
            IsReady = false;
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
                    if (LobbyConfiguration.AreAllOnlineFriends())
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
