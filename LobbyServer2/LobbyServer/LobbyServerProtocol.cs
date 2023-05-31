using System;
using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Store;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : LobbyServerProtocolBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));
        
        private BridgeServerProtocol _currentServer;

        public BridgeServerProtocol CurrentServer
        {
            get => _currentServer;
            private set
            {
                if (_currentServer != value)
                {
                    _currentServer = value;
                    BroadcastRefreshFriendList();
                }
            }
        }

        public bool IsInGame() => CurrentServer != null;

        public bool IsInGroup() => !GroupManager.GetPlayerGroup(AccountId)?.IsSolo() ?? false;

        public int GetGroupSize() => GroupManager.GetPlayerGroup(AccountId)?.Members.Count ?? 1;

        public bool IsInQueue() => MatchmakingManager.IsQueued(GroupManager.GetPlayerGroup(AccountId));
        
        public bool IsReady { get; private set; }

        public CharacterType OldCharacter { get; set; }
        
        public event Action<LobbyServerProtocol, ChatNotification> OnChatNotification = delegate {};
        public event Action<LobbyServerProtocol, GroupChatRequest> OnGroupChatRequest = delegate {};
        

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

            RegisterHandler<PurchaseModRequest>(HandlePurchaseModRequest);
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

        protected override void HandleClose(CloseEventArgs e)
        {
            UnregisterAllHandlers();
            if (!SessionCleaned)
            {
                SessionCleaned = true;
                GroupManager.LeaveGroup(AccountId, false);
            } 
            LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(this.AccountId);
            if (playerInfo != null)
            {
                log.Info(string.Format(Messages.PlayerDisconnected, this.UserName));

                BridgeServerProtocol server = ServerManager.GetServerWithPlayer(AccountId);
                if (server != null)
                {
                    //     server.Send(new DisconnectPlayerRequest()
                    //     {
                    //         SessionInfo = SessionManager.GetSessionInfo(this.AccountId),
                    //         PlayerInfo = playerInfo,
                    //         GameResult = GameResult.ClientLeft
                    //     });

                    // Set client back to previus CharacterType
                    LobbyServerProtocol client = SessionManager.GetClientConnection(AccountId);
                    if (server.GetPlayerInfo(AccountId).CharacterType != client.OldCharacter)
                    {
                        server.ResetCharacterToOriginal(client, true);
                    }
                    // We set it back on reconnect if need be
                    client.OldCharacter = CharacterType.None;
                }

                SessionManager.OnPlayerDisconnect(this);
            }
            BroadcastRefreshFriendList();
        }

        public void JoinServer(BridgeServerProtocol server)
        {
            if (CurrentServer != null)
            {
                log.Debug($"{AccountId} is jumping from {CurrentServer.ProcessCode} to {server.ProcessCode}");
            }

            CurrentServer = server;
        }

        public bool LeaveServer(BridgeServerProtocol server)
        {
            if (CurrentServer == null)
            {
                log.Debug($"{AccountId} is asked to leave {server.ProcessCode} while they are not on any server");
                return true;
            }
            if (CurrentServer != server)
            {
                log.Debug($"{AccountId} is asked to leave {server.ProcessCode} while they are on {CurrentServer.ProcessCode}. Ignoring.");
                return false;
            }

            CurrentServer = null;
            return true;
        }

        public void BroadcastRefreshFriendList()
        {
            foreach (IWebSocketSession session in Sessions.Sessions)
            {
                ((LobbyServerProtocol)session)?.RefreshFriendList();
            }
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

            GroupUpdateNotification update = new GroupUpdateNotification()
            {
                Members = info.Members,
                GameType = info.SelectedQueueType,
                SubTypeMask = info.SubTypeMask,
                AllyDifficulty = BotDifficulty.Medium,
                EnemyDifficulty = BotDifficulty.Medium,
                GroupId = GroupManager.GetGroupID(AccountId)
            };

            Send(update);
        }

        private void HandleGroupPromoteRequest(GroupPromoteRequest message)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            //Sadly message.AccountId returns 0 so look it up by name/handle
            long? accountId = SessionManager.GetOnlinePlayerByHandle(message.Name);
            if (accountId.HasValue)
            {
                group.SetLeader((long)accountId);
                BroadcastRefreshGroup();
                //If the new leader is accountId send success true else false tho we do not have any localization does nothing atm 
                if (group.IsLeader((long)accountId))
                {
                    Send(new GroupPromoteResponse()
                    {
                        Success = true
                    });
                }
                else 
                {
                    Send(new GroupPromoteResponse()
                    {
                        //To send more need LocalizedFailure to be added
                        Success = false
                    });
                }
            }
            else
            {
                Send(new GroupPromoteResponse()
                {
                    //To send more need LocalizedFailure to be added
                    Success = false
                });
            }
        }

        private void HandleGroupKickRequest(GroupKickRequest message)
        {
            LobbyPlayerGroupInfo info = GroupManager.GetGroupInfo(AccountId);
            GroupManager.LeaveGroup(info.Members.Find(m => m.MemberDisplayName == message.MemberName).AccountID, false);
        }

        public void HandleRegisterGame(RegisterGameClientRequest request)
        {
            try
            {
                LobbyServerPlayerInfo playerInfo = SessionManager.OnPlayerConnect(this, request);

                if (playerInfo != null)
                {
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
                    response.AuthInfo.Handle = playerInfo.Handle;
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
                            player.Send(new ChatNotification
                            {
                                ConsoleMessageType = ConsoleMessageType.SystemMessage,
                                Text = $"<link=name>{sessionInfo.Handle}</link> connected to lobby server"
                            });
                        }
                    }
                }
                else
                {
                    SendErrorResponse(new RegisterGameClientResponse(), request.RequestId, Messages.LoginFailed);
                    WebSocket.Close();
                }
            }
            catch (Exception e)
            {
                SendErrorResponse(new RegisterGameClientResponse(), request.RequestId, e);
                WebSocket.Close();
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
            this.SelectedSubTypeMask = request.SubTypeMask;
            SetGameSubTypeResponse response = new SetGameSubTypeResponse() { ResponseId = request.RequestId };
            Send(response);
        }

        public void HandlePlayerInfoUpdateRequest(PlayerInfoUpdateRequest request)
        {
            LobbyPlayerInfoUpdate update = request.PlayerInfoUpdate;
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            
            // TODO validate what player has purchased

            // building character info to validate it for current game
            CharacterType characterType = update.CharacterType ?? account.AccountComponent.LastCharacter;
            CharacterComponent characterComponent = (CharacterComponent)account.CharacterData[characterType].CharacterComponent.Clone();
            if (update.CharacterSkin.HasValue) characterComponent.LastSkin = update.CharacterSkin.Value;
            if (update.CharacterCards.HasValue) characterComponent.LastCards = update.CharacterCards.Value;
            if (update.CharacterMods.HasValue) characterComponent.LastMods = update.CharacterMods.Value;
            if (update.CharacterAbilityVfxSwaps.HasValue) characterComponent.LastAbilityVfxSwaps = update.CharacterAbilityVfxSwaps.Value;
            if (update.CharacterLoadoutChanges.HasValue) characterComponent.CharacterLoadouts = update.CharacterLoadoutChanges.Value.CharacterLoadoutChanges;
            if (update.LastSelectedLoadout.HasValue) characterComponent.LastSelectedLoadout = update.LastSelectedLoadout.Value;
            LobbyCharacterInfo characterInfo = LobbyCharacterInfo.Of(account.CharacterData[characterType], characterComponent);
            
            if (CurrentServer != null && !CurrentServer.UpdateCharacterInfo(AccountId, characterInfo, update))
            {
                Send(new PlayerInfoUpdateResponse
                {
                    Success = false,
                    ResponseId = request.RequestId
                });
                return;
            }

            // committing changes
            if (update.CharacterType.HasValue)
            {
                account.AccountComponent.LastCharacter = update.CharacterType.Value;
            }
            account.CharacterData[characterType].CharacterComponent = characterComponent;
            DB.Get().AccountDao.UpdateAccount(account);
            LobbyServerPlayerInfo playerInfo = SessionManager.UpdateLobbyServerPlayerInfo(AccountId);
            
            if (request.GameType != null && request.GameType.HasValue)
            {
                SetGameType(request.GameType.Value);
            }
            if (update.CharacterType != null && update.CharacterType.HasValue)
            {
                PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification
                {
                    AccountData = account.CloneForClient()
                };
                Send(updateNotification);
            }
            if (update.AllyDifficulty != null && update.AllyDifficulty.HasValue)
                SetAllyDifficulty(update.AllyDifficulty.Value);
            if (update.ContextualReadyState != null && update.ContextualReadyState.HasValue)
                SetContextualReadyState(update.ContextualReadyState.Value);
            if (update.EnemyDifficulty != null && update.EnemyDifficulty.HasValue)
                SetEnemyDifficulty(update.EnemyDifficulty.Value);


            Send(new PlayerInfoUpdateResponse
            {
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
                CharacterInfo = playerInfo.CharacterInfo,
                OriginalPlayerInfoUpdate = update,
                ResponseId = request.RequestId
            });
            BroadcastRefreshGroup();
        }

        public void HandleCheckAccountStatusRequest(CheckAccountStatusRequest request)
        {
            CheckAccountStatusResponse response = new CheckAccountStatusResponse()
            {
                QuestOffers = new QuestOfferNotification() { OfferDailyQuest = false },
                ResponseId = request.RequestId
            };
            Send(response);
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

            BridgeServerProtocol server = ServerManager.GetServerWithPlayer(AccountId);
            LobbyGameInfo lobbyGameInfo = null;

            if (server != null)
            {
                // Make sure we wait untill gameserver disconects us
                if (server.GetPlayerInfo(AccountId).ReplacedWithBots)
                {
                    lobbyGameInfo = server.GameInfo;
                }
            }

            PreviousGameInfoResponse response = new PreviousGameInfoResponse()
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
            BridgeServerProtocol server = CurrentServer;
            if (server != null)
            {
                if (server.GetPlayerInfo(AccountId)?.CharacterType != OldCharacter)
                {
                    server.ResetCharacterToOriginal(this);
                }
                // We set it back on reconnect if need be
                OldCharacter = CharacterType.None;

                if (server.ServerGameStatus == GameStatus.Stopped)
                {
                    LeaveServer(server);
                }
            }

            Send(new LeaveGameResponse
            {
                Success = true,
                ResponseId = request.RequestId
            });
            Send(new GameStatusNotification
            {
                GameServerProcessCode = server?.ProcessCode,
                GameStatus = GameStatus.Stopped
            });
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
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);
            IsReady = contextualReadyState.ReadyState == ReadyState.Ready;
            if (group == null)
            {
                MatchmakingManager.StartPractice(this);
            }
            else
            {
                if (CurrentServer != null)
                {
                    if (contextualReadyState.ReadyState == ReadyState.Ready)
                    {
                        CurrentServer.SetPlayerReady(AccountId);
                    }
                }
                else
                {
                    UpdateGroupReadyState();
                    BroadcastRefreshGroup();
                }
            }
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

                Send(new JoinMatchmakingQueueResponse { Success = true, ResponseId = request.RequestId });
                IsReady = true;
                MatchmakingManager.AddGroupToQueue(request.GameType, group);
            }
            catch (Exception e)
            {
                Send(new JoinMatchmakingQueueResponse { Success = false, ResponseId = request.RequestId });
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
                log.Error("Failed to process join queue request", e);
            }
        }


        public void HandleChatNotification(ChatNotification notification)
        {
            OnChatNotification(this, notification);
        }

        public void HandleGroupInviteRequest(GroupInviteRequest request)
        {
            long? friendAccountId = SessionManager.GetOnlinePlayerByHandle(request.FriendHandle);
            if (!friendAccountId.HasValue)
            {
                log.Warn($"Failed to find player {request.FriendHandle} invited to group by {AccountId}");
                Send(new GroupInviteResponse
                {
                    FriendHandle = request.FriendHandle,
                    ResponseId = request.RequestId,
                    Success = false
                });
                return;
            }

            SocialComponent socialComponent = DB.Get().AccountDao.GetAccount((long)friendAccountId)?.SocialComponent;
            if (socialComponent?.IsBlocked(AccountId) == true)
            {
                log.Warn($"{AccountId} attempted to invite {request.FriendHandle} who blocked them");
                Send(new GroupInviteResponse
                {
                    FriendHandle = request.FriendHandle,
                    ResponseId = request.RequestId,
                    Success = true // shadow ban
                });
                return;
            }
            
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);

            if (group.Members.Count == LobbyConfiguration.GetMaxGroupSize())
            {
                log.Warn($"{AccountId} attempted to invite {request.FriendHandle} into a full group");
                Send(new GroupInviteResponse
                {
                    FriendHandle = request.FriendHandle,
                    ResponseId = request.RequestId,
                    Success = false
                });
                return;
            }

            GroupConfirmationRequest.JoinType joinType;
            if (group == null)
            {
                joinType = GroupConfirmationRequest.JoinType.InviteToFormGroup;
                GroupManager.CreateGroup(AccountId);
                group = GroupManager.GetPlayerGroup(AccountId);
                log.Info($"{AccountId} created group {group.GroupId}");
            }
            else
            {
                // TODO request to join group
                // joinType = group.IsSolo()
                //     ? GroupConfirmationRequest.JoinType.InviteToFormGroup
                //     : GroupConfirmationRequest.JoinType.RequestToJoinGroup;
                joinType = GroupConfirmationRequest.JoinType.InviteToFormGroup;
            }

            PersistedAccountData requester = DB.Get().AccountDao.GetAccount(AccountId);
            PersistedAccountData leader = DB.Get().AccountDao.GetAccount(group.Leader);
            LobbyServerProtocol friend = SessionManager.GetClientConnection((long) friendAccountId);

            if (friend == null)  // can be offline
            {
                log.Info($"{AccountId}/{requester.Handle} failed to invite {friend.AccountId}/{request.FriendHandle} to group {group.GroupId} for they are offline");
                Send(new GroupInviteResponse
                {
                    FriendHandle = request.FriendHandle,
                    ResponseId = request.RequestId,
                    Success = false
                });
                return;
            }
            
            if (group.Leader == AccountId)
            {
                friend.Send(new GroupConfirmationRequest
                {
                    GroupId = group.GroupId,
                    LeaderName = leader.Handle,
                    LeaderFullHandle = leader.Handle,
                    JoinerName = requester.Handle,
                    JoinerAccountId = AccountId,
                    ConfirmationNumber = GroupManager.CreateGroupRequest(AccountId, friend.AccountId, group.GroupId),
                    ExpirationTime = TimeSpan.FromSeconds(20),
                    Type = joinType,
                    // RequestId = TODO
                });
                if (EvosConfiguration.GetPingOnGroupRequest() && !friend.IsInGroup() && !friend.IsInGame())
                {
                    friend.Send(new ChatNotification
                    {
                        SenderAccountId = AccountId,
                        SenderHandle = requester.Handle,
                        ConsoleMessageType = ConsoleMessageType.WhisperChat,
                        Text = "[Group request]"
                    });
                }
            
                log.Info($"{AccountId}/{requester.Handle} invited {friend.AccountId}/{request.FriendHandle} to group {group.GroupId}");
                Send(new GroupInviteResponse
                {
                    FriendHandle = request.FriendHandle,
                    ResponseId = request.RequestId,
                    Success = true
                });
            } 
            else
            {
                LobbyServerProtocol leaderSession = SessionManager.GetClientConnection(leader.AccountId);
                leaderSession.Send(new GroupSuggestionRequest
                {
                    LeaderAccountId = group.Leader,
                    SuggestedAccountFullHandle = request.FriendHandle,
                    SuggesterAccountName = requester.Handle,
                    SuggesterAccountId = AccountId,
                });
            }
        }

        public void HandleGroupSuggestionResponse(GroupSuggestionResponse response)
        { 
            //Is this needed? 
        }

        public void HandleGroupConfirmationResponse(GroupConfirmationResponse response)
        {
            switch (response.Acceptance)
            {
                case GroupInviteResponseType.PlayerRejected:
                case GroupInviteResponseType.OfferExpired:
                case GroupInviteResponseType.RequestorSpamming:
                case GroupInviteResponseType.PlayerInCustomMatch:
                case GroupInviteResponseType.PlayerStillAwaitingPreviousQuery:
                    log.Info($"Player {AccountId} rejected request {response.ConfirmationNumber} " +
                             $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
                    // TODO send message
                    break;
                case GroupInviteResponseType.PlayerAccepted:
                    log.Info($"Player {AccountId} accepted request {response.ConfirmationNumber} " +
                             $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
                    // TODO validation
                    GroupManager.JoinGroup(response.GroupId, AccountId);
                    BroadcastRefreshFriendList();
                    break;
            }
        }
        
        public void HandleGroupLeaveRequest(GroupLeaveRequest request)
        {
            GroupManager.CreateGroup(AccountId);
            BroadcastRefreshFriendList();
        }

        private void OnAccountVisualsUpdated()
        {

            BroadcastRefreshFriendList();
            BroadcastRefreshGroup();
            CurrentServer?.OnAccountVisualsUpdated(AccountId);
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
            DB.Get().AccountDao.UpdateAccount(account);

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
                DB.Get().AccountDao.UpdateAccount(account);

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

            if (CurrentServer != null)
            {
                response.ResponseId = 0;
                foreach (LobbyServerProtocol client in CurrentServer.GetClients())
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

            if (CurrentServer != null)
            {
                CurrentServer.OnPlayerUsedGGPack(AccountId);
                foreach (LobbyServerProtocol client in CurrentServer.GetClients())
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
                            NumGGPacksUsed = CurrentServer.GameInfo.ggPackUsedAccountIDs[AccountId]
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
            account.AccountComponent.UIStates.Add(request.UIState,request.StateValue);
            DB.Get().AccountDao.UpdateAccount(account);
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

            DB.Get().AccountDao.UpdateAccount(account);

            PurchaseBannerForegroundResponse response = new PurchaseBannerForegroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                BannerForegroundId = request.BannerForegroundId
            };

            Send(response);

            //Update account curency
            Send(new PlayerAccountDataUpdateNotification()
            {
                AccountData = account,
            });

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

            DB.Get().AccountDao.UpdateAccount(account);

            PurchaseBannerBackgroundResponse response = new PurchaseBannerBackgroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Success,
                CurrencyType = request.CurrencyType,
                BannerBackgroundId = request.BannerBackgroundId
            };

            Send(response);

            //Update account curency
            Send(new PlayerAccountDataUpdateNotification()
            {
                AccountData = account,
            });
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

            DB.Get().AccountDao.UpdateAccount(account);

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
            Send(new PlayerAccountDataUpdateNotification()
            {
                AccountData = account,
            });
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
                || characterData.CharacterComponent.CharacterLoadouts.Count >= 3) // hardcoded to 10 on the client side but that sounds too many
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
            DB.Get().AccountDao.UpdateAccount(account);
            
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
            Send(new PlayerAccountDataUpdateNotification
            {
                AccountData = account.CloneForClient()
            });
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
        }

        private void HandleUnsubscribeFromCustomGamesRequest(UnsubscribeFromCustomGamesRequest request)
        {
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
                DB.Get().AccountDao.UpdateAccount(account);
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

            log.Info($"{UserName} wants to reconnect to a game");
            
            BridgeServerProtocol server = ServerManager.GetServerWithPlayer(AccountId);

            if (server == null)
            {
                // no longer in a game
                Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
                return;
            }
            
            LobbyServerPlayerInfo playerInfo = server.GetPlayerInfo(AccountId);
            Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = true });

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            LobbyCharacterInfo character = LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]);

            LobbyServerPlayerInfo lobbyServerPlayerInfo = server.TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == AccountId);
            if (lobbyServerPlayerInfo == null)
            {
                // no longer in a game
                Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
                return;
            }
            
            CharacterType oldCharacterType = lobbyServerPlayerInfo.CharacterType;

            if (character.CharacterType != oldCharacterType)
            {
                //Update db with the new LastCharacter
                account.AccountComponent.LastCharacter = oldCharacterType;
                DB.Get().AccountDao.UpdateAccount(account);
                Send(new ForcedCharacterChangeFromServerNotification()
                {
                    ChararacterInfo = LobbyCharacterInfo.Of(account.CharacterData[oldCharacterType]),
                });
            }

            // Set OldCharacter to last selected character before reconnect
            OldCharacter = character.CharacterType;

            JoinServer(server);

            playerInfo.ReplacedWithBots = false;

            server.SendGameAssignmentNotification(this, true);
            server.SendGameInfo(this, GameStatus.Launching);
            server.SendGameInfo(this, GameStatus.Launched);
            OnStartGame(server);
            server.SendGameInfo(this, GameStatus.Started);
            server.StartGameForReconection(AccountId);
        }

        public void OnLeaveGroup()
        {
            RefreshGroup();
            IsReady = false;
        }

        public void OnJoinGroup()
        {
            IsReady = false;
        }

        public void OnStartGame(BridgeServerProtocol server)
        {
            IsReady = false;
            Send(new ChatNotification
            {
                ConsoleMessageType = ConsoleMessageType.SystemMessage,
                Text = (server.Name != "" ? $"You are playing on {server.Name} server. " : "") +
                       (server.BuildVersion != "" ? $"Build {server.BuildVersion}. " : "") +
                       $"Game {LobbyServerUtils.GameIdString(server.GameInfo)}."
            });
        }

        public void CloseConnection()
        {
            this.WebSocket.Close();
        }

        private void HandleClientFeedbackReport(ClientFeedbackReport message)
        {
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
                        DB.Get().AccountDao.UpdateAccount(account);
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
                        DB.Get().AccountDao.UpdateAccount(account);
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
