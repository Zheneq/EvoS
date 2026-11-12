using EvoS.Framework.Network.Static;
using Newtonsoft.Json;

namespace Tests;

public class GameSubTypeJsonTest
{
    [Fact]
    public void RequirementsAreReadFromConfig()
    {
        GameSubType? subType = JsonConvert.DeserializeObject<GameSubType>(
            "{\"LocalizedName\":\"Test\",\"Requirements\":[{\"MinClientVersion\":{\"Version\":\"1.8\"}}]}");

        Assert.NotNull(subType);
        QueueRequirement requirement = Assert.Single(subType.Requirements);
        Assert.IsType<QueueRequirement_MinClientVersion>(requirement);
        Assert.Equal("[{\"MinClientVersion\":{\"Version\":\"1.8.0\"}}]", JsonConvert.SerializeObject(subType.Requirements));
    }

    [Fact]
    public void RequirementsAreNotWritten()
    {
        GameSubType subType = new GameSubType
        {
            LocalizedName = "Test",
            Requirements = RequirementCollection.Create([new QueueRequirement_MinClientVersion(1, 8, 0)])
        };

        Assert.DoesNotContain("Requirements", JsonConvert.SerializeObject(subType));
    }
}
