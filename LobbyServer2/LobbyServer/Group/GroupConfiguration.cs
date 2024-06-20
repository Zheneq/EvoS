using System;

namespace CentralServer.LobbyServer.Group;

public class GroupConfiguration
{
    public int MaxGroupSize = 5;
    public TimeSpan InviteTimeout = TimeSpan.FromSeconds(20);
    public bool CanInviteActiveOpponents;
}