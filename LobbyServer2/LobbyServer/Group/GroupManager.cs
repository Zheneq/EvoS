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
    public class GroupManager : IGroupRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GroupManager));

        public static GroupManager Instance { get; internal set; } = new GroupManager();

        private readonly Dictionary<long, GroupInfo> ActiveGroups = new();
        private readonly Dictionary<long, long> PlayerToGroup = new();
        private readonly Dictionary<long, GroupRequestInfo> GroupRequests = new();

        private long _lastGroupId = -1;
        private long _lastGroupRequestId = -1;
        private readonly object _lock = new object();

        // Static accessor for callers that use GroupManager.Lock directly
        public static object Lock => Instance._lock;

        // ---- Static forwarders (preserve all existing call sites unchanged) ----

        public static GroupInfo GetGroup(long groupId) => Instance.GetGroupCore(groupId);

        public static List<long> GetGroupMembers(long groupId) => Instance.GetGroupMembersCore(groupId);

        public static List<GroupInfo> GetGroups() => Instance.GetGroupsCore();

        public static GroupInfo GetPlayerGroup(long accountId) => Instance.GetPlayerGroupCore(accountId);

        public static long CreateGroupRequest(
            long requesterAccountId,
            long requesteeAccountId,
            long groupId,
            GroupConfirmationRequest.JoinType joinType,
            TimeSpan expirationTime)
            => Instance.CreateGroupRequestCore(requesterAccountId, requesteeAccountId, groupId, joinType, expirationTime);

        public static GroupRequestInfo PopGroupRequest(long requestId) => Instance.PopGroupRequestCore(requestId);

        public static void PingGroupRequests() => Instance.PingGroupRequestsCore();

        public static void CreateGroup(long leader) => Instance.CreateGroupCore(leader);

        public static bool LeaveGroup(long accountId, bool warnIfNotInAGroup = true, bool wasKicked = false)
            => Instance.LeaveGroupCore(accountId, warnIfNotInAGroup, wasKicked);

        public static void JoinGroup(long groupId, long accountId) => Instance.JoinGroupCore(groupId, accountId);

        public static bool PromoteMember(GroupInfo groupInfo, long accountId)
            => Instance.PromoteMemberCore(groupInfo, accountId);

        public static LobbyPlayerGroupInfo GetGroupInfo(long accountId) => Instance.GetGroupInfoCore(accountId);

        public static long GetGroupID(long accountId) => Instance.GetGroupIDCore(accountId);

        public static void OnLeaveQueue(long groupId) => Instance.OnLeaveQueueCore(groupId);

        public static void Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId = 0)
            => Instance.BroadcastCore(group, message, skipAccountId);

        public static void BroadcastSystemMessage(GroupInfo group, LocalizationPayload message, long skipAccountId = 0)
            => Instance.BroadcastSystemMessageCore(group, message, skipAccountId);

        public static ushort GetGroupSubTypeMask(long groupId) => Instance.GetGroupSubTypeMaskCore(groupId);

        public static ushort GetGroupSubTypeMask(GroupInfo groupInfo) => Instance.GetGroupSubTypeMaskCore(groupInfo);

        public static void UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true)
            => Instance.UpdateSelectedSubTypesCore(groupInfo, resetReadyStateIfUpdated);

        public static void UpdateSelectedSubTypesForAccount(long accountId)
            => Instance.UpdateSelectedSubTypesForAccountCore(accountId);

        // ---- Explicit IGroupRegistry implementation ----

        object IGroupRegistry.Lock => _lock;
        GroupInfo IGroupRegistry.GetGroup(long groupId) => GetGroupCore(groupId);
        List<long> IGroupRegistry.GetGroupMembers(long groupId) => GetGroupMembersCore(groupId);
        List<GroupInfo> IGroupRegistry.GetGroups() => GetGroupsCore();
        GroupInfo IGroupRegistry.GetPlayerGroup(long accountId) => GetPlayerGroupCore(accountId);
        long IGroupRegistry.CreateGroupRequest(long requesterAccountId, long requesteeAccountId, long groupId,
            GroupConfirmationRequest.JoinType joinType, TimeSpan expirationTime)
            => CreateGroupRequestCore(requesterAccountId, requesteeAccountId, groupId, joinType, expirationTime);
        GroupRequestInfo IGroupRegistry.PopGroupRequest(long requestId) => PopGroupRequestCore(requestId);
        void IGroupRegistry.PingGroupRequests() => PingGroupRequestsCore();
        void IGroupRegistry.CreateGroup(long leader) => CreateGroupCore(leader);
        bool IGroupRegistry.LeaveGroup(long accountId, bool warnIfNotInAGroup, bool wasKicked)
            => LeaveGroupCore(accountId, warnIfNotInAGroup, wasKicked);
        void IGroupRegistry.JoinGroup(long groupId, long accountId) => JoinGroupCore(groupId, accountId);
        bool IGroupRegistry.PromoteMember(GroupInfo groupInfo, long accountId) => PromoteMemberCore(groupInfo, accountId);
        LobbyPlayerGroupInfo IGroupRegistry.GetGroupInfo(long accountId) => GetGroupInfoCore(accountId);
        long IGroupRegistry.GetGroupID(long accountId) => GetGroupIDCore(accountId);
        void IGroupRegistry.OnLeaveQueue(long groupId) => OnLeaveQueueCore(groupId);
        void IGroupRegistry.Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId)
            => BroadcastCore(group, message, skipAccountId);
        void IGroupRegistry.BroadcastSystemMessage(GroupInfo group, LocalizationPayload message, long skipAccountId)
            => BroadcastSystemMessageCore(group, message, skipAccountId);
        ushort IGroupRegistry.GetGroupSubTypeMask(long groupId) => GetGroupSubTypeMaskCore(groupId);
        ushort IGroupRegistry.GetGroupSubTypeMask(GroupInfo groupInfo) => GetGroupSubTypeMaskCore(groupInfo);
        void IGroupRegistry.UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated)
            => UpdateSelectedSubTypesCore(groupInfo, resetReadyStateIfUpdated);
        void IGroupRegistry.UpdateSelectedSubTypesForAccount(long accountId)
            => UpdateSelectedSubTypesForAccountCore(accountId);

        // ---- Core (instance) implementations ----

        private GroupInfo GetGroupCore(long groupId)
        {
            return ActiveGroups.GetValueOrDefault(groupId);
        }

        private List<long> GetGroupMembersCore(long groupId)
        {
            GroupInfo groupInfo = GetGroupCore(groupId);
            return groupInfo is null ? new List<long>() : groupInfo.Members;
        }

        private List<GroupInfo> GetGroupsCore()
        {
            return ActiveGroups.Values.ToList();
        }

        private GroupInfo GetPlayerGroupCore(long accountId)
        {
            lock (_lock)
            {
                if (PlayerToGroup.TryGetValue(accountId, out long groupId))
                {
                    return ActiveGroups[groupId];
                }
                else if (ClientNotifier.Get().IsOnline(accountId))
                {
                    log.Error($"Player {LobbyServerUtils.GetHandle(accountId)} wasn't in any group");
                    CreateGroupCore(accountId);
                    return PlayerToGroup.TryGetValue(accountId, out groupId)
                        ? ActiveGroups[groupId]
                        : null;
                }
            }

            return null;
        }

        private long CreateGroupRequestCore(
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

        private GroupRequestInfo PopGroupRequestCore(long requestId)
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

        private void PingGroupRequestsCore()
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

                    bool requesteeOnline = ClientNotifier.Get().IsOnline(request.RequesteeAccountId);
                    if (requesteeOnline)
                    {
                        log.Warn($"Request {id} to {LobbyServerUtils.GetHandleForLog(
                            request.RequesteeAccountId)} has expired while they were online");
                    }

                    if (!requesteeOnline && !request.IsInvitation)
                    {
                        ClientNotifier.Get().SendSystemMessage(request.RequesterAccountId, GroupMessages.LeaderLoggedOff);
                    }
                    else
                    {
                        ClientNotifier.Get().SendSystemMessage(
                            request.RequesterAccountId,
                            request.IsInvitation
                                ? GroupMessages.JoinGroupOfferExpired(request.RequesteeAccountId)
                                : GroupMessages.FailedToJoinGroupInviteExpired(request.RequesteeAccountId));
                    }

                    requestsToRemove.Add(id);
                }

                requestsToRemove.ForEach(id => GroupRequests.Remove(id));
            }
        }

        private void CreateGroupCore(long leader)
        {
            LeaveGroupCore(leader, false);
            long groupId;
            lock (_lock)
            {
                groupId = Interlocked.Increment(ref _lastGroupId);
                ActiveGroups.Add(groupId, new GroupInfo(groupId));
            }
            JoinGroupCore(groupId, leader);
        }

        private bool LeaveGroupCore(long accountId, bool warnIfNotInAGroup = true, bool wasKicked = false)
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
                BroadcastSystemMessageCore(
                    leftGroup,
                    wasKicked
                        ? GroupMessages.MemberKickedFromGroup(accountId)
                        : GroupMessages.MemberLeftGroup(accountId));
                if (leftGroup.IsSolo())
                {
                    BroadcastSystemMessageCore(leftGroup, GroupMessages.GroupDisbanded);
                    OnGroupDisbanded(leftGroup.Leader);
                }
                else if (wasLeader)
                {
                    BroadcastSystemMessageCore(leftGroup, GroupMessages.NewLeader(leftGroup.Leader));
                }
            }

            return leftGroup != null;
        }

        private void JoinGroupCore(long groupId, long accountId)
        {
            GroupInfo joinedGroup = null;
            GroupInfo groupInfo = null;
            bool isGroupFull = false;
            lock (_lock)
            {
                LeaveGroupCore(accountId, false);
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

                BroadcastSystemMessageCore(joinedGroup, GroupMessages.MemberJoinedGroup(accountId), accountId);
            }
            else
            {
                BroadcastSystemMessageCore(
                    groupInfo,
                    isGroupFull
                        ? GroupMessages.MemberFailedToJoinGroupIsFull(accountId)
                        : GroupMessages.MemberFailedToJoinUnknownError(accountId));
                ClientNotifier.Get().SendSystemMessage(
                    accountId,
                    isGroupFull
                        ? GroupMessages.FailedToJoinGroupIsFull
                        : GroupMessages.FailedToJoinUnknownError);


                CreateGroupCore(accountId);
            }
        }

        private bool PromoteMemberCore(GroupInfo groupInfo, long accountId)
        {
            bool success;
            lock (_lock)
            {
                success = groupInfo.SetLeader(accountId);
            }

            if (success)
            {
                UpdateSelectedSubTypesCore(groupInfo);
                BroadcastSystemMessageCore(groupInfo, GroupMessages.NewLeader(accountId));
            }

            return success;
        }

        private UpdateGroupMemberData GetMemberData(GroupInfo groupInfo, long accountId)
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

        private LobbyPlayerGroupInfo GetGroupInfoCore(long accountId)
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
                    SelectedQueueType = client?.SelectedGameType ?? GameType.None,
                    SubTypeMask = groupInfo?.SubTypeMask ?? 0,
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
                    SelectedQueueType = leader?.SelectedGameType ?? GameType.None,
                    SubTypeMask = groupInfo.SubTypeMask,
                    MemberDisplayName = account.Handle,
                    InAGroup = true,
                    IsLeader = groupInfo.IsLeader(account.AccountId),
                    Members = groupInfo.Members.Select(id => GetMemberData(groupInfo, id)).ToList()
                };
            }
            response.SetCharacterInfo(LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]));
            return response;
        }

        private long GetGroupIDCore(long accountId)
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

        private void OnJoinGroup(long accountId)
        {
            ClientNotifier.Get().NotifyJoinedGroup(accountId);
        }

        private void OnLeaveGroup(long accountId)
        {
            ClientNotifier.Get().NotifyLeftGroup(accountId);
        }

        private void OnGroupDisbanded(long accountId)
        {
            ClientNotifier.Get().NotifyGroupDisbanded(accountId);
        }

        private void OnGroupMembersUpdated(GroupInfo groupInfo)
        {
            MatchmakingManager.RemoveGroupFromQueue(groupInfo, true);
            if (!groupInfo.IsEmpty())
            {
                UpdateSelectedSubTypesCore(groupInfo);
            }
            ClientNotifier.Get().BroadcastRefreshGroup(groupInfo.Leader, true);
        }

        private void OnLeaveQueueCore(long groupId)
        {
            GroupInfo groupInfo = GetGroupCore(groupId);
            if (groupInfo is null)
            {
                log.Info($"Received OnLeaveQueue for group {groupId} that does not exist");
                return;
            }
            UpdateSelectedSubTypesCore(groupInfo, false);
            BroadcastCore(groupInfo, new MatchmakingQueueAssignmentNotification { MatchmakingQueueInfo = null });
            ClientNotifier.Get().BroadcastRefreshGroup(groupInfo.Leader, false);
        }

        private void BroadcastCore(GroupInfo group, WebSocketMessage message, long skipAccountId = 0)
        {
            foreach (long groupMember in group.Members)
            {
                if (groupMember == skipAccountId)
                {
                    continue;
                }
                ClientNotifier.Get().Send(groupMember, message);
            }
        }

        private void BroadcastSystemMessageCore(GroupInfo group, LocalizationPayload message, long skipAccountId = 0)
        {
            foreach (long groupMember in group.Members)
            {
                if (groupMember == skipAccountId)
                {
                    continue;
                }
                ClientNotifier.Get().SendSystemMessage(groupMember, message);
            }
        }

        private ushort GetGroupSubTypeMaskCore(long groupId)
        {
            return GetGroupSubTypeMaskCore(GetGroupCore(groupId));
        }

        private ushort GetGroupSubTypeMaskCore(GroupInfo groupInfo)
        {
            if (groupInfo is null)
            {
                return 0;
            }

            return groupInfo.SubTypeMask;
        }

        private void UpdateSelectedSubTypesCore(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true)
        {
            LobbyServerProtocol leaderConn = SessionManager.GetClientConnection(groupInfo.Leader);
            if (leaderConn is null)
            {
                log.Error($"UpdateSelectedSubTypes for group {groupInfo.GroupId} with missing leader {groupInfo.Leader}");
                return;
            }

            MatchmakingQueue queue = MatchmakingManager.GetQueue(leaderConn.SelectedGameType);
            if (queue is null)
            {
                log.Error($"UpdateSelectedSubTypes for group {groupInfo.GroupId} with unavailable queue {leaderConn.SelectedGameType}");
                return;
            }

            ushort newMask = queue.FilterSubTypeMask(groupInfo, leaderConn.GetSubTypeMask());

            ushort oldMask = groupInfo.SubTypeMask;
            if (oldMask != newMask)
            {
                groupInfo.SubTypeMask = newMask;
                if (resetReadyStateIfUpdated)
                {
                    log.Info($"UpdateSelectedSubTypes resetting group {groupInfo.GroupId} ready state "
                             + $"(was {queue.DebugFormatSubTypeMask(oldMask)}, now {queue.DebugFormatSubTypeMask(newMask)})");
                    leaderConn.BroadcastRefreshGroup(true);
                }
            }
        }

        private void UpdateSelectedSubTypesForAccountCore(long accountId)
        {
            var groupInfo = GetPlayerGroupCore(accountId);
            if (groupInfo is not null)
            {
                UpdateSelectedSubTypesCore(groupInfo);
            }
        }
    }
}
