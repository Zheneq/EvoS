using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess.Daos;
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
    private static readonly DateTime Now = new DateTime(2023, 12, 25);

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
        AccountDao dao = MockAccountDao(Accounts);
        
        Matchmaker matchmaker = new Matchmaker(
            dao,
            null, // TODO match history dao
            GameType.PvP,
            new GameSubType
            {
                LocalizedName = "Test",
                TeamAPlayers = 4,
                TeamBPlayers = 4
            },
            EloKey,
            new MatchmakingConfiguration
            {
                
            });
        
        int i = 0;
        List<Matchmaker.MatchmakingGroup> queuedGroups = new List<Matchmaker.MatchmakingGroup>()
        {
            new(i++, Team.Invalid, new List<long> {1, 2}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {3, 4, 5, 6}, Now - TimeSpan.FromMinutes(1)),
            new(i++, Team.Invalid, new List<long> {7}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {8}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {9}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {10}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {11}, Now - TimeSpan.FromMinutes(5)),
            new(i++, Team.Invalid, new List<long> {12}, Now - TimeSpan.FromMinutes(5)),
        };
        
        List<Matchmaker.Match> matchesRanked = matchmaker.GetMatchesRanked(queuedGroups, Now);

        foreach (Matchmaker.Match match in matchesRanked)
        {
            log.Info($"{match}");
        }
    }
    
    private static AccountDao MockAccountDao(Dictionary<long, PersistedAccountData> accounts)
    {
        var mock = new Mock<AccountDao>();
        mock
            .Setup(dao => dao.GetAccount(It.IsAny<long>()))
            .Returns((long accId) => accounts[accId]);
        return mock.Object;
    }

    private static PersistedAccountData MakeAccount(long accId, string handle, float elo, int eloConfidenceLevel)
    {
        var acc = new PersistedAccountData
        {
            AccountId = accId,
            UserName = handle,
            Handle = handle,
            ExperienceComponent = new ExperienceComponent
            {
                EloValues = new EloValues()
            }
        };
        
        acc.ExperienceComponent.EloValues.UpdateElo(EloKey, elo, eloConfidenceLevel);

        return acc;
    }
}