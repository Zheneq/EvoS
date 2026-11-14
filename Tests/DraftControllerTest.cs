using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Microsoft.Extensions.Time.Testing;
using Tests.Lib;
using Xunit.Abstractions;
using static EvoS.Framework.Constants.Enums.FreelancerResolutionPhaseSubType;

namespace Tests;

// DraftController run over a fake host and fake time
public class DraftControllerTest : EvosTest
{
    private const long Human = 1001;
    private const CharacterType Tank = CharacterType.Rampart;
    private static readonly TimeSpan BotSubPhase = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    private readonly FakeDraftHost host = new();
    private readonly FakeTimeProvider time = new();
    private readonly DraftController draft;

    // Slot 1 is a human on TeamA, all other slots are bots. With bans, the draft starts with
    // slot 1 banning, then slot 5 banning, then slot 1 picking.
    public DraftControllerTest(ITestOutputHelper output) : base(output)
    {
        host.Players.AddRange(Enumerable.Range(1, 8)
            .Select(playerId => new LobbyServerPlayerInfo
            {
                PlayerId = playerId,
                TeamId = playerId <= 4 ? Team.TeamA : Team.TeamB,
                AccountId = playerId == 1 ? Human : 0,
                IsNPCBot = playerId != 1,
            }));
        draft = new DraftController(host, time);
    }

    private List<int> Deck() => draft.GetRankedResolutionPhaseData().PlayersOnDeck.Select(p => p.PlayerId).ToList();

    // A lock-in or a cancellation ends the sub-phase asynchronously
    private static void WaitFor(Func<bool> condition)
    {
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Start_SendsSnapshotWithFullTimeout()
    {
        _ = draft.RunAsync();

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
        _ = draft.RunAsync();

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.True(draft.Hover(Human, Tank).Success);

        Assert.Equal(host.Timeouts.Ban1 - TimeSpan.FromSeconds(10), host.Snapshots[^1].Data.TimeLeftInSubPhase);
    }

    [Fact]
    public void HumanSubPhase_LastsItsTimeout()
    {
        _ = draft.RunAsync();

        time.Advance(host.Timeouts.Ban1 - Tick);
        Assert.Equal([1], Deck());

        time.Advance(Tick);
        WaitFor(() => Deck() is [5]);
        // Nothing was banned, so the draft banned something
        Assert.Single(draft.GetRankedResolutionPhaseData().FriendlyBans);
    }

    [Fact]
    public void LockIn_EndsSubPhaseWithoutWaiting()
    {
        _ = draft.RunAsync();

        Assert.True(draft.Ban(Human, Tank).Success);

        WaitFor(() => Deck() is [5]);
        Assert.Equal([Tank], draft.GetRankedResolutionPhaseData().FriendlyBans);
    }

    [Fact]
    public void BotSubPhase_EndsAfterFiveSeconds()
    {
        _ = draft.RunAsync();
        draft.Ban(Human, Tank);
        WaitFor(() => Deck() is [5]);

        time.Advance(BotSubPhase - Tick);
        Assert.Equal([5], Deck());

        time.Advance(Tick);
        WaitFor(() => Deck() is [1]);
        Assert.Equal(PICK_FREELANCER1, draft.PhaseSubType);
        Assert.Single(draft.GetRankedResolutionPhaseData().EnemyBans);
    }

    [Fact]
    public async Task MatchCancelled_EndsRunWithoutWaiting()
    {
        Task run = draft.RunAsync();

        host.CancelMatch();

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(host.Snapshots);
        Assert.Empty(draft.GetRankedResolutionPhaseData().FriendlyBans);
    }
}
