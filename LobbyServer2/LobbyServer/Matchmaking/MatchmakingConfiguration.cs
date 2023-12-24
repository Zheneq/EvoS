using System;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakingConfiguration
{
    public int MaxTeamEloDifferenceStart = 20;   // 20 elo diff = 53%/47%
    public int MaxTeamEloDifference = 70;  // 70 elo diff = 60%/40%
    public TimeSpan MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(5);
    public int MaxTeammateEloDifference = 400; // TODO allow it to be broken in groups? Or is it even a good thing with our playerbase size?

    public float TeamEloDifferenceWeight = 3;
    public float TeammateEloDifferenceWeight = 1;
    public float TeamCompositionWeight = 2;
    public float TeamBlockWeight = 2;
    public float WaitingTimeWeight = 5;
    public TimeSpan WaitingTimeWeightCap = TimeSpan.FromMinutes(15);
    public int TeammateEloDifferenceWeightCap = 250;
}