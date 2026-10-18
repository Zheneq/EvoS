using System;
using System.Collections.Generic;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakingConfiguration
{
    public TimeSpan FallbackTime = TimeSpan.FromMinutes(10);
    
    public int MaxTeamEloDifferenceStart = 20;  // 20 elo diff = 53%/47%
    public int MaxTeamEloDifference = 70;  // 70 elo diff = 60%/40%
    public TimeSpan MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(5);
    public int MaxTeammateEloDifference = 400; // TODO allow it to be broken in groups? Or is it even a good thing with our playerbase size?

    public float TeamEloDifferenceWeight = 3;
    public float TeammateEloDifferenceWeight = 1;
    public float TeamCompositionWeight = 2;
    public float TeamBlockWeight = 2;
    public float WaitingTimeWeight = 5;
    public float SeatedWaitingTimeWeight = 0;
    public float TeamConfidenceBalanceWeight = 1;
    public float AsymmetricWeight = 3;
    public float TieBreakerWeight = 0.01f;
    
    public TimeSpan WaitingTimeWeightCap = TimeSpan.FromMinutes(15);
    public TimeSpan SeatedWaitingTimeWeightCap = TimeSpan.FromMinutes(15); // applied to each waiter separately
    public int TeammateEloDifferenceWeightCap = 250;
    // How much each group member beyond the first adds to the group's weight in waiting time factors:
    // 0 - a group counts once, same as a solo, 1 - each member counts as a separate waiter
    public float WaitingTimeGroupMemberFactor = 0;
    
    
    public float EloBasePot = 64.0f;
    public List<float> EloConfidenceFactor = new() { 1.0f, 0.75f, 0.5f };
    public List<TimeSpan> EloConfidenceRetention = new()
    {
        TimeSpan.FromDays(30),
        TimeSpan.FromDays(90),
        TimeSpan.FromDays(180),
    };
    public List<int> EloConfidenceUpgrade = new()
    {
        10,
        25,
    };
}