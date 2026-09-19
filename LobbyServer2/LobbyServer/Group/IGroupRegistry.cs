using System;
using System.Collections.Generic;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Group;

public interface IGroupRegistry
{
    object Lock { get; }
    GroupInfo GetGroup(long groupId);
    List<long> GetGroupMembers(long groupId);
    List<GroupInfo> GetGroups();
    GroupInfo GetPlayerGroup(long accountId);
    long CreateGroupRequest(long requesterAccountId, long requesteeAccountId, long groupId,
        GroupConfirmationRequest.JoinType joinType, TimeSpan expirationTime);
    GroupRequestInfo PopGroupRequest(long requestId);
    void PingGroupRequests();
    void CreateGroup(long leader);
    bool LeaveGroup(long accountId, bool warnIfNotInAGroup = true, bool wasKicked = false);
    void JoinGroup(long groupId, long accountId);
    bool PromoteMember(GroupInfo groupInfo, long accountId);
    LobbyPlayerGroupInfo GetGroupInfo(long accountId);
    long GetGroupID(long accountId);
    void OnLeaveQueue(long groupId);
    void Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId = 0);
    void BroadcastSystemMessage(GroupInfo group, LocalizationPayload message, long skipAccountId = 0);
    ushort GetGroupSubTypeMask(long groupId);
    ushort GetGroupSubTypeMask(GroupInfo groupInfo);
    void UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true);
    void UpdateSelectedSubTypesForAccount(long accountId);
}
