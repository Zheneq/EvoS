using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;

namespace Tests;

public class AsymmetricSubTypeTest
{
    [Fact]
    public void RequirementsAreReadFromConfig()
    {
        List<AsymmetricSubTypeConfig>? configs = JsonConvert.DeserializeObject<List<AsymmetricSubTypeConfig>>(
            "[{\"LocalizedName\":\"Test\",\"BaseSubTypeName\":\"Base\",\"NumControlledCharacters\":2,"
            + "\"Requirements\":[{\"MinClientVersion\":{\"Version\":\"1.8\"}}]},"
            + "{\"LocalizedName\":\"NoRequirements\",\"BaseSubTypeName\":\"Base\",\"NumControlledCharacters\":2}]");

        Assert.NotNull(configs);
        Assert.Equal(2, configs.Count);
        QueueRequirement requirement = Assert.Single(configs[0].Requirements);
        Assert.IsType<QueueRequirement_MinClientVersion>(requirement);
        Assert.Null(configs[1].Requirements);
    }

    [Fact]
    public void AdvertisedSubTypeCombinesBaseAndOwnRequirements()
    {
        GameSubType baseSubType = new GameSubType
        {
            LocalizedName = "Base",
            GameMapConfigs = [],
            Requirements = RequirementCollection.Create([new QueueRequirement_MinClientVersion(1, 7, 0)])
        };
        AsymmetricSubTypeDescriptor descriptor = new AsymmetricSubTypeDescriptor
        {
            LocalizedName = "Test",
            Requirements = RequirementCollection.Create([new QueueRequirement_MinClientVersion(1, 8, 0)])
        };

        GameSubType advertised = descriptor.CreateAdvertisedSubType(baseSubType);

        Assert.Equal(
            "[{\"MinClientVersion\":{\"Version\":\"1.7.0\"}},"
            + "{\"MinClientVersion\":{\"Version\":\"1.8.0\"}},"
            + "{\"AccessLevel\":{\"AccessLevel\":\"VIP\",\"AnyGroupMember\":\"False\"}}]",
            JsonConvert.SerializeObject(advertised.Requirements));
        Assert.Single(baseSubType.Requirements);
    }

    [Fact]
    public void AdvertisedSubTypeWithoutOwnRequirements()
    {
        GameSubType baseSubType = new GameSubType { LocalizedName = "Base", GameMapConfigs = [] };
        AsymmetricSubTypeDescriptor descriptor = new AsymmetricSubTypeDescriptor { LocalizedName = "Test" };

        GameSubType advertised = descriptor.CreateAdvertisedSubType(baseSubType);

        QueueRequirement requirement = Assert.Single(advertised.Requirements);
        Assert.IsType<QueueRequirement_AccessLevel>(requirement);
    }
}
