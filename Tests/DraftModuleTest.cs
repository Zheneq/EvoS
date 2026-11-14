using CentralServer.BridgeServer;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using LobbyGameClientMessages;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

// How DraftModule maps draft requests onto DraftController and its results onto responses.
// The request logic itself is characterized on the controller.
public class DraftModuleTest(ITestOutputHelper output) : EvosTest(output)
{
    private const int RequestId = 42;
    private const CharacterType Mine = CharacterType.Archer;
    private const CharacterType Taken = CharacterType.Rampart;
    private const CharacterType Free = CharacterType.Blaster;

    // TeamA: slot 1 is the player's and on deck, slot 2 is a bot that has picked Taken
    private sealed class TestGame : PvpGame
    {
        public readonly long AccountId = (Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 17_000_000L;

        public TestGame(
            FreelancerResolutionPhaseSubType phase,
            GameStatus status = GameStatus.FreelancerSelecting,
            bool playerHasPicked = false)
            : base(null)
        {
            GameSubType = new GameSubType { Mods = [GameSubType.SubTypeMods.RankedFreelancerSelection] };
            GameInfo = new LobbyGameInfo { GameStatus = status };
            TeamInfo = new LobbyServerTeamInfo
            {
                TeamPlayerInfo =
                [
                    new LobbyServerPlayerInfo { PlayerId = 1, AccountId = AccountId, TeamId = Team.TeamA },
                    new LobbyServerPlayerInfo { PlayerId = 2, TeamId = Team.TeamA, IsNPCBot = true },
                ]
            };

            Dictionary<int, CharacterType> picks = new() { [2] = Taken };
            if (playerHasPicked)
            {
                picks[1] = Mine;
            }

            DraftController draft = CreateDraft();
            draft.SetRankedResolutionPhaseData(new RankedResolutionPhaseData
            {
                UnselectedPlayerStates = TeamInfo.TeamPlayerInfo
                    .Select(p => new RankedResolutionPlayerState { PlayerId = p.PlayerId })
                    .ToList(),
                PlayersOnDeck =
                [
                    new RankedResolutionPlayerState
                    {
                        PlayerId = 1,
                        OnDeckness = RankedResolutionPlayerState.ReadyState.Selected,
                    }
                ],
                FriendlyBans = [],
                EnemyBans = [],
                FriendlyTeamSelections = picks,
                EnemyTeamSelections = [],
                TradeActions = [],
            });
            draft.PhaseSubType = phase;
        }

        public RankedResolutionPhaseData Data => Draft.GetRankedResolutionPhaseData();
    }

    private sealed class CapturingRegistry : IHandlerRegistry
    {
        private readonly Dictionary<Type, Action<WebSocketMessage>> _handlers = new();

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            _handlers[typeof(T)] = msg => handler((T)msg);
        }

        public void Dispatch<T>(T message) where T : WebSocketMessage
        {
            _handlers[typeof(T)](message);
        }
    }

    private static (RecordingClientConnection conn, CapturingRegistry registry) Connect(TestGame? game)
    {
        var conn = new RecordingClientConnection { AccountId = game?.AccountId ?? 1, CurrentGame = game! };
        var registry = new CapturingRegistry();
        new DraftModule(conn).Register(registry);
        return (conn, registry);
    }

    private static T SingleResponse<T>(RecordingClientConnection conn) where T : WebSocketResponseMessage
    {
        T response = Assert.IsType<T>(Assert.Single(conn.Sent));
        Assert.Equal(RequestId, response.ResponseId);
        return response;
    }

    private static RankedTradeRequest TradeFor(CharacterType desired)
    {
        RankedTradeRequest request = new RankedTradeRequest { RequestId = RequestId };
        request.Trade.DesiredCharacter = desired;
        request.Trade.TradeAction = RankedTradeData.TradeActionType.AcceptOrOffer;
        return request;
    }

