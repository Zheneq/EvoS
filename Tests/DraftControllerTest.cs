using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Microsoft.Extensions.Time.Testing;
using Tests.Lib;
using Xunit.Abstractions;
using static EvoS.Framework.Constants.Enums.FreelancerResolutionPhaseSubType;

namespace Tests;

// DraftController run over a fake host and fake time.
// Tests marked "Current behavior" pin down what the draft does today, not necessarily what it should do.
public class DraftControllerTest : EvosTest
{
    private const long Bot = 0;
    private const long Human = 1001;
    private const long Human2 = 1002;
    private const long Stranger = 9999;
    private const CharacterType Tank = CharacterType.Rampart;
    private const CharacterType Support = CharacterType.Archer;
    private static readonly TimeSpan BotSubPhase = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EndPause = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    private readonly FakeDraftHost host = new();
    private readonly DraftTime time = new();
    private readonly DraftController draft;

    public DraftControllerTest(ITestOutputHelper output) : base(output)
    {
        draft = new DraftController(host, time);
    }

    // Fake time that counts the waits the draft starts: one per sub-phase, then one for the pause at the end.
    // When the count goes up, the draft has set up the sub-phase and is waiting.
    private sealed class DraftTime : FakeTimeProvider
    {
        private int _waits;

        public int Waits => Volatile.Read(ref _waits);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = base.CreateTimer(callback, state, dueTime, period);
            Interlocked.Increment(ref _waits);
            return timer;
        }
    }

    // Fills slots 1–4 (TeamA) and 5–8 (TeamB) with the given accounts, Bot being an NPC bot.
    // Sub-phases with bans: slot 1 bans, slot 5 bans, slot 1 picks, slots 5 and 6 pick, slot 2 picks, slot 6 bans,
    // slot 2 bans, slot 7 picks, slots 3 and 4 pick, slot 8 picks, trade.
    // Without bans: slot 1 picks, slots 5 and 6 pick, slot 2 picks, slot 7 picks, slots 3 and 4 pick, slot 8 picks, trade.
    private static void AddSlots(FakeDraftHost host, params long[] accountIds)
    {
        host.Players.AddRange(accountIds.Select((accountId, i) => new LobbyServerPlayerInfo
        {
            PlayerId = i + 1,
            TeamId = i < 4 ? Team.TeamA : Team.TeamB,
            AccountId = accountId,
            IsNPCBot = accountId == Bot,
            Handle = accountId == Bot ? $"Bot {i + 1}" : $"Player#{accountId}",
        }));
    }

    private Task Start(params long[] accountIds)
    {
        AddSlots(host, accountIds);
        return draft.RunAsync();
    }

    private Task StartWithHumanInSlot1() => Start(Human, Bot, Bot, Bot, Bot, Bot, Bot, Bot);

    private RankedResolutionPhaseData Data => draft.GetStateForTest();

    private List<int> Deck() => Data.PlayersOnDeck.Select(p => p.PlayerId).ToList();

    private RankedResolutionPlayerState StateOf(int playerId) => Data.UnselectedPlayerStates.Single(p => p.PlayerId == playerId);

    private static void WaitFor(Func<bool> condition)
    {
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
    }

    // Waits until the draft is in its nth sub-phase, counting from 1.
    // The draft moves on asynchronously after a lock-in or a cancellation.
    private void WaitForSubPhase(int number) => WaitFor(() => time.Waits == number);

    // Lets bots on deck time out until the draft is in its nth sub-phase
    private void SkipBotSubPhasesTo(int number)
    {
        while (time.Waits < number)
        {
            int waits = time.Waits;
            time.Advance(BotSubPhase);
            WaitFor(() => time.Waits > waits);
        }

        Assert.Equal(number, time.Waits);
    }

    private static CharacterRole RoleOf(CharacterType characterType) => CharacterConfigs.Characters[characterType].CharacterRole;

    private static IEnumerable<CharacterType> Playable() => CharacterConfigs.Characters
        .Where(kv => kv.Value.AllowForPlayers && kv.Value.CharacterRole != CharacterRole.None)
        .Select(kv => kv.Key);

    // A character nobody has banned or picked yet. Bots pick randomly, so a fixed one may be taken already.
    private CharacterType Unused()
    {
        HashSet<CharacterType> used = Data.FriendlyBans
            .Concat(Data.EnemyBans)
            .Concat(Data.FriendlyTeamSelections.Values)
            .Concat(Data.EnemyTeamSelections.Values)
            .ToHashSet();
        return Playable().First(characterType => !used.Contains(characterType));
    }

    private static RankedTradeData Offer(CharacterType desired, int offeringPlayerId = 0) => new()
    {
        DesiredCharacter = desired,
        TradeAction = RankedTradeData.TradeActionType.AcceptOrOffer,
        OfferingPlayerId = offeringPlayerId,
    };

    private static RankedTradeData Reject(CharacterType offered) => new()
    {
        DesiredCharacter = offered,
        TradeAction = RankedTradeData.TradeActionType.Reject,
    };

    // ---- Sub-phase timing ----

    [Fact]
    public void Start_SendsSnapshotWithFullTimeout()
    {
        _ = StartWithHumanInSlot1();

        Assert.Equal(1, time.Waits);
        Assert.Equal(PICK_BANS1, draft.PhaseSubType);
        Assert.Equal([1], Deck());
        var (phase, data) = Assert.Single(host.Snapshots);
        Assert.Equal(PICK_BANS1, phase);
        Assert.Equal(host.Timeouts.Ban1, data.TimeLeftInSubPhase);
        Assert.Equal(1, host.GameInfoNotifications);
    }

    [Fact]
    public void Snapshot_TimeLeft_CountsDown()
    {
        _ = StartWithHumanInSlot1();

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.True(draft.Hover(Human, Tank).Success);

        Assert.Equal(host.Timeouts.Ban1 - TimeSpan.FromSeconds(10), host.Snapshots[^1].Data.TimeLeftInSubPhase);
    }

    [Fact]
    public void HumanSubPhase_LastsItsTimeout_ThenBansRandomly()
    {
        _ = StartWithHumanInSlot1();

        time.Advance(host.Timeouts.Ban1 - Tick);
        Assert.Equal(1, time.Waits);

        time.Advance(Tick);
        WaitForSubPhase(2);
        Assert.Equal([5], Deck());
        Assert.Single(Data.FriendlyBans);
    }

    [Fact]
    public void LockIn_EndsSubPhaseWithoutWaiting()
    {
        _ = StartWithHumanInSlot1();

        Assert.True(draft.Ban(Human, Tank).Success);

        WaitForSubPhase(2);
        Assert.Equal([5], Deck());
        Assert.Equal([Tank], Data.FriendlyBans);
    }

    [Fact]
    public void BotSubPhase_EndsAfterFiveSeconds()
    {
        _ = StartWithHumanInSlot1();
        draft.Ban(Human, Tank);
        WaitForSubPhase(2);

        time.Advance(BotSubPhase - Tick);
        Assert.Equal(2, time.Waits);

        time.Advance(Tick);
        WaitForSubPhase(3);
        Assert.Equal([1], Deck());
        Assert.Equal(PICK_FREELANCER1, draft.PhaseSubType);
        Assert.Single(Data.EnemyBans);
    }

    [Fact]
    public async Task MatchCancelled_EndsRunWithoutWaiting()
    {
        Task run = StartWithHumanInSlot1();

        host.CancelMatch();

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(host.Snapshots);
        Assert.Empty(Data.FriendlyBans);
    }

    // ---- Hover ----

    [Fact]
    public void Hover_OnDeck_UpdatesDeckAndIntention()
    {
        _ = StartWithHumanInSlot1();

        Assert.True(draft.Hover(Human, Tank).Success);

        Assert.Equal(Tank, Assert.Single(Data.PlayersOnDeck).Intention);
        Assert.Equal(Tank, StateOf(1).Intention);
        Assert.Equal(Tank, Assert.Single(host.Snapshots[^1].Data.PlayersOnDeck).Intention);
    }

    [Fact]
    public void Hover_NotOnDeck_UpdatesIntentionOnly()
    {
        _ = Start(Human, Human2, Bot, Bot, Bot, Bot, Bot, Bot);

        Assert.True(draft.Hover(Human2, Tank).Success);

        Assert.Equal(CharacterType.None, Assert.Single(Data.PlayersOnDeck).Intention);
        Assert.Equal(Tank, StateOf(2).Intention);
    }

    [Fact]
    public void Hover_AccountNotInRoster_Fails()
    {
        _ = StartWithHumanInSlot1();

        Assert.False(draft.Hover(Stranger, Tank).Success);
    }

    // Hovers keep coming while the draft moves from sub-phase to sub-phase
    [Fact]
    public async Task Hover_WhileDraftMovesOn_KeepsStateConsistent()
    {
        host.HasBans = false;
        Task run = StartWithHumanInSlot1();
        // Slot 1 has hovered by the time its sub-phase ends, whenever the other thread gets going
        draft.Hover(Human, Tank);
        using CancellationTokenSource stop = new();
        Task hovering = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                draft.Hover(Human, Tank);
                Thread.Yield();
            }
        });

        // Slot 1 times out with Tank hovered, the bots pick, the trade sub-phase times out
        time.Advance(host.Timeouts.Pick);
        WaitForSubPhase(2);
        SkipBotSubPhasesTo(7);
        time.Advance(host.Timeouts.Trade);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();
        await hovering.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Tank, host.GetPlayerById(1)!.CharacterType);
        Assert.Equal(8, host.Players.Select(p => p.CharacterType).Distinct().Count());
    }

    // ---- Ban ----

    [Fact]
    public void Ban_OnDeck_BansAndClearsHover()
    {
        _ = StartWithHumanInSlot1();
        draft.Hover(Human, Tank);

        Assert.True(draft.Ban(Human, Tank).Success);

        WaitForSubPhase(2);
        Assert.Equal([Tank], Data.FriendlyBans);
        // Start, hover, ban, start of the next sub-phase
        Assert.Equal(4, host.Snapshots.Count);
        RankedResolutionPlayerState onDeck = Assert.Single(host.Snapshots[2].Data.PlayersOnDeck);
        Assert.Equal(CharacterType.None, onDeck.Intention);
        Assert.Equal(RankedResolutionPlayerState.ReadyState.Unselected, onDeck.OnDeckness);
    }

    [Fact]
    public void Ban_InPickPhase_Fails()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();

        Assert.False(draft.Ban(Human, Tank).Success);

        Assert.Empty(Data.FriendlyBans);
        Assert.Equal(1, time.Waits);
    }

    [Fact]
    public void Ban_NotOnDeck_Fails()
    {
        _ = Start(Human, Human2, Bot, Bot, Bot, Bot, Bot, Bot);

        Assert.False(draft.Ban(Human2, Tank).Success);

        Assert.Empty(Data.FriendlyBans);
    }

    [Fact]
    public void Ban_AlreadyBannedCharacter_Fails()
    {
        _ = Start(Human, Bot, Bot, Bot, Human2, Bot, Bot, Bot);
        draft.Ban(Human, Tank);
        WaitForSubPhase(2);

        Assert.False(draft.Ban(Human2, Tank).Success);

        Assert.Empty(Data.EnemyBans);
    }

    [Theory]
    [InlineData(CharacterType.PendingWillFill, CharacterRole.Assassin)]
    [InlineData(CharacterType.TestFreelancer1, CharacterRole.Tank)]
    [InlineData(CharacterType.TestFreelancer2, CharacterRole.Support)]
    public void Ban_FillRole_BansCharacterOfThatRole(CharacterType fill, CharacterRole role)
    {
        _ = StartWithHumanInSlot1();

        Assert.True(draft.Ban(Human, fill).Success);

        WaitForSubPhase(2);
        CharacterType banned = Assert.Single(Data.FriendlyBans);
        Assert.Equal(role, RoleOf(banned));
        // Shown as a hover first, for the lock in animation
        Assert.Equal(banned, Assert.Single(host.Snapshots[1].Data.PlayersOnDeck).Intention);
    }

    [Fact]
    public void Ban_AccountNotInRoster_Fails()
    {
        _ = StartWithHumanInSlot1();

        Assert.False(draft.Ban(Stranger, Tank).Success);
    }

    // Holds the draft after its first sub-phase has timed out and been resolved, before the next one starts.
    // The timer goes off on another thread, which the draft then runs on.
    private Task TimeOutFirstSubPhaseAndHold(TimeSpan timeout, ManualResetEventSlim held, ManualResetEventSlim resume)
    {
        host.BeforeConnectivityCheck = () =>
        {
            host.BeforeConnectivityCheck = null;
            held.Set();
            resume.Wait();
        };
        Task timingOut = Task.Run(() => time.Advance(timeout));
        Assert.True(held.Wait(TimeSpan.FromSeconds(5)));
        return timingOut;
    }

    // Before the draft had a lock, such a ban was added on top of the one the draft had made
    [Fact]
    public async Task Ban_AfterSubPhaseEnded_Rejected()
    {
        _ = StartWithHumanInSlot1();
        using ManualResetEventSlim held = new();
        using ManualResetEventSlim resume = new();
        Task timingOut = TimeOutFirstSubPhaseAndHold(host.Timeouts.Ban1, held, resume);
        CharacterType drafted = Assert.Single(Data.FriendlyBans);

        Assert.False(draft.Ban(Human, Unused()).Success);

        resume.Set();
        await timingOut.WaitAsync(TimeSpan.FromSeconds(5));
        WaitForSubPhase(2);
        Assert.Equal([drafted], Data.FriendlyBans);
    }

    // ---- Select ----

    [Fact]
    public void Select_OnDeck_LocksInAndEndsSubPhase()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();

        DraftRequestResult result = draft.Select(Human, Tank);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        WaitForSubPhase(2);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
    }

    [Fact]
    public void Select_InBanPhase_FailsCannotReady()
    {
        _ = StartWithHumanInSlot1();

        DraftRequestResult result = draft.Select(Human, Tank);

        Assert.False(result.Success);
        Assert.Equal("CannotReady", result.Failure?.Term);
        Assert.Empty(Data.FriendlyTeamSelections);
    }

    [Fact]
    public void Select_Twice_FailsCannotChange()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();
        draft.Select(Human, Tank);
        WaitForSubPhase(2);

        DraftRequestResult result = draft.Select(Human, Support);

        Assert.False(result.Success);
        Assert.Equal("CannotChangeCharactersOnceReadied", result.Failure?.Term);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
    }

    [Fact]
    public void Select_TakenCharacter_FailsNotAllowed()
    {
        host.HasBans = false;
        _ = Start(Human, Bot, Bot, Bot, Human2, Bot, Bot, Bot);
        draft.Select(Human, Tank);
        WaitForSubPhase(2);

        DraftRequestResult result = draft.Select(Human2, Tank);

        Assert.False(result.Success);
        Assert.Equal("CharacterTypeNotAllowed", result.Failure?.Term);
        Assert.False(Data.EnemyTeamSelections.ContainsKey(5));
    }

    // Current behavior: a lock-in that fails still marks the player's intention as selected
    [Fact]
    public void Select_NotOnDeck_FailsButMarksIntention()
    {
        host.HasBans = false;
        _ = Start(Human, Human2, Bot, Bot, Bot, Bot, Bot, Bot);

        DraftRequestResult result = draft.Select(Human2, Tank);

        Assert.False(result.Success);
        Assert.Equal("CharacterTypeNotAllowed", result.Failure?.Term);
        Assert.False(Data.FriendlyTeamSelections.ContainsKey(2));
        Assert.Equal(Tank, StateOf(2).Intention);
        Assert.Equal(RankedResolutionPlayerState.ReadyState.Selected, StateOf(2).OnDeckness);
    }

    [Theory]
    [InlineData(CharacterType.PendingWillFill, CharacterRole.Assassin)]
    [InlineData(CharacterType.TestFreelancer1, CharacterRole.Tank)]
    [InlineData(CharacterType.TestFreelancer2, CharacterRole.Support)]
    public void Select_FillRole_PicksCharacterOfThatRole(CharacterType fill, CharacterRole role)
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();

        Assert.True(draft.Select(Human, fill).Success);

        WaitForSubPhase(2);
        CharacterType picked = Data.FriendlyTeamSelections[1];
        Assert.Equal(role, RoleOf(picked));
        // Shown as a hover first, for the lock in animation
        RankedResolutionPlayerState hover = Assert.Single(host.Snapshots[1].Data.PlayersOnDeck);
        Assert.Equal(picked, hover.Intention);
        Assert.Equal(RankedResolutionPlayerState.ReadyState.Selected, hover.OnDeckness);
    }

    [Fact]
    public void Select_TwoOnDeck_EndsSubPhaseAfterBoth()
    {
        host.HasBans = false;
        _ = Start(Bot, Bot, Bot, Bot, Human, Human2, Bot, Bot);
        SkipBotSubPhasesTo(2);
        Assert.Equal([5, 6], Deck());

        CharacterType first = Unused();
        Assert.True(draft.Select(Human, first).Success);
        Assert.Equal(2, time.Waits);

        CharacterType second = Unused();
        Assert.True(draft.Select(Human2, second).Success);
        WaitForSubPhase(3);
        Assert.Equal(first, Data.EnemyTeamSelections[5]);
        Assert.Equal(second, Data.EnemyTeamSelections[6]);
    }

    [Fact]
    public void Select_Multilancer_ActsOnSlotOnDeck()
    {
        host.HasBans = false;
        AddSlots(host, Bot, Bot, Bot, Bot, Human, Human, Bot, Bot);
        LobbyServerPlayerInfo main = host.GetPlayerById(5)!;
        LobbyServerPlayerInfo proxy = host.GetPlayerById(6)!;
        proxy.ControllingPlayerId = main.PlayerId;
        proxy.ControllingPlayerInfo = main;
        main.ProxyPlayerIds.Add(proxy.PlayerId);
        _ = draft.RunAsync();

        // The double pick of slots 5 and 6 is split, as they belong to one account
        SkipBotSubPhasesTo(2);
        Assert.Equal([5], Deck());
        CharacterType first = Unused();
        Assert.True(draft.Select(Human, first).Success);
        WaitForSubPhase(3);
        Assert.Equal([6], Deck());
        CharacterType second = Unused();
        Assert.True(draft.Select(Human, second).Success);
        WaitForSubPhase(4);

        Assert.Equal(first, Data.EnemyTeamSelections[5]);
        Assert.Equal(second, Data.EnemyTeamSelections[6]);
    }

    [Fact]
    public void Select_AccountNotInRoster_Fails()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();

        DraftRequestResult result = draft.Select(Stranger, Tank);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task Select_AfterSubPhaseEnded_RejectedAndHoverStands()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();
        draft.Hover(Human, Tank);
        using ManualResetEventSlim held = new();
        using ManualResetEventSlim resume = new();
        Task timingOut = TimeOutFirstSubPhaseAndHold(host.Timeouts.Pick, held, resume);

        DraftRequestResult result = draft.Select(Human, Unused());

        Assert.False(result.Success);
        Assert.Equal("CannotReady", result.Failure?.Term);
        resume.Set();
        await timingOut.WaitAsync(TimeSpan.FromSeconds(5));
        WaitForSubPhase(2);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
    }

    // Two players on deck lock in the same character at once: exactly one of them gets it
    [Fact]
    public void Select_SameCharacterAtOnce_OneGetsIt()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            FakeDraftHost twoOnDeck = new() { HasBans = false };
            DraftTime clock = new();
            DraftController controller = new(twoOnDeck, clock);
            AddSlots(twoOnDeck, Bot, Bot, Bot, Bot, Human, Human2, Bot, Bot);
            _ = controller.RunAsync();
            clock.Advance(BotSubPhase);
            WaitFor(() => clock.Waits == 2);
            CharacterType contested = Playable()
                .First(c => !controller.GetStateForTest().FriendlyTeamSelections.ContainsValue(c));

            using Barrier barrier = new(2);
            DraftRequestResult[] results = new DraftRequestResult[2];
            Task[] selects = new[] { Human, Human2 }
                .Select((accountId, i) => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    results[i] = controller.Select(accountId, contested);
                }))
                .ToArray();
            Assert.True(Task.WaitAll(selects, TimeSpan.FromSeconds(5)));

            Assert.Single(results, r => r.Success);
            Assert.Equal("CharacterTypeNotAllowed", results.Single(r => !r.Success).Failure?.Term);
            Assert.Single(controller.GetStateForTest().EnemyTeamSelections.Values, c => c == contested);
        }
    }

    // ---- Trade ----

    // Without bans: slot 1 picks Tank, slot 2 picks what it returns, bots pick the rest, then the trade sub-phase
    private CharacterType StartAndReachTrade()
    {
        host.HasBans = false;
        _ = Start(Human, Human2, Bot, Bot, Bot, Bot, Bot, Bot);
        Assert.True(draft.Select(Human, Tank).Success);
        WaitForSubPhase(2);
        SkipBotSubPhasesTo(3);
        CharacterType secondPick = Unused();
        Assert.True(draft.Select(Human2, secondPick).Success);
        WaitForSubPhase(4);
        SkipBotSubPhasesTo(7);
        Assert.Equal(FREELANCER_TRADE, draft.PhaseSubType);
        return secondPick;
    }

    [Fact]
    public void Trade_OfferToHuman_CreatesPendingTrade()
    {
        CharacterType secondPick = StartAndReachTrade();

        Assert.True(draft.Trade(Human, Offer(secondPick)).Success);

        RankedTradeData trade = Assert.Single(Data.TradeActions);
        Assert.Equal(1, trade.OfferingPlayerId);
        Assert.Equal(2, trade.AskedPlayerId);
        Assert.Equal(Tank, trade.OfferedCharacter);
        Assert.Equal(secondPick, trade.DesiredCharacter);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
        Assert.Equal(secondPick, Data.FriendlyTeamSelections[2]);
    }

    [Fact]
    public void Trade_Accept_SwapsAndClearsOffers()
    {
        CharacterType secondPick = StartAndReachTrade();
        draft.Trade(Human, Offer(secondPick));

        Assert.True(draft.Trade(Human2, Offer(Tank)).Success);

        Assert.Empty(Data.TradeActions);
        Assert.Equal(secondPick, Data.FriendlyTeamSelections[1]);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[2]);
    }

    [Fact]
    public void Trade_Reject_RemovesOffer()
    {
        CharacterType secondPick = StartAndReachTrade();
        draft.Trade(Human, Offer(secondPick));

        Assert.True(draft.Trade(Human2, Reject(Tank)).Success);

        Assert.Empty(Data.TradeActions);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
        Assert.Equal(secondPick, Data.FriendlyTeamSelections[2]);
    }

    [Fact]
    public void Trade_RejectWithoutOffer_Fails()
    {
        StartAndReachTrade();

        Assert.False(draft.Trade(Human2, Reject(Tank)).Success);
    }

    [Fact]
    public void Trade_WithBot_SwapsRightAway()
    {
        StartAndReachTrade();
        CharacterType botCharacter = Data.FriendlyTeamSelections[3];

        Assert.True(draft.Trade(Human, Offer(botCharacter)).Success);

        Assert.Empty(Data.TradeActions);
        Assert.Equal(botCharacter, Data.FriendlyTeamSelections[1]);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[3]);
    }

    [Fact]
    public void Trade_FromSlotNotControlled_Fails()
    {
        StartAndReachTrade();

        Assert.False(draft.Trade(Human, Offer(Tank, offeringPlayerId: 2)).Success);
        Assert.False(draft.Trade(Stranger, Offer(Tank)).Success);

        Assert.Empty(Data.TradeActions);
    }

    [Fact]
    public void Trade_ForCharacterNotOnTeam_Fails()
    {
        StartAndReachTrade();

        Assert.False(draft.Trade(Human, Offer(Data.EnemyTeamSelections[5])).Success);

        Assert.Empty(Data.TradeActions);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
    }

    // Before, it changed selections that had already been applied to the roster
    [Fact]
    public void Trade_AfterDraftEnded_Rejected()
    {
        CharacterType secondPick = StartAndReachTrade();
        time.Advance(host.Timeouts.Trade);
        WaitFor(() => host.GetPlayerById(1)!.CharacterType == Tank);

        Assert.False(draft.Trade(Human, Offer(secondPick)).Success);

        Assert.Empty(Data.TradeActions);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
    }

    [Fact]
    public void Trade_InPickPhase_Fails()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();

        Assert.False(draft.Trade(Human, Offer(Tank)).Success);
    }

    // ---- Full runs ----

    [Fact]
    public async Task Run_EveryoneLocksIn_GoesThroughPhasesInOrderAndAppliesPicks()
    {
        Task run = Start(1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008);
        List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> phases =
            DraftController.GetDraftPhases(host.Players, hasBans: true, hasTrade: true);
        Queue<CharacterType> characters = new(Playable());
        Dictionary<int, CharacterType> picks = [];
        List<FreelancerResolutionPhaseSubType> seenPhases = [];
        List<List<int>> seenDecks = [];

        for (int subPhase = 1; subPhase <= phases.Count; subPhase++)
        {
            WaitForSubPhase(subPhase);
            FreelancerResolutionPhaseSubType phase = draft.PhaseSubType;
            List<int> deck = Deck();
            seenPhases.Add(phase);
            seenDecks.Add(deck);
            if (phase == FREELANCER_TRADE)
            {
                time.Advance(host.Timeouts.Trade);
                break;
            }

            foreach (int playerId in deck)
            {
                long accountId = host.GetPlayerById(playerId)!.AccountId;
                CharacterType characterType = characters.Dequeue();
                if (phase is PICK_BANS1 or PICK_BANS2)
                {
                    Assert.True(draft.Ban(accountId, characterType).Success);
                }
                else
                {
                    Assert.True(draft.Select(accountId, characterType).Success);
                    picks[playerId] = characterType;
                }
            }
        }

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(phases.Select(p => p.Value), seenPhases);
        Assert.Equal(
            phases.Select(p => p.Key == -1 ? [] : p.Value == PICK_FREELANCER2 ? new List<int> { p.Key + 1, p.Key + 2 } : [p.Key + 1]),
            seenDecks);
        Assert.All(host.Players, slot => Assert.Equal(picks[slot.PlayerId], slot.CharacterType));
        Assert.Empty(host.Cancellations);
    }

    // Current behavior: the trade sub-phase is monitored with an empty placeholder slot, which counts as human,
    // so it lasts its full timeout even when only bots are drafting
    [Fact]
    public async Task Run_OnlyBots_FiveSecondsPerSubPhaseAndFullTrade()
    {
        Task run = Start(Bot, Bot, Bot, Bot, Bot, Bot, Bot, Bot);

        SkipBotSubPhasesTo(11);
        Assert.Equal(FREELANCER_TRADE, draft.PhaseSubType);
        time.Advance(host.Timeouts.Trade - Tick);
        Assert.False(run.IsCompleted);
        time.Advance(Tick);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, Data.FriendlyBans.Count + Data.EnemyBans.Count);
        foreach (Team team in new[] { Team.TeamA, Team.TeamB })
        {
            List<LobbyServerPlayerInfo> slots = host.Players.Where(p => p.TeamId == team).ToList();
            Assert.Equal(4, slots.Select(p => p.CharacterType).Distinct().Count());
            Assert.Contains(slots, p => RoleOf(p.CharacterType) == CharacterRole.Tank);
            Assert.Contains(slots, p => RoleOf(p.CharacterType) == CharacterRole.Support);
            // Bots are named after their character
            Assert.All(slots, p => Assert.Equal(p.CharacterType.ToString(), p.Handle));
        }
    }

    [Fact]
    public async Task Run_ControlAllBots_NoTradeAndPausesAtEnd()
    {
        host.IsControlAllBots = true;
        Task run = Start(Bot, Bot, Bot, Bot, Bot, Bot, Bot, Bot);
        List<FreelancerResolutionPhaseSubType> seenPhases = [draft.PhaseSubType];
        while (time.Waits < 10)
        {
            SkipBotSubPhasesTo(time.Waits + 1);
            seenPhases.Add(draft.PhaseSubType);
        }

        // The last pick ends, and the draft pauses for the lock in animation
        SkipBotSubPhasesTo(11);
        time.Advance(EndPause - Tick);
        Assert.False(run.IsCompleted);
        time.Advance(Tick);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(FREELANCER_TRADE, seenPhases);
        Assert.Equal(PICK_FREELANCER1, seenPhases[^1]);
    }

    [Fact]
    public async Task Run_NoBans_SkipsBanPhases()
    {
        host.HasBans = false;
        Task run = Start(Bot, Bot, Bot, Bot, Bot, Bot, Bot, Bot);
        List<FreelancerResolutionPhaseSubType> seenPhases = [draft.PhaseSubType];
        while (time.Waits < 7)
        {
            SkipBotSubPhasesTo(time.Waits + 1);
            seenPhases.Add(draft.PhaseSubType);
        }

        time.Advance(host.Timeouts.Trade);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            [PICK_FREELANCER1, PICK_FREELANCER2, PICK_FREELANCER1, PICK_FREELANCER1, PICK_FREELANCER2, PICK_FREELANCER1, FREELANCER_TRADE],
            seenPhases);
        Assert.Empty(Data.FriendlyBans);
        Assert.Empty(Data.EnemyBans);
    }

    [Fact]
    public async Task Run_HumanDoesNotPick_CancelsWithThemAsDodger()
    {
        host.HasBans = false;
        Task run = StartWithHumanInSlot1();

        time.Advance(host.Timeouts.Pick);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([("Player#1001", Human)], host.Cancellations);
        Assert.All(host.Players, slot => Assert.Equal(CharacterType.None, slot.CharacterType));
    }

    [Fact]
    public void Run_HumanHoveredButDidNotLockIn_LocksInHover()
    {
        host.HasBans = false;
        _ = StartWithHumanInSlot1();
        draft.Hover(Human, Tank);

        time.Advance(host.Timeouts.Pick);

        WaitForSubPhase(2);
        Assert.Equal(Tank, Data.FriendlyTeamSelections[1]);
        Assert.Empty(host.Cancellations);
    }

    // Before, the draft canceled the match and then crashed on the missing bot
    [Fact]
    public async Task Run_BotNotInDraft_CancelsMatch()
    {
        Task run = StartWithHumanInSlot1();
        // The bot on deck in the second sub-phase is not among the slots the draft started with
        host.GetPlayerById(5)!.PlayerId = 55;

        draft.Ban(Human, Tank);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([(null, null)], host.Cancellations);
    }

    [Fact]
    public async Task Run_ParticipantDisconnected_CancelsAtNextSubPhase()
    {
        Task run = StartWithHumanInSlot1();
        host.Connected = false;

        draft.Ban(Human, Tank);

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([(null, null)], host.Cancellations);
        // Start and ban, but no start of the next sub-phase
        Assert.Equal(2, host.Snapshots.Count);
    }
}
