using System;
using System.Collections.Generic;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Group;

public interface IGroupRegistry
{
    /// <summary>
    /// A group's members or leader changed: a player joined or left (the group may now be empty and
    /// disbanded), or the leader promoted another member. Whatever the group was queued for no longer
    /// applies. May be raised under <see cref="Lock"/>.
    /// </summary>
    event Action<GroupInfo> OnMembersChanged;

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
    void Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId = 0);
    void BroadcastSystemMessage(GroupInfo group, LocalizationPayload message, long skipAccountId = 0);
    ushort GetGroupSubTypeMask(long groupId);
    ushort GetGroupSubTypeMask(GroupInfo groupInfo);
}
