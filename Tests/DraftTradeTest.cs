using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;

namespace Tests;

public class DraftTradeTest
{
    private const long Multilancer = 1001;
    private const long Teammate = 1002;
    private const long Spectator = 1003;

    private sealed class TestGame : PvpGame
    {
        public TestGame() : base(null)
        {
            LobbyServerPlayerInfo main = Slot(1, Multilancer, Team.TeamA);
            LobbyServerPlayerInfo proxy = Slot(2, Multilancer, Team.TeamA);
            proxy.ControllingPlayerId = main.PlayerId;
            proxy.ControllingPlayerInfo = main;
            main.ProxyPlayerIds.Add(proxy.PlayerId);

            LobbyServerPlayerInfo bot = Slot(4, 0, Team.TeamA);
            bot.IsNPCBot = true;

            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo =
                [
                    main,
                    proxy,
                    Slot(3, Teammate, Team.TeamA),
                    bot,
                    Slot(5, Spectator, Team.Spectator),
                ]
            };
            CreateDraft();
        }

        private static LobbyServerPlayerInfo Slot(int playerId, long accountId, Team team)
        {
            return new LobbyServerPlayerInfo
            {
                PlayerId = playerId,
                AccountId = accountId,
                TeamId = team,
            };
        }
    }

    private readonly TestGame game = new();

    [Theory]
    [InlineData(Multilancer, 0, 1)]
    [InlineData(Multilancer, 1, 1)]
    [InlineData(Multilancer, 2, 2)]
    [InlineData(Teammate, 0, 3)]
    [InlineData(Teammate, 3, 3)]
    public void GetDraftTradingSlot_OwnSlot(long accountId, int requestedPlayerId, int expectedPlayerId)
    {
        Assert.Equal(expectedPlayerId, game.Draft.GetDraftTradingSlot(accountId, requestedPlayerId)?.PlayerId);
    }

    [Theory]
    [InlineData(Multilancer, 3)] // a teammate's slot
    [InlineData(Multilancer, 4)] // a bot
    [InlineData(Multilancer, 99)] // no such slot
    [InlineData(Teammate, 2)] // someone else's proxy
    [InlineData(Spectator, 0)] // not playing
    [InlineData(Spectator, 5)]
    public void GetDraftTradingSlot_NotControlled_Null(long accountId, int requestedPlayerId)
    {
        Assert.Null(game.Draft.GetDraftTradingSlot(accountId, requestedPlayerId));
    }
}
