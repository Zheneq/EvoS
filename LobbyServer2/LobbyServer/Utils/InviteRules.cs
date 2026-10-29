using System.Text.RegularExpressions;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Utils;

// Checks shared by group and custom game invites
public static class InviteRules
{
    private static readonly ILog log = LogManager.GetLogger(typeof(InviteRules));

    // Removes the (mentor icon) and (Dev) tag from a handle
    public static string CleanHandle(string handle)
    {
        return handle is null ? "" : Regex.Replace(handle, @"\p{C}|\(.*?\)", "");
    }

    // Returns the failure to report or null if the handle belongs to another player.
    public static LocalizationPayload ResolveInvitee(
        IClientConnection inviter,
        string inviteeHandle,
        string targetForLog,
        out long inviteeAccountId)
    {
        inviteeAccountId = string.IsNullOrWhiteSpace(inviteeHandle)
            ? 0
            : LobbyServerUtils.ResolveAccountId(inviteeHandle);
        if (inviteeAccountId == 0)
        {
            log.Info($"Failed to find player {inviteeHandle} to invite to {targetForLog}");
            return GroupMessages.PlayerNotFound(inviteeHandle);
        }

        if (inviteeAccountId == inviter.AccountId)
        {
            log.Info($"{inviter.Handle} attempted to invite themself to {targetForLog}");
            return GroupMessages.CantInviteYourself;
        }

        return null;
    }

    // Returns the failure to report or null if the invite can be sent to the invitee.
    // If the invitee blocked the inviter, the inviter must be told that the invite was sent (shadow ban).
    // Blocks are checked before the online status so that a blocked player can't tell if the other one is online.
    public static LocalizationPayload CheckInvitee(
        PersistedAccountData inviter,
        PersistedAccountData invitee,
        string inviteeHandle,
        string target,
        out bool shadowBanned,
        out LobbyServerProtocol inviteeConnection)
    {
        shadowBanned = false;
        inviteeConnection = null;

        if (inviter.SocialComponent?.IsBlocked(invitee.AccountId) == true)
        {
            log.Info($"{inviter.Handle} attempted to invite {inviteeHandle} whom they blocked to {target}");
            return GroupMessages.YouAreBlocking(invitee.AccountId);
        }

        if (invitee.SocialComponent?.IsBlocked(inviter.AccountId) == true)
        {
            log.Info($"{inviter.Handle} attempted to invite {inviteeHandle} who blocked them to {target}");
            shadowBanned = true;
            return null;
        }

        inviteeConnection = SessionManager.GetClientConnection(invitee.AccountId);
        if (inviteeConnection is null) // offline
        {
            log.Info($"{inviter.Handle} attempted to invite {inviteeHandle} who is offline to {target}");
            return GroupMessages.PlayerNotFound(inviteeHandle);
        }

        return null;
    }
}
