using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using static EvoS.Framework.Constants.Enums.FreelancerResolutionPhaseSubType;

namespace Tests;

public class DraftPhasesTest
{
    private const long NpcBot = 0;

    private static List<LobbyServerPlayerInfo> Slots(params long[] accountIds)
    {
        return accountIds
            .Select((accountId, i) => new LobbyServerPlayerInfo
            {
                PlayerId = i + 1,
                TeamId = i < 4 ? Team.TeamA : Team.TeamB,
                AccountId = accountId,
                IsNPCBot = accountId == NpcBot,
            })
            .ToList();
    }

    private static List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> Phases(
        params (int, FreelancerResolutionPhaseSubType)[] phases)
    {
        return phases.Select(p => new KeyValuePair<int, FreelancerResolutionPhaseSubType>(p.Item1, p.Item2)).ToList();
    }

    [Fact]
    public void RegularWithBans()
    {
        Assert.Equal(
            Phases(
                (0, PICK_BANS1),
                (4, PICK_BANS1),
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER2),
                (1, PICK_FREELANCER1),
                (5, PICK_BANS2),
                (1, PICK_BANS2),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER2),
                (7, PICK_FREELANCER1),
                (-1, FREELANCER_TRADE)),
            Game.GetDraftPhases(Slots(1, 2, 3, 4, 5, 6, 7, 8), hasBans: true, hasTrade: true));
    }

    [Fact]
    public void RegularWithoutBans()
    {
        Assert.Equal(
            Phases(
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER2),
                (1, PICK_FREELANCER1),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER2),
                (7, PICK_FREELANCER1),
                (-1, FREELANCER_TRADE)),
            Game.GetDraftPhases(Slots(1, 2, 3, 4, 5, 6, 7, 8), hasBans: false, hasTrade: true));
    }

    [Fact]
    public void ControlAllBotsWithBans()
    {
        Assert.Equal(
            Phases(
                (0, PICK_BANS1),
                (4, PICK_BANS1),
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER1),
                (5, PICK_FREELANCER1),
                (1, PICK_FREELANCER1),
                (5, PICK_BANS2),
                (1, PICK_BANS2),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER1),
                (3, PICK_FREELANCER1),
                (7, PICK_FREELANCER1)),
            Game.GetDraftPhases(Slots(1, 1, 1, 1, 2, 2, 2, 2), hasBans: true, hasTrade: false));
    }

    [Fact]
    public void ControlAllBotsWithoutBans()
    {
        Assert.Equal(
            Phases(
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER1),
                (5, PICK_FREELANCER1),
                (1, PICK_FREELANCER1),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER1),
                (3, PICK_FREELANCER1),
                (7, PICK_FREELANCER1)),
            Game.GetDraftPhases(Slots(1, 1, 1, 1, 2, 2, 2, 2), hasBans: false, hasTrade: false));
    }

    [Fact]
    public void ControlAllBotsAgainstNpcBots()
    {
        Assert.Equal(
            Phases(
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER2),
                (1, PICK_FREELANCER1),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER1),
                (3, PICK_FREELANCER1),
                (7, PICK_FREELANCER1)),
            Game.GetDraftPhases(
                Slots(1, 1, 1, 1, NpcBot, NpcBot, NpcBot, NpcBot),
                hasBans: false,
                hasTrade: false));
    }

    [Fact]
    public void AsymmetricProxy()
    {
        Assert.Equal(
            Phases(
                (0, PICK_FREELANCER1),
                (4, PICK_FREELANCER1),
                (5, PICK_FREELANCER1),
                (1, PICK_FREELANCER1),
                (6, PICK_FREELANCER1),
                (2, PICK_FREELANCER2),
                (7, PICK_FREELANCER1),
                (-1, FREELANCER_TRADE)),
            Game.GetDraftPhases(Slots(1, 2, 3, 4, 5, 5, 6, 7), hasBans: false, hasTrade: true));
    }
}
