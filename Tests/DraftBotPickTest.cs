using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace Tests;

public class DraftBotPickTest
{
    private const CharacterType Tank = CharacterType.Rampart;
    private const CharacterType Support = CharacterType.Archer;
    private const CharacterType Assassin = CharacterType.Blaster;
    private const CharacterType Assassin2 = CharacterType.Soldier;

    // TeamA slots 1 and 2 have picked, slots 3 and 4 are on deck in PICK_FREELANCER2
    private sealed class TestGame : PvpGame
    {
        public TestGame(CharacterType[] lockedPicks, CharacterType humanSlot3Hover) : base(null)
        {
            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo = Enumerable.Range(1, 8)
                    .Select(playerId => new LobbyServerPlayerInfo
                    {
                        PlayerId = playerId,
                        TeamId = playerId <= 4 ? Team.TeamA : Team.TeamB,
                        AccountId = playerId == 3 && humanSlot3Hover != CharacterType.None ? 1003 : 0,
                        IsNPCBot = playerId != 3 || humanSlot3Hover == CharacterType.None,
                    })
                    .ToList()
            };
            SetRankedResolutionPhaseData(new RankedResolutionPhaseData
            {
                UnselectedPlayerStates = TeamInfo.TeamPlayerInfo
                    .Select(p => new RankedResolutionPlayerState
                    {
                        PlayerId = p.PlayerId,
                        Intention = p.PlayerId == 3 ? humanSlot3Hover : CharacterType.None,
                    })
                    .ToList(),
                PlayersOnDeck = [],
                FriendlyBans = [],
                EnemyBans = [],
                FriendlyTeamSelections = lockedPicks
                    .Select((characterType, i) => (PlayerId: i + 1, characterType))
                    .ToDictionary(p => p.PlayerId, p => p.characterType),
                EnemyTeamSelections = [],
                TradeActions = [],
            });
            PhaseSubType = FreelancerResolutionPhaseSubType.PICK_FREELANCER2;
        }
    }

    private static List<CharacterType> PutSlots3And4OnDeck(TestGame game)
    {
        game.AddPlayersToDeckBasedOnPhase(
            FreelancerResolutionPhaseSubType.PICK_FREELANCER2,
            game.GetPlayerById(3),
            game.GetPlayerById(4));
        return game.GetRankedResolutionPhaseData().PlayersOnDeck.Select(p => p.Intention).ToList();
    }

    private static CharacterRole RoleOf(CharacterType characterType)
    {
        return CharacterConfigs.Characters[characterType].CharacterRole;
    }

    [Theory]
    [InlineData(new CharacterType[0], CharacterRole.Tank, CharacterRole.Support)]
    [InlineData(new[] { Tank }, CharacterRole.Support, CharacterRole.Assassin)]
    [InlineData(new[] { Support }, CharacterRole.Tank, CharacterRole.Assassin)]
    [InlineData(new[] { Assassin, Assassin2 }, CharacterRole.Tank, CharacterRole.Support)]
    [InlineData(new[] { Tank, Support }, CharacterRole.Assassin, CharacterRole.Assassin)]
    public void BotsOnDeckTogether_TakeDifferentMissingRoles(
        CharacterType[] lockedPicks,
        CharacterRole expectedSlot3Role,
        CharacterRole expectedSlot4Role)
    {
        List<CharacterType> hovers = PutSlots3And4OnDeck(new TestGame(lockedPicks, CharacterType.None));

        Assert.Equal(new[] { expectedSlot3Role, expectedSlot4Role }, hovers.Select(RoleOf));
        Assert.NotEqual(hovers[0], hovers[1]);
        Assert.DoesNotContain(hovers, h => lockedPicks.Contains(h));
    }

    [Fact]
    public void BotOnDeckWithHuman_IgnoresHumanHover()
    {
        List<CharacterType> hovers = PutSlots3And4OnDeck(new TestGame([Support], humanSlot3Hover: Tank));

        Assert.Equal(Tank, hovers[0]);
        Assert.Equal(CharacterRole.Tank, RoleOf(hovers[1]));
    }

    [Fact]
    public void PickCharacter_OnlyAvailable()
    {
        List<CharacterType> tanks = CharacterConfigs.Characters
            .Where(kv => kv.Value.AllowForBots && kv.Value.CharacterRole == CharacterRole.Tank)
            .Select(kv => kv.Key)
            .ToList();

        Assert.Equal(
            tanks[0],
            Game.PickCharacter(new HashSet<CharacterType>(), tanks.Skip(1).ToHashSet(), forBot: true));
    }

    [Fact]
    public void PickCharacter_RoleUsedUp_FallsBackToOtherRoles()
    {
        HashSet<CharacterType> tanks = CharacterConfigs.Characters
            .Where(kv => kv.Value.CharacterRole == CharacterRole.Tank)
            .Select(kv => kv.Key)
            .ToHashSet();

        CharacterType characterType = Game.PickCharacter(new HashSet<CharacterType>(), tanks, forBot: true);

        Assert.DoesNotContain(characterType, tanks);
        Assert.True(CharacterConfigs.Characters[characterType].AllowForBots);
        Assert.NotEqual(CharacterRole.None, RoleOf(characterType));
    }
}
