using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CentralServer.LobbyServer.CustomGames;

// Pending custom game invites. The client shows an invite until it is answered, so an invite stays pending
// until then, unless its game is removed or the invitee disconnects or joins a game.
public class CustomGameInvites
{
    private readonly Dictionary<int, CustomGameInvite> _invites = new();
    private int _lastInviteId;
    private readonly Lock _lock = new();

    // Returns null if the player already has a pending invite to this game
    public CustomGameInvite Create(long inviterAccountId, long inviteeAccountId, string processCode, int initialRequestId)
    {
        lock (_lock)
        {
            if (_invites.Values.Any(i => i.ProcessCode == processCode && i.InviteeAccountId == inviteeAccountId))
            {
                return null;
            }

            CustomGameInvite invite = new CustomGameInvite(
                ++_lastInviteId,
                inviterAccountId,
                inviteeAccountId,
                processCode,
                initialRequestId);
            _invites.Add(invite.InviteId, invite);
            return invite;
        }
    }

    // Removes and returns the invite. Returns null if there is no such invite for this player.
    public CustomGameInvite Take(int inviteId, long inviteeAccountId)
    {
        lock (_lock)
        {
            if (!_invites.TryGetValue(inviteId, out CustomGameInvite invite)
                || invite.InviteeAccountId != inviteeAccountId)
            {
                return null;
            }
            _invites.Remove(inviteId);
            return invite;
        }
    }

    public void RemoveForGame(string processCode)
    {
        RemoveWhere(i => i.ProcessCode == processCode);
    }

    // A restarted client has no invite popups, and loading a match closes them
    public void RemoveForInvitee(long inviteeAccountId)
    {
        RemoveWhere(i => i.InviteeAccountId == inviteeAccountId);
    }

    private void RemoveWhere(Func<CustomGameInvite, bool> predicate)
    {
        lock (_lock)
        {
            List<int> inviteIds = _invites.Values
                .Where(predicate)
                .Select(i => i.InviteId)
                .ToList();
            inviteIds.ForEach(id => _invites.Remove(id));
        }
    }
}
