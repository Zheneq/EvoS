using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Moq;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class MatchmakerTest : EvosTest
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakerTest));
    private const string EloKey = "elo_key";

    private static readonly Dictionary<long, PersistedAccountData> Accounts = new()
    {
        { 1, MakeAccount(1, "Psycho", 2260, 2) },
        { 2, MakeAccount(2, "Donut", 2180, 2) },
        { 3, MakeAccount(3, "Kid", 2070, 2) },
        { 4, MakeAccount(4, "Goose", 2050, 2) },
        { 5, MakeAccount(5, "Joke", 2050, 2) },
        { 6, MakeAccount(6, "Dozen", 2040, 2) },
        { 7, MakeAccount(7, "Script", 1990, 2) },
        { 8, MakeAccount(8, "Dream", 1960, 2) },
        { 9, MakeAccount(9, "Dolly", 1910, 2) },
        { 10, MakeAccount(10, "Darkness", 1860, 2) },
        { 11, MakeAccount(11, "Assault", 1560, 2) },
        { 12, MakeAccount(12, "Grounded", 1550, 2) },
        { 13, MakeAccount(13, "Crossbow", 1500, 2) },
        { 14, MakeAccount(14, "Hammer", 1500, 1) },
        { 15, MakeAccount(15, "Everyone", 1500, 1) },
        { 16, MakeAccount(16, "Cater", 1500, 0) },
        { 17, MakeAccount(17, "Crackle", 1500, 0) },
        { 18, MakeAccount(18, "Doe", 1460, 2) },
        { 19, MakeAccount(19, "Adult", 1450, 2) },
        { 20, MakeAccount(20, "Geek", 1340, 2) },
        { 21, MakeAccount(21, "Pie", 1230, 2) },
        { 22, MakeAccount(22, "Medic", 1060, 2) },
    };

    public MatchmakerTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public void Test()
    {
        Matchmaker matchmaker = MakeMatchmaker(new MatchmakingConfiguration
        {
            MaxTeamEloDifferenceStart = 20,
            MaxTeamEloDifference = 70,
            MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(5),

            TeamEloDifferenceWeight = 3,
            TeammateEloDifferenceWeight = 1,
            WaitingTimeWeight = 5,
            WaitingTimeWeightCap = TimeSpan.FromMinutes(15),
            TeammateEloDifferenceWeightCap = 250,
        });

        DateTime now = DateTime.UtcNow;
        int i = 0;
        List<Matchmaker.MatchmakingGroup> queuedGroups = new List<Matchmaker.MatchmakingGroup>
        {
            new(i++, new List<long> {1, 2}, now - TimeSpan.FromMinutes(2)),
            new(i++, new List<long> {3, 4, 5, 6}, now - TimeSpan.FromMinutes(1)),
            new(i++, new List<long> {7}, now - TimeSpan.FromMinutes(2)),
            new(i++, new List<long> {8}, now - TimeSpan.FromMinutes(2)),
            new(i++, new List<long> {9}, now - TimeSpan.FromMinutes(3)),
            new(i++, new List<long> {10}, now - TimeSpan.FromMinutes(3)),
            new(i++, new List<long> {11}, now - TimeSpan.FromMinutes(4)),
            new(i++, new List<long> {12}, now - TimeSpan.FromMinutes(8)),
        };
        
        List<Matchmaker.Match> matchesRanked = matchmaker.GetMatchesRanked(queuedGroups, now);

        foreach (Matchmaker.Match match in matchesRanked)
        {
            log.Info($"{match}");
        }
    }

    [Fact]
    public void TestTryToWaitIfUnbalanced()
    {
        Matchmaker matchmaker = MakeMatchmaker(new MatchmakingConfiguration
        {
            MaxTeamEloDifferenceStart = 20,
            MaxTeamEloDifference = 30,
            MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(10),
        });

        DateTime now = DateTime.UtcNow;
        int i = 0;
        List<Matchmaker.MatchmakingGroup> queuedGroups = new List<Matchmaker.MatchmakingGroup>
        {
            new(i++, new List<long> {1, 2}, now - TimeSpan.FromSeconds(30)),
            new(i++, new List<long> {3, 4, 5, 6}, now - TimeSpan.FromSeconds(1)),
            new(i++, new List<long> {8}, now - TimeSpan.FromSeconds(50)),
            new(i++, new List<long> {9}, now - TimeSpan.FromSeconds(50)),
        };
        
        List<Matchmaker.Match> matchesRanked = matchmaker.GetMatchesRanked(queuedGroups, now);
        Assert.Empty(matchesRanked);
        matchesRanked = matchmaker.GetMatchesRanked(queuedGroups,  now + TimeSpan.FromMinutes(10));
        Assert.Single(matchesRanked);
    }

    [Fact]
    public void TestDoNotMatchUnbalanced()
    {
        Matchmaker matchmaker = MakeMatchmaker(new MatchmakingConfiguration
        {
            MaxTeamEloDifferenceStart = 20,
            MaxTeamEloDifference = 20,
            MaxTeamEloDifferenceWaitTime = TimeSpan.FromMinutes(10),
        });

        DateTime now = DateTime.UtcNow;
        int i = 0;
        List<Matchmaker.MatchmakingGroup> queuedGroups = new List<Matchmaker.MatchmakingGroup>
        {
            new(i++, new List<long> {1, 2}, now - TimeSpan.FromMinutes(30)),
            new(i++, new List<long> {3, 4, 5, 6}, now - TimeSpan.FromMinutes(30)),
            new(i++, new List<long> {8}, now - TimeSpan.FromMinutes(50)),
            new(i++, new List<long> {9}, now - TimeSpan.FromMinutes(50)),
        };
        
        List<Matchmaker.Match> matchesRanked = matchmaker.GetMatchesRanked(queuedGroups, now);
        Assert.Empty(matchesRanked);
    }

    private static Matchmaker MakeMatchmaker(MatchmakingConfiguration conf)
    {
        AccountDao dao = MockAccountDao(Accounts);
        Matchmaker matchmaker = new Matchmaker(
            dao,
            GameType.PvP,
            new GameSubType
            {
                LocalizedName = "Test",
                TeamAPlayers = 4,
                TeamBPlayers = 4
            },
            EloKey,
            () => conf);
        return matchmaker;
    }

    private static AccountDao MockAccountDao(Dictionary<long, PersistedAccountData> accounts)
    {
        var mock = new Mock<AccountDao>();
        mock
            .Setup(dao => dao.GetAccount(It.IsAny<long>()))
            .Returns((long accId) => accounts[accId]);
        return mock.Object;
    }

    private static PersistedAccountData MakeAccount(long accId, string username, float elo, int eloConfidenceLevel)
    {
        var acc = new PersistedAccountData
        {
            AccountId = accId,
            UserName = username,
            Handle = $"{username}#{accId}",
            ExperienceComponent = new ExperienceComponent
            {
                EloValues = new EloValues()
            },
            AccountComponent = new AccountComponent
            {
                LastCharacter = CharacterType.PendingWillFill
            },
            SocialComponent = new SocialComponent
            {
                BlockedAccounts = new HashSet<long>()
            },
        };
        
        acc.ExperienceComponent.EloValues.UpdateElo(EloKey, elo, eloConfidenceLevel);

        return acc;
    }
}
