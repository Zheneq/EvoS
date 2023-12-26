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
    public float TeamConfidenceBalanceWeight = 1;
    
    public TimeSpan WaitingTimeWeightCap = TimeSpan.FromMinutes(15);
    public int TeammateEloDifferenceWeightCap = 250;
    
    
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