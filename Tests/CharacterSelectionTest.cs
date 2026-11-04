using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class CharacterSelectionTest(ITestOutputHelper output) : EvosTest(output)
{
    private sealed class TestGame : PvpGame
    {
        public readonly long AccountId = (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 16_000_000L;

        public TestGame(GameStatus status) : base(null)
        {
            GameInfo = new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig { GameType = GameType.PvP },
                GameServerProcessCode = $"test-{Guid.NewGuid()}",
                GameStatus = status,
            };
            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo =
                [
                    new LobbyServerPlayerInfo
                    {
                        AccountId = AccountId,
                        PlayerId = 1,
                        Handle = $"Test#{AccountId}",
                        TeamId = Team.TeamA,
                        CharacterInfo = new LobbyCharacterInfo { CharacterType = CharacterType.Cleric },
                    }
                ]
            };
        }

        public CharacterType Character => GetPlayerInfo(AccountId).CharacterType;

        public bool Select(CharacterType characterType, ReadyState? readyState = null)
        {
            return UpdateCharacterInfo(
                AccountId,
                new LobbyCharacterInfo { CharacterType = characterType },
                new LobbyPlayerInfoUpdate
                {
                    CharacterType = characterType,
                    ContextualReadyState = readyState is null
                        ? null
                        : new ContextualReadyState { ReadyState = readyState.Value, GameProcessCode = GameInfo.GameServerProcessCode },
                });
        }
    }

    [Theory]
    [InlineData(CharacterType.PendingWillFill)]
    [InlineData(CharacterType.TestFreelancer1)]
    [InlineData(CharacterType.TestFreelancer2)]
    [InlineData(CharacterType.PunchingDummy)]
    [InlineData(CharacterType.Gryd)]
    [InlineData(CharacterType.None)]
    [InlineData((CharacterType)999)]
    public void IsPlayable_Placeholder_False(CharacterType characterType)
    {
        Assert.False(CharacterRules.IsPlayable(characterType));
    }

    [Fact]
    public void IsPlayable_Freelancer_True()
    {
        Assert.True(CharacterRules.IsPlayable(CharacterType.Archer));
    }

    [Theory]
    [InlineData(CharacterType.PendingWillFill)]
    [InlineData(CharacterType.TestFreelancer1)]
    [InlineData(CharacterType.TestFreelancer2)]
    public void FreelancerSelecting_ReadyAsPlaceholder_Rejected(CharacterType characterType)
    {
        TestGame game = new(GameStatus.FreelancerSelecting);

        Assert.False(game.Select(characterType, ReadyState.Ready));
        Assert.Equal(CharacterType.Cleric, game.Character);
    }

    [Fact]
    public void FreelancerSelecting_ReadyAsFreelancer_Accepted()
    {
        TestGame game = new(GameStatus.FreelancerSelecting);

        Assert.True(game.Select(CharacterType.Archer, ReadyState.Ready));
        Assert.Equal(CharacterType.Archer, game.Character);
    }

    // A client that missed character select sends its stale selection while the game is being launched
    [Theory]
    [InlineData(GameStatus.LoadoutSelecting)]
    [InlineData(GameStatus.Launching)]
    [InlineData(GameStatus.Launched)]
    [InlineData(GameStatus.Started)]
    public void AfterFreelancerSelecting_CharacterChange_Rejected(GameStatus status)
    {
        TestGame game = new(status);

        Assert.False(game.Select(CharacterType.PendingWillFill));
        Assert.Equal(CharacterType.Cleric, game.Character);
    }
}
