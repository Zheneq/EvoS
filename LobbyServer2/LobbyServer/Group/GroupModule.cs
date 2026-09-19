using System;
using System.Linq;
using System.Text.RegularExpressions;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Group;

public class GroupModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GroupModule));
    private readonly IClientConnection _conn;
    private readonly IGroupRegistry _groupRegistry;

    public GroupModule(IClientConnection conn, IGroupRegistry groupRegistry)
    {
        _conn = conn;
        _groupRegistry = groupRegistry;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<GroupInviteRequest>(HandleGroupInviteRequest);
        registry.Register<GroupJoinRequest>(HandleGroupJoinRequest);
        registry.Register<GroupConfirmationResponse>(HandleGroupConfirmationResponse);
        registry.Register<GroupSuggestionResponse>(HandleGroupSuggestionResponse);
        registry.Register<GroupLeaveRequest>(HandleGroupLeaveRequest);
        registry.Register<GroupKickRequest>(HandleGroupKickRequest);
        registry.Register<GroupPromoteRequest>(HandleGroupPromoteRequest);
        registry.Register<PlayerGroupInfoUpdateRequest>(HandlePlayerGroupInfoUpdateRequest);
    }

    private void HandleGroupPromoteRequest(GroupPromoteRequest request)
    {
        GroupInfo group = _groupRegistry.GetPlayerGroup(_conn.AccountId);
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
        else if (!group.IsLeader(_conn.AccountId))
        {
            response.LocalizedFailure = GroupMessages.NotTheLeader;
        }
        else if (_conn.AccountId == accountId)
        {
            response.LocalizedFailure = GroupMessages.AlreadyTheLeader;
        }
        else if (accountId.HasValue && _groupRegistry.PromoteMember(group, (long)accountId))
        {
            response.Success = true;
            _conn.BroadcastRefreshGroup();
        }
        else
        {
            response.LocalizedFailure = GroupMessages.PlayerIsNotInGroup(request.Name);
        }

        _conn.Send(response);
    }

    private void HandleGroupKickRequest(GroupKickRequest request)
    {
        GroupInfo group = _groupRegistry.GetPlayerGroup(_conn.AccountId);
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
        else if (!group.IsLeader(_conn.AccountId))
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
                response.Success = _groupRegistry.LeaveGroup(accountId.Value, false, true);
            }
            if (!response.Success)
            {
                response.LocalizedFailure = GroupMessages.PlayerIsNotInGroup(request.MemberName);
            }
            else if (accountId.HasValue)
            {
                _groupRegistry.BroadcastSystemMessage(group, GroupMessages.MemberKickedFromGroup(accountId.Value));
            }
        }
        _conn.Send(response);
    }

    private void HandleGroupInviteRequest(GroupInviteRequest request)
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
            _conn.Send(response);
            return;
        }

        if (friendAccountId == _conn.AccountId)
        {
            log.Info($"{_conn.Handle} attempted to invite themself to a group");
            response.LocalizedFailure = GroupMessages.CantInviteYourself;
            _conn.Send(response);
            return;
        }

        GroupInfo group = _groupRegistry.GetPlayerGroup(_conn.AccountId);
        if (group.Members.Contains(friendAccountId))
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} to a group when they are already there");
            response.LocalizedFailure = GroupMessages.AlreadyInYourGroup(friendAccountId);
            _conn.Send(response);
            return;
        }

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        SocialComponent socialComponent = account?.SocialComponent;
        PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);
        SocialComponent friendSocialComponent = friendAccount?.SocialComponent;
        PersistedAccountData leaderAccount = DB.Get().AccountDao.GetAccount(group.Leader);

        if (account is null || friendAccount is null || leaderAccount is null)
        {
            log.Error($"Failed to send group invite request: "
                      + $"account={account?.Handle} "
                      + $"friendAccount={friendAccount?.Handle}.");
            _conn.Send(response);
            return;
        }

        if (socialComponent?.IsBlocked(friendAccountId) == true)
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} whom they blocked to a group");
            response.LocalizedFailure = GroupMessages.YouAreBlocking(friendAccountId);
            _conn.Send(response);
            return;
        }

        if (friendSocialComponent?.IsBlocked(_conn.AccountId) == true)
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} who blocked them to a group");
            response.Success = true; // shadow ban
            _conn.Send(response);
            return;
        }

        LobbyServerProtocol friend = SessionManager.GetClientConnection(friendAccountId);
        if (friend is null) // offline
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} who is offline to a group");
            response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
            _conn.Send(response);
            return;
        }

        if (group.Members.Count == LobbyConfiguration.GetMaxGroupSize())
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} into a full group");
            response.LocalizedFailure = GroupMessages.MemberFailedToJoinGroupIsFull(request.FriendHandle);
            _conn.Send(response);
            return;
        }

        GroupInfo friendGroup = _groupRegistry.GetPlayerGroup(friendAccountId);
        if (!friendGroup.IsSolo())
        {
            log.Info($"{_conn.Handle} attempted to invite {request.FriendHandle} who is already in a group");
            response.LocalizedFailure = GroupMessages.OtherPlayerInOtherGroup(request.FriendHandle);
            _conn.Send(response);
            return;
        }

        // TODO GROUPS AleadyInvitedPlayerToGroup@Invite? You've already invited {0}, please await their response.

        TimeSpan expirationTime = LobbyConfiguration.GetGroupConfiguration().InviteTimeout;
        if (group.Leader == _conn.AccountId)
        {
            GroupConfirmationRequest.JoinType joinType = GroupConfirmationRequest.JoinType.InviteToFormGroup;
            friend.Send(new GroupConfirmationRequest
            {
                GroupId = group.GroupId,
                LeaderName = account.UserName,
                LeaderFullHandle = account.Handle,
                JoinerName = friendAccount.Handle,
                JoinerAccountId = friendAccount.AccountId,
                ConfirmationNumber = _groupRegistry.CreateGroupRequest(
                    _conn.AccountId, friendAccount.AccountId, group.GroupId, joinType, expirationTime),
                ExpirationTime = expirationTime,
                Type = joinType
            });
            if (EvosConfiguration.GetPingOnGroupRequest() && !friend.IsInGroup() && !friend.IsInGame())
            {
                friend.Send(new ChatNotification
                {
                    SenderAccountId = _conn.AccountId,
                    SenderHandle = account.Handle,
                    ConsoleMessageType = ConsoleMessageType.WhisperChat,
                    LocalizedText = LocalizationPayload.Create("GroupRequest", "Global")
                });
            }

            log.Info($"{_conn.AccountId}/{account.Handle} invited {friend.AccountId}/{request.FriendHandle} to group {group.GroupId}");
            response.Success = true;
            _conn.Send(response);

            _groupRegistry.BroadcastSystemMessage(
                group,
                GroupMessages.InvitedFriendToGroup(friendAccount.AccountId),
                _conn.AccountId);
        }
        else
        {
            LobbyServerProtocol leaderSession = SessionManager.GetClientConnection(leaderAccount.AccountId);
            leaderSession.Send(new GroupSuggestionRequest
            {
                LeaderAccountId = group.Leader,
                SuggestedAccountFullHandle = request.FriendHandle,
                SuggesterAccountName = account.Handle,
                SuggesterAccountId = _conn.AccountId,
            });
            _groupRegistry.BroadcastSystemMessage(
                group,
                GroupMessages.InviteToGroupWithYou(_conn.AccountId, friendAccount.AccountId),
                _conn.AccountId);
        }
    }

    private void HandleGroupJoinRequest(GroupJoinRequest request)
    {
        var response = new GroupJoinResponse
        {
            FriendHandle = request.FriendHandle,
            ResponseId = request.RequestId,
            Success = false
        };

        GroupInfo myGroup = _groupRegistry.GetPlayerGroup(_conn.AccountId);
        if (!myGroup.IsSolo())
        {
            log.Info($"{_conn.Handle} attempted to join {request.FriendHandle}'s group while being in another group.");
            response.LocalizedFailure = GroupMessages.CantJoinIfInGroup;
            _conn.Send(response);
            return;
        }

        long friendAccountId = SessionManager.GetOnlinePlayerByHandleOrUsername(request.FriendHandle) ?? 0;
        if (friendAccountId == 0)
        {
            log.Info($"Failed to find player {request.FriendHandle} to request to join their group.");
            response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
            _conn.Send(response);
            return;
        }

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
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
            _conn.Send(response);
            return;
        }

        if (socialComponent?.IsBlocked(friendAccountId) == true)
        {
            log.Info($"{_conn.Handle} attempted to join {request.FriendHandle}'s group whom they blocked");
            response.LocalizedFailure = GroupMessages.YouAreBlocking(friendAccountId);
            _conn.Send(response);
            return;
        }

        if (friendSocialComponent?.IsBlocked(_conn.AccountId) == true)
        {
            log.Info($"{_conn.Handle} attempted to join {request.FriendHandle}'s group who blocked them");
            response.Success = true; // shadow ban
            _conn.Send(response);
            return;
        }

        if (leaderSocialComponent?.IsBlocked(_conn.AccountId) == true)
        {
            log.Info($"{_conn.Handle} attempted to join {leaderAccount.Handle}'s group who blocked them via {request.FriendHandle}");
            response.Success = true; // shadow ban
            _conn.Send(response);
            return;
        }

        GroupInfo friendGroup = _groupRegistry.GetPlayerGroup(friendAccountId);
        if (friendGroup.IsSolo())
        {
            log.Info($"{_conn.Handle} attempted to join {request.FriendHandle}'s ({friendAccountId}) group while they are solo.");
            response.LocalizedFailure = GroupMessages.OtherPlayerNotInGroup(friendAccountId);
            _conn.Send(response);
            return;
        }

        LobbyServerProtocol friend = SessionManager.GetClientConnection(friendAccountId);
        if (friend is null) // offline
        {
            log.Info($"{_conn.Handle} attempted to join {request.FriendHandle}'s group who is offline");
            response.LocalizedFailure = GroupMessages.PlayerNotFound(request.FriendHandle);
            _conn.Send(response);
            return;
        }

        if (friendGroup.Members.Count == LobbyConfiguration.GetMaxGroupSize())
        {
            log.Warn($"{_conn.AccountId} attempted to join {request.FriendHandle}'s full group");
            response.LocalizedFailure = GroupMessages.FailedToJoinGroupIsFull;
            _conn.Send(response);
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
            JoinerAccountId = _conn.AccountId,
            ConfirmationNumber = _groupRegistry.CreateGroupRequest(
                _conn.AccountId, friendGroup.Leader, friendGroup.GroupId, joinType, expirationTime),
            ExpirationTime = expirationTime,
            Type = joinType
        });
        _groupRegistry.BroadcastSystemMessage(
            friendGroup,
            GroupMessages.RequestToJoinGroup(_conn.AccountId),
            leaderAccount.AccountId);

        response.Success = true;
        _conn.Send(response);
    }

    private void HandleGroupSuggestionResponse(GroupSuggestionResponse response)
    {
        GroupInfo group = _groupRegistry.GetPlayerGroup(_conn.AccountId);
        if (group is null)
        {
            return;
        }

        if (response.SuggestionStatus == GroupSuggestionResponse.Status.Denied)
        {
            _groupRegistry.BroadcastSystemMessage(
                group,
                GroupMessages.LeaderRejectedSuggestion); // no param for response.SuggesterAccountId
        }
        // nothing else to say as we don't know who was suggested
    }

    private void HandleGroupConfirmationResponse(GroupConfirmationResponse response)
    {
        GroupInfo myGroup = _groupRegistry.GetPlayerGroup(_conn.AccountId);
        GroupRequestInfo groupRequestInfo = _groupRegistry.PopGroupRequest(response.ConfirmationNumber);

        if (groupRequestInfo is null)
        {
            log.Error($"Player {_conn.AccountId} responded to not found request {response.ConfirmationNumber} "
                      + $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
            if (response.GroupId == myGroup.GroupId)
            {
                _conn.SendSystemMessage(GroupMessages.MemberFailedToJoinGroupInviteExpired(response.JoinerAccountId));
            }
            else
            {
                _conn.SendSystemMessage(GroupMessages.FailedToJoinGroupInviteExpired(response.JoinerAccountId));
            }
            return;
        }

        string typeForLog = groupRequestInfo.IsInvitation
            ? "invitation"
            : "request";
        if (groupRequestInfo.RequesteeAccountId != _conn.AccountId)
        {
            log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                     + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                     + $"by {response.JoinerAccountId}: {response.Acceptance}");
            _conn.SendSystemMessage(GroupMessages.FailedToJoinUnknownError);
            return;
        }

        LobbyServerProtocol requester = SessionManager.GetClientConnection(groupRequestInfo.RequesterAccountId);
        if (requester is null)
        {
            log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                     + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                     + $"by {response.JoinerAccountId} who is offline");
            if (groupRequestInfo.IsInvitation)
            {
                _conn.SendSystemMessage(GroupMessages.FailedToJoinGroupCreatorOffline);
            }
            else
            {
                _groupRegistry.BroadcastSystemMessage(
                    myGroup,
                    GroupMessages.MemberFailedToJoinGroupPlayerNotFound(groupRequestInfo.RequesterAccountId));
            }
            return;
        }

        GroupInfo requesterGroup = _groupRegistry.GetPlayerGroup(groupRequestInfo.RequesterAccountId);
        if (groupRequestInfo.IsInvitation)
        {
            if (groupRequestInfo.GroupId != requesterGroup.GroupId)
            {
                log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId} who is already in another group");
                _conn.SendSystemMessage(GroupMessages.FailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                return;
            }

            if (!myGroup.IsSolo())
            {
                log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId} but they are already in a group");
                _conn.SendSystemMessage(GroupMessages.FailedToJoinGroupCantJoinIfInGroup);
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(_conn.AccountId));
                return;
            }
        }
        else
        {
            if (groupRequestInfo.GroupId != myGroup.GroupId)
            {
                log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId} but they are already in another group");
                requester.SendSystemMessage(GroupMessages.FailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                _conn.SendSystemMessage(GroupMessages.MemberFailedToJoinGroupInviteExpired(groupRequestInfo.RequesterAccountId));
                return;
            }

            if (!requesterGroup.IsSolo())
            {
                log.Info($"Player {_conn.AccountId} responded to {typeForLog} {response.ConfirmationNumber} "
                         + $"to {groupRequestInfo.RequesteeAccountId} to join group {response.GroupId} "
                         + $"by {response.JoinerAccountId} who is already in a group");
                _conn.SendSystemMessage(GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.MemberFailedToJoinGroupOtherPlayerInOtherGroup(groupRequestInfo.RequesterAccountId));
                return;
            }
        }

        if (response.Acceptance != GroupInviteResponseType.PlayerAccepted)
        {
            log.Info($"Player {_conn.AccountId} rejected {typeForLog} {response.ConfirmationNumber} " +
                     $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
        }
        else
        {
            log.Info($"Player {_conn.AccountId} accepted {typeForLog} {response.ConfirmationNumber} " +
                     $"to join group {response.GroupId} by {response.JoinerAccountId}: {response.Acceptance}");
        }

        switch (response.Acceptance)
        {
            case GroupInviteResponseType.PlayerRejected:
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.RejectedGroupInvite(_conn.AccountId));
                break;
            case GroupInviteResponseType.OfferExpired:
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    groupRequestInfo.IsInvitation
                        ? GroupMessages.JoinGroupOfferExpired(_conn.AccountId)
                        : GroupMessages.FailedToJoinGroupInviteExpired(_conn.AccountId));
                break;
            case GroupInviteResponseType.RequestorSpamming:
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.AlreadyRejectedInvite(_conn.AccountId));
                break;
            case GroupInviteResponseType.PlayerInCustomMatch:
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.PlayerInACustomMatchAtTheMoment(_conn.AccountId));
                break;
            case GroupInviteResponseType.PlayerStillAwaitingPreviousQuery:
                _groupRegistry.BroadcastSystemMessage(
                    requesterGroup,
                    GroupMessages.PlayerStillConsideringYourPreviousInviteRequest(_conn.AccountId));
                break;
            case GroupInviteResponseType.PlayerAccepted:

                if (_conn.CurrentGame != null && !LobbyConfiguration.GetGroupConfiguration().CanInviteActiveOpponents)
                {
                    Game game = GameManager.GetGameWithPlayer(response.JoinerAccountId);

                    if (game != null && game == _conn.CurrentGame)
                    {
                        LobbyServerPlayerInfo lobbyServerOtherPlayerInfo = game.TeamInfo.TeamPlayerInfo
                            .FirstOrDefault(p => p.AccountId == response.JoinerAccountId);
                        LobbyServerPlayerInfo lobbyServerPlayerInfo = game.TeamInfo.TeamPlayerInfo
                            .FirstOrDefault(p => p.AccountId == _conn.AccountId);

                        if (lobbyServerOtherPlayerInfo?.TeamId != lobbyServerPlayerInfo?.TeamId)
                        {
                            log.Info($"Player {_conn.AccountId} is trying to accept a group invite but is currently on the opposing team.");
                            _groupRegistry.BroadcastSystemMessage(
                                requesterGroup,
                                GroupMessages.FailedToJoinGroupCantInviteActiveOpponent);
                            break;
                        }
                    }
                }

                _groupRegistry.JoinGroup(
                    groupRequestInfo.GroupId,
                    groupRequestInfo.IsInvitation
                        ? groupRequestInfo.RequesteeAccountId
                        : groupRequestInfo.RequesterAccountId);
                _conn.BroadcastRefreshFriendList();
                requester.BroadcastRefreshFriendList();
                break;
        }
    }

    private void HandleGroupLeaveRequest(GroupLeaveRequest request)
    {
        _groupRegistry.CreateGroup(_conn.AccountId);
        _conn.BroadcastRefreshFriendList();
    }

    private void HandlePlayerGroupInfoUpdateRequest(PlayerGroupInfoUpdateRequest request)
    {
        GroupInfo group = _groupRegistry.GetPlayerGroup(_conn.AccountId);
        if (!group.IsLeader(_conn.AccountId))
        {
            _conn.Send(new PlayerGroupInfoUpdateResponse
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

        _conn.Send(new PlayerGroupInfoUpdateResponse
        {
            Success = true,
            ResponseId = request.RequestId
        });
    }
}
