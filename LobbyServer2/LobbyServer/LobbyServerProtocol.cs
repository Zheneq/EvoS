using System;
using System.Collections.Generic;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Store;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using log4net;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using MongoDB.Bson;
using Discord.Webhook;
using Discord;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocol : LobbyServerProtocolBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocol));
        
        private BridgeServerProtocol _currentServer;

        public BridgeServerProtocol CurrentServer
        {
            get => _currentServer;
            set
            {
                _currentServer = value;
                BroadcastRefreshFriendList();
            }
        }

        public bool IsInGame() => CurrentServer != null;

        public bool IsInGroup() => !GroupManager.GetPlayerGroup(AccountId)?.IsSolo() ?? false;

        public int GetGroupSize() => GroupManager.GetPlayerGroup(AccountId)?.Members.Count ?? 1;

        public bool IsInQueue() => MatchmakingManager.IsQueued(GroupManager.GetPlayerGroup(AccountId));
        
        public bool IsReady { get; private set; }

        public CharacterType OldCharacter { get; set; }

        protected override void HandleOpen()
        {
            RegisterHandler(new EvosMessageDelegate<RegisterGameClientRequest>(HandleRegisterGame));
            RegisterHandler(new EvosMessageDelegate<OptionsNotification>(HandleOptionsNotification));
            RegisterHandler(new EvosMessageDelegate<CustomKeyBindNotification>(HandleCustomKeyBindNotification));
            RegisterHandler(new EvosMessageDelegate<PricesRequest>(HandlePricesRequest));
            RegisterHandler(new EvosMessageDelegate<PlayerUpdateStatusRequest>(HandlePlayerUpdateStatusRequest));
            RegisterHandler(new EvosMessageDelegate<PlayerMatchDataRequest>(HandlePlayerMatchDataRequest));
            RegisterHandler(new EvosMessageDelegate<SetGameSubTypeRequest>(HandleSetGameSubTypeRequest));
            RegisterHandler(new EvosMessageDelegate<PlayerInfoUpdateRequest>(HandlePlayerInfoUpdateRequest));
            RegisterHandler(new EvosMessageDelegate<CheckAccountStatusRequest>(HandleCheckAccountStatusRequest));
            RegisterHandler(new EvosMessageDelegate<CheckRAFStatusRequest>(HandleCheckRAFStatusRequest));
            RegisterHandler(new EvosMessageDelegate<ClientErrorSummary>(HandleClientErrorSummary));
            RegisterHandler(new EvosMessageDelegate<PreviousGameInfoRequest>(HandlePreviousGameInfoRequest));
            RegisterHandler(new EvosMessageDelegate<PurchaseTintRequest>(HandlePurchaseTintRequest));
            RegisterHandler(new EvosMessageDelegate<LeaveGameRequest>(HandleLeaveGameRequest));
            RegisterHandler(new EvosMessageDelegate<JoinMatchmakingQueueRequest>(HandleJoinMatchmakingQueueRequest));
            RegisterHandler(new EvosMessageDelegate<LeaveMatchmakingQueueRequest>(HandleLeaveMatchmakingQueueRequest));
            RegisterHandler(new EvosMessageDelegate<ChatNotification>(HandleChatNotification));
            RegisterHandler(new EvosMessageDelegate<GroupInviteRequest>(HandleGroupInviteRequest));
            RegisterHandler(new EvosMessageDelegate<GroupConfirmationResponse>(HandleGroupConfirmationResponse));
            RegisterHandler(new EvosMessageDelegate<GroupSuggestionResponse>(HandleGroupSuggestionResponse));
            RegisterHandler(new EvosMessageDelegate<GroupLeaveRequest>(HandleGroupLeaveRequest));
            RegisterHandler(new EvosMessageDelegate<GroupKickRequest>(HandleGroupKickRequest));
            RegisterHandler(new EvosMessageDelegate<GroupPromoteRequest>(HandleGroupPromoteRequest));
            RegisterHandler(new EvosMessageDelegate<SelectBannerRequest>(HandleSelectBannerRequest));
            RegisterHandler(new EvosMessageDelegate<SelectTitleRequest>(HandleSelectTitleRequest));
            RegisterHandler(new EvosMessageDelegate<UseOverconRequest>(HandleUseOverconRequest));
            RegisterHandler(new EvosMessageDelegate<UseGGPackRequest>(HandleUseGGPackRequest));
            RegisterHandler(new EvosMessageDelegate<UpdateUIStateRequest>(HandleUpdateUIStateRequest));
            RegisterHandler(new EvosMessageDelegate<GroupChatRequest>(HandleGroupChatRequest));
            RegisterHandler(new EvosMessageDelegate<ClientFeedbackReport>(HandleClientFeedbackReport));
            RegisterHandler(new EvosMessageDelegate<RejoinGameRequest>(HandleRejoinGameRequest));

            /* TODO: adding these to
            RegisterHandler(new EvosMessageDelegate<PurchaseModResponse>(HandlePurchaseModRequest));
            RegisterHandler(new EvosMessageDelegate<PurchaseTauntRequest>(HandlePurchaseTauntRequest));
            RegisterHandler(new EvosMessageDelegate<PurchaseChatEmojiRequest>(HandlePurchaseChatEmoji));
            RegisterHandler(new EvosMessageDelegate<PurchaseLoadoutSlotRequest>(HandlePurchaseLoadoutSlot));
            */

            RegisterHandler(new EvosMessageDelegate<PurchaseBannerForegroundRequest>(HandlePurchaseEmblemRequest));
            RegisterHandler(new EvosMessageDelegate<PurchaseBannerBackgroundRequest>(HandlePurchaseBannerRequest));
            RegisterHandler(new EvosMessageDelegate<PurchaseAbilityVfxRequest>(HandlePurchasAbilityVfx));
            
        }

        protected override void HandleClose(CloseEventArgs e)
        {
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
                        LobbyServerProtocol player =  SessionManager.GetClientConnection(playerAccountId);
                        if (player.CurrentServer == null)
                        {
                            ChatNotification chatNotification = new ChatNotification
                            {
                                ConsoleMessageType = ConsoleMessageType.SystemMessage,
                                Text = $"<link=name>{sessionInfo.Handle}</link> connected to lobby server"
                            };
                            player.Send(chatNotification);
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
            PlayerMatchDataResponse response = new PlayerMatchDataResponse()
            {
                MatchData = new List<PersistedCharacterMatchData>(),
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

            // Change Character
            if (update.CharacterType.HasValue)
            {
                account.AccountComponent.LastCharacter = update.CharacterType.Value;
            }
            CharacterComponent characterComponent = account.CharacterData[account.AccountComponent.LastCharacter].CharacterComponent;

            // Change Skin
            if (update.CharacterSkin.HasValue) characterComponent.LastSkin = update.CharacterSkin.Value;
            
            // Change Catalysts
            if (update.CharacterCards.HasValue) characterComponent.LastCards = update.CharacterCards.Value;

            // Change Mods
            if (update.CharacterMods.HasValue) characterComponent.LastMods = update.CharacterMods.Value;

            // Change Ability VFX
            if (update.CharacterAbilityVfxSwaps.HasValue) characterComponent.LastAbilityVfxSwaps = update.CharacterAbilityVfxSwaps.Value;

            // Change Loadout
            if (update.CharacterLoadoutChanges.HasValue)
            {
                characterComponent.CharacterLoadouts = update.CharacterLoadoutChanges.Value.CharacterLoadoutChanges;
            }
            if (update.LastSelectedLoadout.HasValue)
            {
                characterComponent.LastSelectedLoadout = update.LastSelectedLoadout.Value;
            }


            DB.Get().AccountDao.UpdateAccount(account);
            LobbyServerPlayerInfo playerInfo = SessionManager.UpdateLobbyServerPlayerInfo(AccountId);


            if (request.GameType != null && request.GameType.HasValue)
                SetGameType(request.GameType.Value);

            if (update.CharacterType != null && update.CharacterType.HasValue)
            {
                PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification()
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
            if (update.LastSelectedLoadout != null && update.LastSelectedLoadout.HasValue)
                SetLastSelectedLoadout(update.LastSelectedLoadout.Value);

            
            PlayerInfoUpdateResponse response = new PlayerInfoUpdateResponse()
            {
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
                CharacterInfo = playerInfo.CharacterInfo,
                OriginalPlayerInfoUpdate = update,
                ResponseId = request.RequestId
            };

            if (CurrentServer != null)
            {
                CurrentServer.SendGameInfoNotifications();
                if (CurrentServer.GameInfo.GameStatus == GameStatus.FreelancerSelecting && update.CharacterType.HasValue)
                {
                    Send(new ForcedCharacterChangeFromServerNotification()
                    {
                        ChararacterInfo = playerInfo.CharacterInfo,
                    });
                }
            }
            Send(response);
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
            LeaveGameResponse response = new LeaveGameResponse()
            {
                Success = true,
                ResponseId = request.RequestId
            };
            Send(response);

            Send(new GameStatusNotification() { GameStatus = GameStatus.Stopped });
            Send(new GameAssignmentNotification()
            {
                GameInfo = null,
                GameResult = GameResult.NoResult,
                Reconnection = false
            });

            if (CurrentServer != null)
            {
                LobbyServerProtocol client = SessionManager.GetClientConnection(AccountId);
                if (CurrentServer.GetPlayerInfo(AccountId).CharacterType != client.OldCharacter)
                {
                    CurrentServer.ResetCharacterToOriginal(client);
                }
                // We set it back on reconnect if need be
                client.OldCharacter = CharacterType.None;

                if (CurrentServer.ServerGameStatus == GameStatus.Stopped)
                {
                    CurrentServer = null;
                }
            }
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
                        CurrentServer.GetPlayerInfo(AccountId).ReadyState = ReadyState.Ready;
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
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            ChatNotification message = new ChatNotification
            {
                SenderAccountId = AccountId,
                SenderHandle = account.Handle,
                ResponseId = notification.RequestId,
                CharacterType = account.AccountComponent.LastCharacter,
                ConsoleMessageType = notification.ConsoleMessageType,
                Text = notification.Text,
                EmojisAllowed = InventoryManager.GetUnlockedEmojiIDs(AccountId),
                DisplayDevTag = false,
            };

            LobbyServerPlayerInfo lobbyServerPlayerInfo = null;
            if (CurrentServer != null)
            {
                lobbyServerPlayerInfo = CurrentServer.GetPlayerInfo(AccountId);
                if (lobbyServerPlayerInfo != null)
                {
                    message.SenderTeam = lobbyServerPlayerInfo.TeamId;
                }
                else
                {
                    log.Error($"{AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} but they are not in the game they are supposed to be in");
                }
            }
            
            switch (notification.ConsoleMessageType)
            {
                case ConsoleMessageType.GlobalChat:
                {
                    Broadcast(message);
                    break;
                }
                case ConsoleMessageType.WhisperChat:
                {
                    long? accountId = SessionManager.GetOnlinePlayerByHandle(notification.RecipientHandle);
                    if (accountId.HasValue)
                    {
                        message.RecipientHandle = notification.RecipientHandle;
                        SessionManager.GetClientConnection((long)accountId)?.Send(message);
                        Send(message);
                    }
                    else
                    {
                        log.Warn($"{AccountId} {account.Handle} failed to whisper to {notification.RecipientHandle}");
                    }
                    break;
                }
                case ConsoleMessageType.GameChat:
                {
                    if (CurrentServer == null)
                    {
                        log.Warn($"{AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in game");
                        break;
                    }
                    foreach (long accountId in CurrentServer.GetPlayers())
                    {
                        SessionManager.GetClientConnection(accountId)?.Send(message);
                    }
                    break;
                }
                case ConsoleMessageType.GroupChat:
                {
                    LobbyPlayerGroupInfo group = GroupManager.GetGroupInfo(AccountId);
                    if (CurrentServer == null)
                    {
                        log.Error($"{AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in a group");
                        break;
                    }
                    foreach (UpdateGroupMemberData member in group.Members)
                    {
                        SessionManager.GetClientConnection(member.AccountID)?.Send(message);
                    }
                    break;
                }
                case ConsoleMessageType.TeamChat:
                {
                    if (CurrentServer == null)
                    {
                        log.Warn($"{AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in game");
                        break;
                    }
                    if (lobbyServerPlayerInfo == null)
                    {
                        log.Error($"{AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} but they are not in the game they are supposed to be in");
                        break;
                    }
                    foreach (long teammateAccountId in CurrentServer.GetPlayers(lobbyServerPlayerInfo.TeamId))
                    {
                        SessionManager.GetClientConnection(teammateAccountId)?.Send(message);
                    }
                    break;
                }
                default:
                {
                    log.Error($"Console message type {notification.ConsoleMessageType} is not supported yet!");
                    log.Info(DefaultJsonSerializer.Serialize(notification));
                    break;
                }
            }
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
            
            GroupInfo group = GroupManager.GetPlayerGroup(AccountId);

            if (group.Members.Count == LobbyConfiguration.GetMaxGroupSize())
            {
                log.Warn($"{AccountId} try'd to invite {request.FriendHandle} into a full group");
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
                joinType = group.IsSolo()
                    ? GroupConfirmationRequest.JoinType.InviteToFormGroup
                    : GroupConfirmationRequest.JoinType.RequestToJoinGroup;
            }

            PersistedAccountData requester = DB.Get().AccountDao.GetAccount(AccountId);
            PersistedAccountData leader = DB.Get().AccountDao.GetAccount(group.Leader);
            LobbyServerProtocol friend = SessionManager.GetClientConnection((long) friendAccountId);
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

            BroadcastRefreshFriendList();
            BroadcastRefreshGroup();
            
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
            
                BroadcastRefreshFriendList();
                BroadcastRefreshGroup();
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
                GGPackUserName = account.UserName,
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
                foreach(LobbyServerProtocol client in CurrentServer.GetClients())
                {
                    if (client.AccountId != AccountId)
                    {
                        UseGGPackNotification useGGPackNotification = new UseGGPackNotification()
                        {
                            GGPackUserName = account.UserName,
                            GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
                            GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
                            GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
                            GGPackUserTitle = account.AccountComponent.SelectedTitleID,
                            GGPackUserTitleLevel = 1
                        };
                        client.Send(useGGPackNotification);
                    }
                    CurrentServer.OnPlayerUsedGGPack(AccountId);
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

        public void HandleGroupChatRequest(GroupChatRequest request)
        {
            Send(new GroupChatResponse
            {
                Text = request.Text,
                ResponseId = request.RequestId,
                Success = true
            });

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            ChatNotification message = new ChatNotification()
            {
                SenderAccountId = AccountId,
                EmojisAllowed = request.RequestedEmojis,
                CharacterType = account.AccountComponent.LastCharacter,
                ConsoleMessageType = ConsoleMessageType.GroupChat,
                SenderHandle = account.Handle,
                Text = request.Text
            };

            foreach (long accountID in GroupManager.GetPlayerGroup(AccountId).Members)
            {
                LobbyServerProtocol connection = SessionManager.GetClientConnection(accountID);
                connection.Send(message);
            }
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
            LobbyServerPlayerInfo playerInfo;
            try
            {
                playerInfo = server.GetPlayerInfo(AccountId);
            }
            catch (EvosException)
            {
                // no longer in a game
                Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
                return;
            }

            Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = true });

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            LobbyCharacterInfo character = LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]);

            CharacterType oldCharacterType = server.TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == AccountId).CharacterType;

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

            CurrentServer = server;

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
                       $"Game {new DateTime(server.GameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}."
            });
        }

        public void CloseConnection()
        {
            this.WebSocket.Close();
        }

        private void HandleClientFeedbackReport(ClientFeedbackReport message)
        {
            if (LobbyConfiguration.GetAdminChannelWebhook().MaybeUri())
            {
                try
                {
                    DiscordWebhookClient discord = new DiscordWebhookClient(LobbyConfiguration.GetAdminChannelWebhook());
                    LobbyServerPlayerInfo playerInfo = SessionManager.GetPlayerInfo(this.AccountId);
                    EmbedBuilder eb = new EmbedBuilder()
                    {
                        Title = $"User Report From: {playerInfo.Handle}",
                        Description = message.Message,
                        Color = 16711680
                    };
                    eb.AddField("Reason", message.Reason, true);
                    if (message.ReportedPlayerHandle != null)
                    {
                        eb.AddField("Reported Account", message.ReportedPlayerHandle, true);
                    }
                    Embed[] embedArray = new Embed[] { eb.Build() };
                    discord.SendMessageAsync(null, false, embeds: embedArray, "Atlas Reactor");
                }
                catch (Exception e)
                {
                    log.Info($"Failed to send report to discord webhook {e.Message}");
                }
            }
        }
    }
}
