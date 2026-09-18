using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Network.Static;

namespace Tests;

public class QueuePenaltyManagerTest
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(200);
    private static readonly TimeSpan Cap = TimeSpan.FromHours(48);
    private static readonly TimeSpan Parole = TimeSpan.FromHours(72);

    private static QueuePenalties Penalties(int count = 0, DateTime? block = null, DateTime? parole = null)
    {
        return new QueuePenalties
        {
            QueueDodgeCount = count,
            QueueDodgeBlockTimeout = block ?? DateTime.MinValue,
            QueueDodgeParoleTimeout = parole ?? DateTime.MinValue,
        };
    }

    private static QueuePenaltyManager.PenaltyEvaluation Escalate(QueuePenalties current, TimeSpan span)
    {
        return QueuePenaltyManager.EvaluatePenalty(
            current, span, Now, overridePenalty: false, capPenalty: false, escalate: true, Cap, Parole);
    }

    // --- Escalation (#3) ---

    [Fact]
    public void FirstOffense_AppliesBaseBlockCountOneAndSetsParole()
    {
        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(Penalties(), Base);

        Assert.True(eval.Apply);
        Assert.Equal(1, eval.Count);
        Assert.Equal(Base, eval.AppliedSpan);
        Assert.Equal(Now.Add(Base), eval.BlockTimeout);
        Assert.Equal(Now.Add(Parole), eval.ParoleTimeout);
    }

    [Theory]
    [InlineData(0, 200)]    // 200 * 4^0
    [InlineData(1, 800)]    // 200 * 4^1
    [InlineData(2, 3200)]    // 200 * 4^2
    [InlineData(3, 12800)]   // 200 * 4^3
    [InlineData(4, 51200)]   // 200 * 4^4
    public void Escalation_QuadruplesPerOffense(int startingCount, int expectedSeconds)
    {
        // Parole still active so the count is not reset.
        QueuePenalties current = Penalties(count: startingCount, parole: Now.Add(TimeSpan.FromHours(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.True(eval.Apply);
        Assert.Equal(startingCount + 1, eval.Count);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), eval.AppliedSpan);
        Assert.Equal(Now.Add(TimeSpan.FromSeconds(expectedSeconds)), eval.BlockTimeout);
    }

    [Fact]
    public void Escalation_ClampedToCap()
    {
        QueuePenalties current = Penalties(count: 10, parole: Now.Add(TimeSpan.FromHours(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.True(eval.Apply);
        Assert.Equal(11, eval.Count);
        Assert.Equal(Cap, eval.AppliedSpan);
        Assert.Equal(Now.Add(Cap), eval.BlockTimeout);
    }

    [Fact]
    public void Escalation_ParoleLapsed_ResetsCountToBase()
    {
        // Parole window already elapsed -> streak forgiven, next offense starts back at base.
        QueuePenalties current = Penalties(count: 3, parole: Now.Subtract(TimeSpan.FromSeconds(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.True(eval.Apply);
        Assert.Equal(1, eval.Count);
        Assert.Equal(Base, eval.AppliedSpan);
    }

    [Fact]
    public void Escalation_ParoleActive_ContinuesStreak()
    {
        QueuePenalties current = Penalties(count: 3, parole: Now.Add(TimeSpan.FromHours(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.Equal(4, eval.Count);
        Assert.Equal(TimeSpan.FromSeconds(12800), eval.AppliedSpan);
    }

    [Fact]
    public void Escalation_ParoleUnset_DoesNotResetStreak()
    {
        // MinValue parole (never penalized before this streak) must not count as "lapsed".
        QueuePenalties current = Penalties(count: 2, parole: DateTime.MinValue);

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.Equal(3, eval.Count);
        Assert.Equal(TimeSpan.FromSeconds(3200), eval.AppliedSpan);
    }

    [Fact]
    public void Escalation_LongerActiveBlock_RecordsOffenseWithoutLoweringBlock()
    {
        // Parole lapsed -> streak restarts at 1 -> span is just Base, far below the active block.
        // The offense must still be recorded (count + parole), while the block must not shrink.
        DateTime block = Now.Add(TimeSpan.FromHours(6));
        QueuePenalties current = Penalties(count: 3, block: block, parole: Now.Subtract(TimeSpan.FromSeconds(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.True(eval.Apply);
        Assert.Equal(1, eval.Count);
        Assert.Equal(block, eval.BlockTimeout);                     // not lowered
        Assert.Equal(Now.Add(Parole), eval.ParoleTimeout);          // parole refreshed
    }

    [Fact]
    public void Escalation_LongerActiveBlock_ActiveParole_ContinuesStreakWithoutLoweringBlock()
    {
        DateTime block = Now.Add(TimeSpan.FromHours(6));
        QueuePenalties current = Penalties(count: 1, block: block, parole: Now.Add(TimeSpan.FromHours(1)));

        // 200 * 4^1 = 800s, still below the active block.
        QueuePenaltyManager.PenaltyEvaluation eval = Escalate(current, Base);

        Assert.True(eval.Apply);
        Assert.Equal(2, eval.Count);
        Assert.Equal(block, eval.BlockTimeout);
        Assert.Equal(Now.Add(Parole), eval.ParoleTimeout);
    }

    // --- Direction check (normal mode raises only) ---

    [Fact]
    public void NormalMode_DoesNotLowerActiveLongerBlock()
    {
        QueuePenalties current = Penalties(count: 2, block: Now.Add(TimeSpan.FromHours(2)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, Base, Now, overridePenalty: false, capPenalty: false, escalate: false, Cap, Parole);

        Assert.False(eval.Apply);
        Assert.Equal(2, eval.Count);                                // untouched
        Assert.Equal(Now.Add(TimeSpan.FromHours(2)), eval.BlockTimeout);
    }

    [Fact]
    public void NormalMode_RaisesBlock_Applies()
    {
        QueuePenalties current = Penalties(count: 0, block: Now.Add(TimeSpan.FromSeconds(100)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, Base, Now, overridePenalty: false, capPenalty: false, escalate: false, Cap, Parole);

        Assert.True(eval.Apply);
        Assert.Equal(0, eval.Count);                                // non-escalate leaves count alone
        Assert.Equal(Now.Add(Base), eval.BlockTimeout);
    }

    [Fact]
    public void NoChange_WhenTimeoutIdentical()
    {
        QueuePenalties current = Penalties(block: Now.Add(Base));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, Base, Now, overridePenalty: false, capPenalty: false, escalate: false, Cap, Parole);

        Assert.False(eval.Apply);
    }

    [Fact]
    public void Override_BypassesDirectionCheck()
    {
        QueuePenalties current = Penalties(count: 0, block: Now.Add(TimeSpan.FromHours(2)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, Base, Now, overridePenalty: true, capPenalty: false, escalate: false, Cap, Parole);

        Assert.True(eval.Apply);
        Assert.Equal(Now.Add(Base), eval.BlockTimeout);
    }

    // --- Cap / pardon (lowers only, decrements streak) ---

    [Fact]
    public void CapMode_LowersBlock_AndDecrementsCount()
    {
        QueuePenalties current = Penalties(count: 3, block: Now.Add(TimeSpan.FromHours(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, TimeSpan.FromSeconds(15), Now, overridePenalty: false, capPenalty: true, escalate: false, Cap, Parole);

        Assert.True(eval.Apply);
        Assert.Equal(2, eval.Count);                                // pardon forgives one offense
        Assert.Equal(Now.Add(TimeSpan.FromSeconds(15)), eval.BlockTimeout);
    }

    [Fact]
    public void CapMode_DoesNotRaiseShorterBlock()
    {
        QueuePenalties current = Penalties(count: 1, block: Now.Add(TimeSpan.FromSeconds(5)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, TimeSpan.FromSeconds(15), Now, overridePenalty: false, capPenalty: true, escalate: false, Cap, Parole);

        Assert.False(eval.Apply);
        Assert.Equal(1, eval.Count);                                // untouched when not applied
    }

    [Fact]
    public void CapMode_CountFloorsAtZero()
    {
        QueuePenalties current = Penalties(count: 0, block: Now.Add(TimeSpan.FromHours(1)));

        QueuePenaltyManager.PenaltyEvaluation eval = QueuePenaltyManager.EvaluatePenalty(
            current, TimeSpan.FromSeconds(15), Now, overridePenalty: false, capPenalty: true, escalate: false, Cap, Parole);

        Assert.True(eval.Apply);
        Assert.Equal(0, eval.Count);
    }

    // --- Enforcement grace window (IsQueueBlocked) ---

    [Fact]
    public void IsQueueBlocked_NullPenalties_ReturnsFalse()
    {
        Assert.False(QueuePenaltyManager.IsQueueBlocked(null, Now));
    }

    [Theory]
    [InlineData(-3600, false)]   // expired
    [InlineData(0.5, false)]     // within 1s grace
    [InlineData(1, false)]       // exactly at grace boundary (not strictly greater)
    [InlineData(2, true)]        // beyond grace
    [InlineData(600, true)]      // well beyond
    public void IsQueueBlocked_HonorsFiveSecondGrace(float blockOffsetSeconds, bool expected)
    {
        QueuePenalties current = Penalties(block: Now.Add(TimeSpan.FromSeconds(blockOffsetSeconds)));

        Assert.Equal(expected, QueuePenaltyManager.IsQueueBlocked(current, Now));
    }
}
