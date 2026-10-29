using CentralServer.LobbyServer.CustomGames;

namespace Tests;

/// <summary>
/// Tests for <see cref="CustomGameInvites"/>, the store of pending custom game invites.
/// </summary>
public class CustomGameInvitesTest
{
    private const long Inviter = 1;
    private const long Invitee = 2;
    private const string Game = "CustomGame-1";

    private static CustomGameInvites WithCooldown() => new(TimeSpan.FromHours(1));

    private static CustomGameInvites WithoutCooldown() => new(TimeSpan.Zero);

    [Fact]
    public void Create_ThenTake_ReturnsInviteOnce()
    {
        CustomGameInvites invites = WithCooldown();

        CustomGameInvite invite = invites.Create(Inviter, Invitee, Game, 7);

        Assert.NotNull(invite);
        Assert.NotEqual(0, invite.InviteId); // 0 is an invalid request id
        Assert.Equal(Inviter, invite.InviterAccountId);
        Assert.Equal(Invitee, invite.InviteeAccountId);
        Assert.Equal(Game, invite.ProcessCode);
        Assert.Equal(7, invite.InitialRequestId);
        Assert.Same(invite, invites.Take(invite.InviteId, Invitee));
        Assert.Null(invites.Take(invite.InviteId, Invitee));
    }

    [Fact]
    public void Take_ByAnotherPlayer_KeepsInvite()
    {
        CustomGameInvites invites = WithCooldown();
        CustomGameInvite invite = invites.Create(Inviter, Invitee, Game, 7);

        Assert.Null(invites.Take(invite.InviteId, 3));
        Assert.Same(invite, invites.Take(invite.InviteId, Invitee));
    }

    [Fact]
    public void Take_UnknownInvite_ReturnsNull()
    {
        CustomGameInvites invites = WithCooldown();

        Assert.Null(invites.Take(12345, Invitee));
    }

    [Fact]
    public void Create_SamePlayerAndGameWithinCooldown_ReturnsNull()
    {
        CustomGameInvites invites = WithCooldown();
        CustomGameInvite invite = invites.Create(Inviter, Invitee, Game, 7);

        // from another player in the game too
        Assert.Null(invites.Create(3, Invitee, Game, 8));
        Assert.Same(invite, invites.Take(invite.InviteId, Invitee));
    }

    [Fact]
    public void Create_AfterCooldown_ReplacesInvite()
    {
        CustomGameInvites invites = WithoutCooldown();
        CustomGameInvite oldInvite = invites.Create(Inviter, Invitee, Game, 7);

        CustomGameInvite newInvite = invites.Create(Inviter, Invitee, Game, 8);

        Assert.NotNull(newInvite);
        Assert.NotEqual(oldInvite.InviteId, newInvite.InviteId);
        Assert.Null(invites.Take(oldInvite.InviteId, Invitee));
        Assert.Same(newInvite, invites.Take(newInvite.InviteId, Invitee));
    }

    [Fact]
    public void Create_OtherPlayerOrGame_IsIndependent()
    {
        CustomGameInvites invites = WithCooldown();
        CustomGameInvite invite = invites.Create(Inviter, Invitee, Game, 7);

        CustomGameInvite otherPlayerInvite = invites.Create(Inviter, 3, Game, 8);
        CustomGameInvite otherGameInvite = invites.Create(Inviter, Invitee, "CustomGame-2", 9);

        Assert.NotNull(otherPlayerInvite);
        Assert.NotNull(otherGameInvite);
        Assert.Equal(3, new[] { invite.InviteId, otherPlayerInvite.InviteId, otherGameInvite.InviteId }.Distinct().Count());
        Assert.Same(invite, invites.Take(invite.InviteId, Invitee));
    }

    [Fact]
    public void RemoveForGame_RemovesOnlyThatGame()
    {
        CustomGameInvites invites = WithCooldown();
        CustomGameInvite invite = invites.Create(Inviter, Invitee, Game, 7);
        CustomGameInvite otherGameInvite = invites.Create(Inviter, Invitee, "CustomGame-2", 8);

        invites.RemoveForGame(Game);

        Assert.Null(invites.Take(invite.InviteId, Invitee));
        Assert.Same(otherGameInvite, invites.Take(otherGameInvite.InviteId, Invitee));
        // the player can be invited to the game again
        Assert.NotNull(invites.Create(Inviter, Invitee, Game, 9));
    }
}
