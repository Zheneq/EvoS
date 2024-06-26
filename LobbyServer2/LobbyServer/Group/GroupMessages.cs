using CentralServer.LobbyServer.Utils;

namespace CentralServer.LobbyServer.Group;

public static class GroupMessages
{
    public static LocalizationPayload MemberJoinedGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "MemberJoinedGroup",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }
    
    public static LocalizationPayload MemberLeftGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "MemberLeftGroup",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }
    
    public static LocalizationPayload MemberKickedFromGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "MemberKickedFromGroup",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }
    
    public static LocalizationPayload NewLeader(long accountId)
    {
        return LocalizationPayload.Create(
            "NewLeader",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }
    
    public static LocalizationPayload PlayerIsNotInGroup(string userName)
    {
        return LocalizationPayload.Create(
            "PlayerIsNotInGroup", 
            "GroupManager",
            LocalizationArg_Handle.Create(userName));
    }
    
    public static LocalizationPayload GroupDisbanded { get; }
        = LocalizationPayload.Create("GroupDisbanded", "Group");
    
    public static LocalizationPayload LeaderLoggedOff { get; }
        = LocalizationPayload.Create("LeaderLoggedOff", "Invite");

    public static LocalizationPayload NotTheLeader { get; }
        = LocalizationPayload.Create("NotTheLeader", "GroupManager"); // You are not the leader.

    public static LocalizationPayload NotInGroupMember { get; }
        = LocalizationPayload.Create("NotInGroupMember", "GroupManager"); // You are not a member of a group.
    
    public static LocalizationPayload AlreadyTheLeader { get; }
        = LocalizationPayload.Create("AlreadyTheLeader", "GroupManager");
    
    public static LocalizationPayload FailedToJoinGroupInviteExpired(long inviterAccountId)
    {
        return LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "GroupInviteExpired",
                    "Invite",
                    LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(inviterAccountId)))));
    }

    public static LocalizationPayload FailedToJoinGroupOtherPlayerInOtherGroup(long inviterAccountId)
    {
        return LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                OtherPlayerInOtherGroup(LobbyServerUtils.GetHandle(inviterAccountId))));
    }

    public static LocalizationPayload FailedToJoinGroupIsFull { get; }
        = LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "GroupIsFull",
                    "Invite")));

    public static LocalizationPayload FailedToJoinGroupCreatorOffline { get; }
        = LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "CreatorOffline",
                    "Invite")));

    public static LocalizationPayload FailedToJoinGroupCantJoinIfInGroup { get; }
        = LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "CantJoinIfInGroup",
                    "Invite")));

    public static LocalizationPayload FailedToJoinGroupCantInviteActiveOpponent { get; }
        = LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "CantInviteActiveOpponent",
                    "AddFollower")));

    public static LocalizationPayload FailedToJoinUnknownError { get; }
        = LocalizationPayload.Create(
            "FailedToJoinGroupError",
            "GroupInvite",
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "UnknownError",
                    "Global")));

    public static LocalizationPayload MemberFailedToJoinGroupIsFull(long accountId)
    {
        return MemberFailedToJoinGroupIsFull(LobbyServerUtils.GetHandle(accountId));
    }

    public static LocalizationPayload MemberFailedToJoinGroupIsFull(string handle)
    {
        return LocalizationPayload.Create(
            "MemberFailedToJoin",
            "Group",
            LocalizationArg_Handle.Create(handle),
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "GroupIsFull",
                    "Invite")));
    }

    public static LocalizationPayload MemberFailedToJoinUnknownError(long accountId)
    {
        return LocalizationPayload.Create(
            "MemberFailedToJoin",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)),
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "UnknownError",
                    "Global")));
    }

    public static LocalizationPayload MemberFailedToJoinGroupInviteExpired(long inviterAccountId)
    {
        string handle = LobbyServerUtils.GetHandle(inviterAccountId);
        return LocalizationPayload.Create(
            "MemberFailedToJoin",
            "Group",
            LocalizationArg_Handle.Create(handle),
            LocalizationArg_LocalizationPayload.Create(
                LocalizationPayload.Create(
                    "GroupInviteExpired",
                    "Invite",
                    LocalizationArg_Handle.Create(handle))));
    }

    public static LocalizationPayload MemberFailedToJoinGroupPlayerNotFound(long accountId)
    {
        string handle = LobbyServerUtils.GetHandle(accountId);
        return LocalizationPayload.Create(
            "MemberFailedToJoin",
            "Group",
            LocalizationArg_Handle.Create(handle),
            LocalizationArg_LocalizationPayload.Create(PlayerNotFound(handle)));
    }

    public static LocalizationPayload MemberFailedToJoinGroupOtherPlayerInOtherGroup(long accountId)
    {
        string handle = LobbyServerUtils.GetHandle(accountId);
        return LocalizationPayload.Create(
            "MemberFailedToJoin",
            "Group",
            LocalizationArg_Handle.Create(handle),
            LocalizationArg_LocalizationPayload.Create(OtherPlayerInOtherGroup(handle)));
    }

    public static LocalizationPayload OtherPlayerInOtherGroup(string handle)
    {
        return LocalizationPayload.Create(
            "OtherPlayerInOtherGroup",
            "Invite",
            LocalizationArg_Handle.Create(handle));
    }

    public static LocalizationPayload PlayerNotFound(string handle)
    {
        return LocalizationPayload.Create(
            "PlayerNotFound",
            "Invite",
            LocalizationArg_Handle.Create(handle));
    }

    public static LocalizationPayload RequestToJoinGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "RequestToJoinGroup",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload InvitedFriendToGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "InvitedFriendToGroup",
            "Global",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload InviteToGroupWithYou(long suggesterAccountId, long inviteeAccountId)
    {
        return LocalizationPayload.Create(
            "InviteToGroupWithYou",
            "Global",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(suggesterAccountId)),
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(inviteeAccountId)));
    }

    public static LocalizationPayload OtherPlayerNotInGroup(long otherPlayerAccountId)
    {
        return LocalizationPayload.Create(
            "OtherPlayerNotInGroup",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(otherPlayerAccountId)));
    }

    public static LocalizationPayload AlreadyInYourGroup(long accountId)
    {
        return LocalizationPayload.Create(
            "AlreadyInYourGroup",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload YouAreBlocking(long accountId)
    {
        return LocalizationPayload.Create(
            "YouAreBlocking",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload RejectedGroupInvite(long rejectorAccountId)
    {
        return LocalizationPayload.Create(
            "RejectedGroupInvite",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(rejectorAccountId)));
    }

    public static LocalizationPayload JoinGroupOfferExpired(long accountIdThatFailedToJoin)
    {
        return LocalizationPayload.Create(
            "JoinGroupOfferExpired",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountIdThatFailedToJoin)));
    }

    public static LocalizationPayload AlreadyRejectedInvite(long rejectorAccountId)
    {
        return LocalizationPayload.Create(
            "AlreadyRejectedInvite",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(rejectorAccountId)));
    }

    public static LocalizationPayload PlayerInACustomMatchAtTheMoment(long accountId)
    {
        return LocalizationPayload.Create(
            "PlayerInACustomMatchAtTheMoment",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload PlayerStillConsideringYourPreviousInviteRequest(long accountId)
    {
        return LocalizationPayload.Create(
            "PlayerStillConsideringYourPreviousInviteRequest",
            "Group",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)));
    }

    public static LocalizationPayload CantJoinIfInGroup { get; }
        = LocalizationPayload.Create("CantJoinIfInGroup", "Invite");

    public static LocalizationPayload CantInviteYourself { get; }
        = LocalizationPayload.Create("CantInviteYourself", "Invite");

    public static LocalizationPayload LeaderRejectedSuggestion { get; }
        = LocalizationPayload.Create("LeaderRejectedSuggestion", "Invite");
}