    // No game, or a draft game past the draft
    private static TestGame? NotDraftingGame(bool hasGame)
    {
        return hasGame
            ? new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1, GameStatus.LoadoutSelecting)
            : null;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotDrafting_Hover_Fails(bool hasGame)
    {
        var (conn, registry) = Connect(NotDraftingGame(hasGame));
        registry.Dispatch(new RankedHoverClickRequest { RequestId = RequestId, Selection = Free });
        Assert.False(SingleResponse<RankedHoverClickResponse>(conn).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotDrafting_Ban_Fails(bool hasGame)
    {
        var (conn, registry) = Connect(NotDraftingGame(hasGame));
        registry.Dispatch(new RankedBanRequest { RequestId = RequestId, Selection = Free });
        Assert.False(SingleResponse<RankedBanResponse>(conn).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotDrafting_Select_FailsCannotReady(bool hasGame)
    {
        var (conn, registry) = Connect(NotDraftingGame(hasGame));
        registry.Dispatch(new RankedSelectionRequest { RequestId = RequestId, Selection = Free });
        RankedSelectionResponse response = SingleResponse<RankedSelectionResponse>(conn);
        Assert.False(response.Success);
        Assert.Equal("CannotReady", response.LocalizedFailure?.Term);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotDrafting_Trade_Fails(bool hasGame)
    {
        var (conn, registry) = Connect(NotDraftingGame(hasGame));
        registry.Dispatch(TradeFor(Taken));
        Assert.False(SingleResponse<RankedTradeResponse>(conn).Success);
    }

    [Fact]
    public void Hover_OnDeck_Succeeds()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedHoverClickRequest { RequestId = RequestId, Selection = Free });

        Assert.True(SingleResponse<RankedHoverClickResponse>(conn).Success);
        Assert.Equal(Free, Assert.Single(game.Data.PlayersOnDeck).Intention);
    }

    [Fact]
    public void Ban_InBanPhase_Succeeds()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_BANS1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedBanRequest { RequestId = RequestId, Selection = Free });

        Assert.True(SingleResponse<RankedBanResponse>(conn).Success);
        Assert.Equal([Free], game.Data.FriendlyBans);
    }

    [Fact]
    public void Ban_TakenCharacter_FailsWithBanResponse()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_BANS1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedBanRequest { RequestId = RequestId, Selection = Taken });

        Assert.False(SingleResponse<RankedBanResponse>(conn).Success);
        Assert.Empty(game.Data.FriendlyBans);
    }

    [Fact]
    public void Ban_InPickPhase_Fails()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedBanRequest { RequestId = RequestId, Selection = Free });

        Assert.False(SingleResponse<RankedBanResponse>(conn).Success);
        Assert.Empty(game.Data.FriendlyBans);
    }

    [Fact]
    public void Select_OnDeck_Succeeds()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedSelectionRequest { RequestId = RequestId, Selection = Free });

        RankedSelectionResponse response = SingleResponse<RankedSelectionResponse>(conn);
        Assert.True(response.Success);
        Assert.Null(response.LocalizedFailure);
        Assert.Equal(Free, game.Data.FriendlyTeamSelections[1]);
    }

    [Fact]
    public void Select_InBanPhase_FailsCannotReady()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_BANS1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedSelectionRequest { RequestId = RequestId, Selection = Free });

        RankedSelectionResponse response = SingleResponse<RankedSelectionResponse>(conn);
        Assert.False(response.Success);
        Assert.Equal("CannotReady", response.LocalizedFailure?.Term);
    }

    [Fact]
    public void Select_TakenCharacter_FailsNotAllowed()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedSelectionRequest { RequestId = RequestId, Selection = Taken });

        RankedSelectionResponse response = SingleResponse<RankedSelectionResponse>(conn);
        Assert.False(response.Success);
        Assert.Equal("CharacterTypeNotAllowed", response.LocalizedFailure?.Term);
    }

    [Fact]
    public void Select_AlreadyPicked_FailsCannotChange()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1, playerHasPicked: true);
        var (conn, registry) = Connect(game);

        registry.Dispatch(new RankedSelectionRequest { RequestId = RequestId, Selection = Free });

        RankedSelectionResponse response = SingleResponse<RankedSelectionResponse>(conn);
        Assert.False(response.Success);
        Assert.Equal("CannotChangeCharactersOnceReadied", response.LocalizedFailure?.Term);
        Assert.Equal(Mine, game.Data.FriendlyTeamSelections[1]);
    }

    [Fact]
    public void Trade_WithBot_Succeeds()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.FREELANCER_TRADE, playerHasPicked: true);
        var (conn, registry) = Connect(game);

        registry.Dispatch(TradeFor(Taken));

        Assert.True(SingleResponse<RankedTradeResponse>(conn).Success);
        Assert.Equal(Taken, game.Data.FriendlyTeamSelections[1]);
        Assert.Equal(Mine, game.Data.FriendlyTeamSelections[2]);
    }

    [Fact]
    public void Trade_InPickPhase_Fails()
    {
        TestGame game = new TestGame(FreelancerResolutionPhaseSubType.PICK_FREELANCER1, playerHasPicked: true);
        var (conn, registry) = Connect(game);

        registry.Dispatch(TradeFor(Taken));

        Assert.False(SingleResponse<RankedTradeResponse>(conn).Success);
        Assert.Equal(Mine, game.Data.FriendlyTeamSelections[1]);
    }
}
