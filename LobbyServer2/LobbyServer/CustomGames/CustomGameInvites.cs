using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CentralServer.LobbyServer.CustomGames;

// Pending custom game invites. An invite stays valid until it is answered or its game is removed.
public class CustomGameInvites(TimeSpan reinviteCooldown)
{
    private readonly Dictionary<int, CustomGameInvite> _invites = new();
    private int _lastInviteId;
    private readonly Lock _lock = new();

    // Returns null if the player was invited to this game less than the cooldown ago.
    // Otherwise, the new invite replaces their previous invite to this game.
    public CustomGameInvite Create(long inviterAccountId, long inviteeAccountId, string processCode, int initialRequestId)
    {
        DateTime now = DateTime.UtcNow;
        lock (_lock)
        {
            CustomGameInvite previousInvite = _invites.Values
                .FirstOrDefault(i => i.ProcessCode == processCode && i.InviteeAccountId == inviteeAccountId);
            if (previousInvite is not null)
            {
                if (now - previousInvite.Created < reinviteCooldown)
                {
                    return null;
                }
                _invites.Remove(previousInvite.InviteId);
            }

            CustomGameInvite invite = new CustomGameInvite(
                ++_lastInviteId,
                inviterAccountId,
                inviteeAccountId,
                processCode,
                initialRequestId,
                now);
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
        lock (_lock)
        {
            List<int> inviteIds = _invites.Values
                .Where(i => i.ProcessCode == processCode)
                .Select(i => i.InviteId)
                .ToList();
            inviteIds.ForEach(id => _invites.Remove(id));
        }
    }
}
