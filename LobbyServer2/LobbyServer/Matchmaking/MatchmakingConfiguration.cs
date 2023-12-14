using System;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakingConfiguration
{
    public int MaxTeamEloDifferenceStart = 30;
    public int MaxTeamEloDifference = 60;
    public TimeSpan MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(3);
    public int MaxTeammateEloDifference = 400; // TODO allow it to be broken in groups? Or is it even a good thing with our playerbase size?

    public float TeamEloDifferenceWeight = 2;
    public float TeammateEloDifferenceWeight = 1;
    public float WaitingTimeWeight = 3;
    public TimeSpan WaitingTimeWeightCap = TimeSpan.FromMinutes(15);
    public int TeammateEloDifferenceWeightCap = 250;
}