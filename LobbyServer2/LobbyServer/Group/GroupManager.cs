using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer.Group
{
    class GroupManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GroupManager));
        
        private static readonly Dictionary<long, GroupInfo> ActiveGroups = new();
        private static readonly Dictionary<long, long> PlayerToGroup = new();
        private static readonly Dictionary<long, GroupRequestInfo> GroupRequests = new();
        
        private static long _lastGroupId = -1;
        private static long _lastGroupRequestId = -1;
        private static readonly object _lock = new object();

        public static object Lock => _lock;
        
        public static GroupInfo GetGroup(long groupId)
        {
            return ActiveGroups.GetValueOrDefault(groupId);
        }
        
        public static List<GroupInfo> GetGroups()
        {
            return ActiveGroups.Values.ToList();
        }
        
        public static GroupInfo GetPlayerGroup(long accountId)
        {
            lock (_lock)
            {
                if (PlayerToGroup.TryGetValue(accountId, out long groupId))
                {
                    return ActiveGroups[groupId];
                }
                else if (SessionManager.GetClientConnection(accountId) is not null)
                {
                    log.Error($"Player {LobbyServerUtils.GetHandle(accountId)} wasn't in any group");
                    CreateGroup(accountId);
                    return PlayerToGroup.TryGetValue(accountId, out groupId)
                        ? ActiveGroups[groupId]
                        : null;
                }
            }

            return null;
        }
        
        public static long CreateGroupRequest(
            long requesterAccountId,
            long requesteeAccountId,
            long groupId,
            GroupConfirmationRequest.JoinType joinType,
            TimeSpan expirationTime)
        {
            lock (_lock)
            {
                if (!ActiveGroups.ContainsKey(groupId))
                {
                    throw new ArgumentException("Invalid group id");
                }

                long requestId = Interlocked.Increment(ref _lastGroupRequestId);
                GroupRequests.Add(
                    requestId,
                    new GroupRequestInfo(
                        requestId,
                        requesterAccountId,
                        requesteeAccountId,
                        groupId,
                        joinType,
                        DateTime.UtcNow.Add(expirationTime)));
                return requestId;
            }
        }

        public static GroupRequestInfo PopGroupRequest(long requestId)
        {
            lock (_lock)
            {
                GroupRequests.Remove(requestId, out GroupRequestInfo requestInfo);
                if (requestInfo is not null && requestInfo.HasExpiredPadded)
                {
                    log.Error(
                        $"Attempted to access an expired group {
                            (requestInfo.JoinType == GroupConfirmationRequest.JoinType.InviteToFormGroup
                                ? "invitation"
                                : "request")
                        } {requestId} to {requestInfo.RequesteeAccountId} to join group {
                            requestInfo.GroupId} by {requestInfo.RequesterAccountId}");
                    return null;
                }
                return requestInfo;
            }
        }

        public static void PingGroupRequests()
        {
            lock (_lock)
            {
                List<long> requestsToRemove = new List<long>();
                foreach (var (id, request) in GroupRequests)
                {
                    if (!request.HasExpiredPadded)
                    {
                        continue;
                    }
                    
                    LobbyServerProtocol requesterConn = SessionManager.GetClientConnection(request.RequesterAccountId);
                    LobbyServerProtocol requesteeConn = SessionManager.GetClientConnection(request.RequesteeAccountId);
                    if (requesteeConn is not null)
                    {
                        log.Warn($"Request {id} to {LobbyServerUtils.GetHandleForLog(
                            request.RequesteeAccountId)} has expired while they were online");
                    }
                        
                    if (requesteeConn is null && !request.IsInvitation)
                    {
                        requesterConn?.SendSystemMessage(GroupMessages.LeaderLoggedOff);
                    }
                    else
                    {
                        requesterConn?.SendSystemMessage(
                            request.IsInvitation
                                ? GroupMessages.JoinGroupOfferExpired(request.RequesteeAccountId)
                                : GroupMessages.FailedToJoinGroupInviteExpired(request.RequesteeAccountId));
                    }

                    requestsToRemove.Add(id);
                }
                
                requestsToRemove.ForEach(id => GroupRequests.Remove(id));
            }
        }
        
        public static void CreateGroup(long leader) {
            LeaveGroup(leader, false);
            long groupId;
            lock (_lock)
            {
                groupId = Interlocked.Increment(ref _lastGroupId);
                ActiveGroups.Add(groupId, new GroupInfo(groupId));
            }
            JoinGroup(groupId, leader);
        }

        public static bool LeaveGroup(long accountId, bool warnIfNotInAGroup = true, bool wasKicked = false)
        {
            GroupInfo leftGroup = null;
            bool wasLeader = false;
            lock (_lock)
            {
                if (PlayerToGroup.TryGetValue(accountId, out long groupId))
                {
                    GroupInfo groupInfo = ActiveGroups[groupId];
                    wasLeader = groupInfo.IsLeader(accountId);
                    groupInfo.RemovePlayer(accountId);
                    PlayerToGroup.Remove(accountId);
                    log.Info($"Removed {accountId} from group {groupId}");
                    if (groupInfo.IsEmpty())
                    {
                        ActiveGroups.Remove(groupId);
                        log.Info($"Group {groupId} disbanded");
                    }
                    leftGroup = groupInfo;
                }
                else if (warnIfNotInAGroup)
                {
                    log.Warn($"Player {accountId} attempted to leave a group while not being in one");
                }
            }

            if (leftGroup != null)
            {
                OnLeaveGroup(accountId);
                OnGroupMembersUpdated(leftGroup);
                BroadcastSystemMessage(
                    leftGroup,
                    wasKicked
                        ? GroupMessages.MemberKickedFromGroup(accountId)
                        : GroupMessages.MemberLeftGroup(accountId));
                if (leftGroup.IsSolo())
                {
                    BroadcastSystemMessage(leftGroup, GroupMessages.GroupDisbanded);
                }
                else if (wasLeader)
                {
                    BroadcastSystemMessage(leftGroup, GroupMessages.NewLeader(leftGroup.Leader));
                }
            }

            return leftGroup != null;
        }

        public static void JoinGroup(long groupId, long accountId)
        {
            GroupInfo joinedGroup = null;
            GroupInfo groupInfo = null;
            bool isGroupFull = false;
            lock (_lock)
            {
                LeaveGroup(accountId, false);
                if (ActiveGroups.TryGetValue(groupId, out groupInfo))
                {
                    if (groupInfo.Members.Count < LobbyConfiguration.GetMaxGroupSize())
                    {
                        groupInfo.AddPlayer(accountId);
                        PlayerToGroup.Add(accountId, groupId);
                        log.Info($"Added {accountId} to group {groupId}");
                        joinedGroup = groupInfo;
                    } 
                    else
                    {
                        log.Error($"Player {accountId} attempted to join a full group {groupId}");
                        isGroupFull = true;
                    }
                }
                else
                {
                    log.Error($"Player {accountId} attempted to join a non-existing group {groupId}");
                }
            }

            if (joinedGroup != null)
            {
                OnJoinGroup(accountId);
                OnGroupMembersUpdated(joinedGroup);
                
                BroadcastSystemMessage(joinedGroup, GroupMessages.MemberJoinedGroup(accountId), accountId);
            }
            else
            {
                BroadcastSystemMessage(
                    groupInfo,
                    isGroupFull
                        ? GroupMessages.MemberFailedToJoinGroupIsFull(accountId)
                        : GroupMessages.MemberFailedToJoinUnknownError(accountId));
                SessionManager.GetClientConnection(accountId)?.SendSystemMessage(
                    isGroupFull
                        ? GroupMessages.FailedToJoinGroupIsFull
                        : GroupMessages.FailedToJoinUnknownError);
                
                
                CreateGroup(accountId);
            }
        }

        public static bool PromoteMember(GroupInfo groupInfo, long accountId)
        {
            bool success;
            lock (Lock)
            {
                success = groupInfo.SetLeader(accountId);
            }

            if (success)
            {
                BroadcastSystemMessage(groupInfo, GroupMessages.NewLeader(accountId));
            }

            return success;
        }

        private static UpdateGroupMemberData GetMemberData(GroupInfo groupInfo, long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            LobbyServerProtocol session = SessionManager.GetClientConnection(accountId);
            CharacterComponent characterComponent = account.CharacterData[account.AccountComponent.LastCharacter].CharacterComponent;

            return new UpdateGroupMemberData
            {
                MemberDisplayName = account.Handle,
                MemberHandle = account.Handle,
                HasFullAccess = true,
                IsLeader = groupInfo.IsLeader(account.AccountId),
                IsReady = session?.IsReady == true,
                IsInGame = session?.IsInGame() == true,
                CreateGameTimestamp = session?.CurrentGame?.GameInfo?.CreateTimestamp ?? 0L,
                AccountID = account.AccountId,
                MemberDisplayCharacter = account.AccountComponent.LastCharacter,
                VisualData = new GroupMemberVisualData
                {
                    VisualInfo = characterComponent.LastSkin,
                    ForegroundBannerID = account.AccountComponent.SelectedForegroundBannerID,
                    BackgroundBannerID = account.AccountComponent.SelectedBackgroundBannerID,
                    TitleID = account.AccountComponent.SelectedTitleID,
                    RibbonID = account.AccountComponent.SelectedRibbonID,
                },
                PenaltyTimeout = DateTime.MinValue,
                GameLeavingPoints = 0
            };
        }
        
        public static LobbyPlayerGroupInfo GetGroupInfo(long accountId)
        {
            GroupInfo groupInfo = null;
            lock (_lock)
            {
                if (PlayerToGroup.TryGetValue(accountId, out long groupId))
                {
                    groupInfo = ActiveGroups[groupId];
                }
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            LobbyServerProtocol client = SessionManager.GetClientConnection(accountId);
            LobbyPlayerGroupInfo response;
            if (groupInfo == null || groupInfo.IsSolo())
            {
                response = new LobbyPlayerGroupInfo
                {
                    SelectedQueueType = client.SelectedGameType,
                    MemberDisplayName = account.Handle,
                    InAGroup = false,
                    // IsLeader = true,
                    Members = new List<UpdateGroupMemberData>(),
                };
            }
            else
            {
                LobbyServerProtocol leader = SessionManager.GetClientConnection(groupInfo.Leader);
                response = new LobbyPlayerGroupInfo
                {
                    SelectedQueueType = leader.SelectedGameType,
                    SubTypeMask = leader.SelectedSubTypeMask,
                    MemberDisplayName = account.Handle,
                    InAGroup = true,
                    IsLeader = groupInfo.IsLeader(account.AccountId),
                    Members = groupInfo.Members.Select(id => GetMemberData(groupInfo, id)).ToList()
                };
            }
            response.SetCharacterInfo(LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]));
            return response;
        }

        public static long GetGroupID(long accountId)
        {
            lock (_lock)
            {
                if (PlayerToGroup.TryGetValue(accountId, out long groupId))
                {
                    return groupId;
                }
            }

            return -1;
        }

        private static void OnJoinGroup(long accountId)
        {
            SessionManager.GetClientConnection(accountId)?.OnJoinGroup();
        }

        private static void OnLeaveGroup(long accountId)
        {
            SessionManager.GetClientConnection(accountId)?.OnLeaveGroup();
        }

        private static void OnGroupMembersUpdated(GroupInfo groupInfo)
        {
            if (!MatchmakingManager.RemoveGroupFromQueue(groupInfo, true))
            {
                SessionManager.GetClientConnection(groupInfo.Leader)?.BroadcastRefreshGroup(true);
            }
        }
        
        public static void OnLeaveQueue(long groupId)
        {
            GroupInfo groupInfo = GetGroup(groupId);
            if (groupInfo is null)
            {
                log.Info($"Received OnLeaveQueue for group {groupId} that does not exist");
                return;
            }
            Broadcast(groupInfo, new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null });
            SessionManager.GetClientConnection(groupInfo.Leader)?.BroadcastRefreshGroup(false);
        }

        public static void Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId = 0)
        {
            foreach (long groupMember in group.Members)
            {
                if (groupMember == skipAccountId)
                {
                    continue;
                }
                SessionManager.GetClientConnection(groupMember)?.Send(message);
            }
        }

        public static void BroadcastSystemMessage(GroupInfo group, LocalizationPayload message, long skipAccountId = 0)
        {
            foreach (long groupMember in group.Members)
            {
                if (groupMember == skipAccountId)
                {
                    continue;
                }
                SessionManager.GetClientConnection(groupMember)?.SendSystemMessage(message);
            }
        }

        public static ushort GetGroupSubTypeMask(long groupId)
        {
            return GetGroupSubTypeMask(GetGroup(groupId));
        }

        public static ushort GetGroupSubTypeMask(GroupInfo groupInfo)
        {
            if (groupInfo is null)
            {
                return 0;
            }

            LobbyServerProtocol conn = SessionManager.GetClientConnection(groupInfo.Leader);
            if (conn is null)
            {
                return 0;
            }

            return Math.Max((ushort)1, conn.SelectedSubTypeMask);
        }
    }
}
