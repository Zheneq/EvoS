using System;
using System.Collections.Generic;
using System.Linq;
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
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using LobbyGameClientMessages;
using log4net;
using Newtonsoft.Json;
using WebSocketSharp;
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
            RegisterHandler<ClientErrorSummary>(HandleClientErrorSummary);
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
            RegisterHandler<ClientFeedbackReport>(HandleClientFeedbackReport);
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
            RegisterHandler<SubscribeToCustomGamesRequest>(HandleSubscribeToCustomGamesRequest);
            RegisterHandler<UnsubscribeFromCustomGamesRequest>(HandleUnsubscribeFromCustomGamesRequest);
            RegisterHandler<CreateGameRequest>(HandleCreateGameRequest);
            RegisterHandler<GameInfoUpdateRequest>(HandleGameInfoUpdateRequest);
            RegisterHandler<RankedLeaderboardOverviewRequest>(HandleRankedLeaderboardOverviewRequest);
            RegisterHandler<CalculateFreelancerStatsRequest>(HandleCalculateFreelancerStatsRequest);
            RegisterHandler<PlayerPanelUpdatedNotification>(HandlePlayerPanelUpdatedNotification);

            RegisterHandler<SetRegionRequest>(HandleSetRegionRequest);
            RegisterHandler<LoadingScreenToggleRequest>(HandleLoadingScreenToggleRequest);
            RegisterHandler<SendRAFReferralEmailsRequest>(HandleSendRAFReferralEmailsRequest);

            RegisterHandler<PurchaseBannerForegroundRequest>(HandlePurchaseEmblemRequest);
            RegisterHandler<PurchaseBannerBackgroundRequest>(HandlePurchaseBannerRequest);
            RegisterHandler<PurchaseAbilityVfxRequest>(HandlePurchasAbilityVfx);
            RegisterHandler<PurchaseInventoryItemRequest>(HandlePurchaseInventoryItemRequest);


            RegisterHandler<FriendUpdateRequest>(HandleFriendUpdate);
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

        private void HandleDEBUG_AdminSlashCommandNotification(DEBUG_AdminSlashCommandNotification notification)
        {
            log.Info($"DEBUG_AdminSlashCommandNotification: {notification.Command}");
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (account == null)
            {
                return;
            }
            if (account.AccountComponent.AppliedEntitlements.ContainsKey("DEVELOPER_ACCESS") || EvosConfiguration.GetDevMode())
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
            }
        }

        private void HandleSetDevTagRequest(SetDevTagRequest request)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            if (account == null)
            {
                return;
            }
            if (account.AccountComponent.AppliedEntitlements.ContainsKey("DEVELOPER_ACCESS"))
            {
                account.AccountComponent.DisplayDevTag = request.active;
                Send(new SetDevTagResponse() { 
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
            bool success = CustomGameManager.UpdateGameInfo(AccountId, gameInfoUpdateRequest.GameInfo, gameInfoUpdateRequest.TeamInfo);
            Game game = CustomGameManager.GetMyGame(AccountId);

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
            GroupInfo group = GroupManager.GetGroup(AccountId);
            GroupKickResponse response = new GroupKickResponse
            {
                ResponseId = request.RequestId,
                MemberName = request.MemberName,
            };
            if (group.IsSolo())
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

                GroupManager.CreateGroup(AccountId);

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

        public void HandleClientErrorSummary(ClientErrorSummary request)
        {
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
                    Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId, LocalizedFailure = failure});
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
            var response = new GroupInviteResponse
            {
                FriendHandle = request.FriendHandle,
                ResponseId = request.RequestId,
                Success = false
            };
            
            long friendAccountId = SessionManager.GetOnlinePlayerByHandle(request.FriendHandle) ?? 0;
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
            Send(new CrashReportArchiveNameResponse
            {
                Success = false,
                ResponseId = request.RequestId
            });
        }

        private void HandleClientStatusReport(ClientStatusReport msg)
        {
            string shortDetails = msg.StatusDetails != null ? msg.StatusDetails.Split('\n', 2)[0] : "";
            log.Info($"ClientStatusReport {msg.Status}: {shortDetails} ({msg.UserMessage})");
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
            SendSystemMessage(
                (game.Server.Name != "" ? $"You are playing on {game.Server.Name} server. " : "") +
                (game.Server.BuildVersion != "" ? $"Build {game.Server.BuildVersion}. " : "") +
                $"Game {LobbyServerUtils.GameIdString(game.GameInfo)}.");
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

        private void HandleClientFeedbackReport(ClientFeedbackReport message)
        {
            string context = CurrentGame is not null ? LobbyServerUtils.GameIdString(CurrentGame.GameInfo) : "";
            DB.Get().UserFeedbackDao.Save(new UserFeedbackDao.UserFeedback(AccountId, message, context));
            DiscordManager.Get().SendPlayerFeedback(AccountId, message);
        }

        private void HandleFriendUpdate(FriendUpdateRequest request)
        {
            long friendAccountId = LobbyServerUtils.ResolveAccountId(request.FriendAccountId, request.FriendHandle);
            if (friendAccountId == 0 || friendAccountId == AccountId)
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
                log.Info($"Attempted to {request.FriendOperation} {request.FriendHandle}, but such a user was not found");
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);

            if (account == null || friendAccount == null)
            {
                Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                log.Info($"Failed to find account {AccountId} and/or {friendAccountId}");
                return;
            }

            switch (request.FriendOperation)
            {
                case FriendOperation.Block:
                {
                    bool updated = account.SocialComponent.Block(friendAccountId);
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
                    bool updated = account.SocialComponent.Unblock(friendAccountId);
                    log.Info($"{account.Handle} unblocked {friendAccount.Handle}{(updated ? "" : ", but they weren't blocked")}");
                    if (updated)
                    {
                        DB.Get().AccountDao.UpdateSocialComponent(account);
                    }

                    Send(FriendUpdateResponse.of(request));
                    RefreshFriendList();
                    return;
                }
                case FriendOperation.Remove:
                {
                    if (account.SocialComponent.IsBlocked(friendAccountId))
                    {
                        goto case FriendOperation.Unblock;
                    }
                    else
                    {
                        goto default;
                    }
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
    }
}
