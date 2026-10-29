using CentralServer.BridgeServer;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Group;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for joining custom games through <see cref="CustomGameManager"/>.
/// Joined to [Collection("ClientNotifierSeam")] because it replaces the process-global client notifier
/// and uses process-global GroupManager state.
/// </summary>
[Collection("ClientNotifierSeam")]
public class CustomGameManagerTest(ITestOutputHelper output) : EvosTest(output)
{
    private static long UniqueId() => (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 10_000_000L;

    private static long MakeAccount()
    {
        long accountId = UniqueId();
        DB.Get().AccountDao.CreateAccount(new PersistedAccountData
        {
            AccountId = accountId,
            Handle = $"Test#{accountId}",
            AccountComponent = new AccountComponent
            {
                LastCharacter = CharacterType.Archer,
            },
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>
            {
                { CharacterType.Archer, new PersistedCharacterData(CharacterType.Archer) }
            },
        });
        return accountId;
    }

    // A 1v1 game with one spectator slot. The owner takes a player slot.
    private static Game CreateGame(long owner)
    {
        GroupManager.CreateGroup(owner);
        Game game = CustomGameManager.CreateGame(
            owner,
            new LobbyGameConfig
            {
                TeamAPlayers = 1,
                TeamBPlayers = 1,
                Spectators = 1,
                SubTypes = [new GameSubType()],
            },
            out LocalizationPayload error);
        Assert.True(game is not null, error?.ToString());
        return game;
    }

    private static Team TeamOf(Game game, long accountId) => game.TeamInfo.TeamPlayerInfo.Single(p => p.AccountId == accountId).TeamId;

    [Fact]
    public void JoinGameAsInvitee_TakesPlayerSlotThenSpectatorSlot()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long player = MakeAccount();
        long spectator = MakeAccount();
        long extra = MakeAccount();
        Game game = CreateGame(owner);
        try
        {
            Assert.Same(game, CustomGameManager.JoinGameAsInvitee(player, game.ProcessCode, out LocalizationPayload failure));
            Assert.Null(failure);
            Assert.Equal(Team.TeamB, TeamOf(game, player));

            Assert.Same(game, CustomGameManager.JoinGameAsInvitee(spectator, game.ProcessCode, out failure));
            Assert.Null(failure);
            Assert.Equal(Team.Spectator, TeamOf(game, spectator));

            Assert.Null(CustomGameManager.JoinGameAsInvitee(extra, game.ProcessCode, out failure));
            Assert.Equal("GameCreatorNoLongerHasAGameForYou", failure.Term);
            Assert.Equal("Invite", failure.Context);
            Assert.DoesNotContain(game.TeamInfo.TeamPlayerInfo, p => p.AccountId == extra);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void JoinGame_FullTeams_DoesNotFallBackToSpectator()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long player = MakeAccount();
        long extra = MakeAccount();
        Game game = CreateGame(owner);
        try
        {
            Assert.NotNull(CustomGameManager.JoinGame(player, game.ProcessCode, false, out _));

            Assert.Null(CustomGameManager.JoinGame(extra, game.ProcessCode, false, out LocalizationPayload failure));
            Assert.Equal("GameCreatorNoLongerHasAGameForYou", failure.Term);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-such-game-xyz")]
    public void JoinGameAsInvitee_UnknownGame_NoLongerAvailable(string processCode)
    {
        Assert.Null(CustomGameManager.JoinGameAsInvitee(UniqueId(), processCode, out LocalizationPayload failure));
        Assert.Equal("GameCreatorNoLongerHasAGameForYou", failure.Term);
        Assert.Equal("Invite", failure.Context);
    }

    [Fact]
    public void DeleteGame_RemovesItsInvites()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long invitee = UniqueId();
        Game game = CreateGame(owner);
        CustomGameInvite invite;
        try
        {
            invite = CustomGameManager.Invites.Create(owner, invitee, game.ProcessCode, 1);
            Assert.NotNull(invite);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }

        Assert.Null(CustomGameManager.Invites.Take(invite.InviteId, invitee));
    }
}
