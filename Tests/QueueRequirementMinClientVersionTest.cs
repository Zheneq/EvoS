using System.Runtime.CompilerServices;
using EvoS.Framework.Network;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;

namespace Tests;

public class QueueRequirementMinClientVersionTest
{
    // RequirementCollection type IDs in the client, read from a 1.8 client build
    private static readonly Dictionary<Type, byte> ClientTypeIds = new()
    {
        { typeof(QueueRequirement_TimeOfWeek), 2 },
        { typeof(QueueRequirement_Never), 5 },
        { typeof(QueueRequirement_MaxLeavingPoints), 8 },
        { typeof(QueueRequirement_GreaterThan), 11 },
        { typeof(QueueRequirement_Environement), 12 },
        { typeof(QueueRequirement_DateTime), 14 },
        { typeof(QueueRequirement_Character), 16 },
        { typeof(QueueRequirement_AccessLevel), 18 },
        { typeof(QueueRequirement_MinClientVersion), 20 },
    };

    [Fact]
    public void TypeIdsMatchClient()
    {
        Dictionary<Type, byte> typeIds = QueueRequirement.MessageTypes.ToDictionary(
            type => type,
            type => Serialize((QueueRequirement)RuntimeHelpers.GetUninitializedObject(type))[0]);

        Assert.Equal(ClientTypeIds, typeIds);
    }

    [Fact]
    public void SerializesLikeClient()
    {
        // Type ID, then m_major, m_minor, m_patch as zigzag varints
        Assert.Equal([20, 2, 16, 0], Serialize(new QueueRequirement_MinClientVersion(1, 8, 0)));
    }

    [Fact]
    public void BinaryRoundTrip()
    {
        RequirementCollection collection = RequirementCollection.Create([new QueueRequirement_MinClientVersion(1, 8, 2)]);

        Assert.Equal("[{\"MinClientVersion\":{\"Version\":\"1.8.2\"}}]", JsonConvert.SerializeObject(collection));
    }

    [Theory]
    [InlineData("1.8", "1.8.0")]
    [InlineData("1.8.2", "1.8.2")]
    public void JsonRoundTrip(string version, string expected)
    {
        RequirementCollection collection = JsonConvert.DeserializeObject<RequirementCollection>(
            $"[{{\"MinClientVersion\":{{\"Version\":\"{version}\"}}}}]");

        QueueRequirement requirement = Assert.Single(collection);
        Assert.IsType<QueueRequirement_MinClientVersion>(requirement);
        Assert.Equal($"[{{\"MinClientVersion\":{{\"Version\":\"{expected}\"}}}}]", JsonConvert.SerializeObject(collection));
    }

    [Fact]
    public void OnlySentTo18Clients()
    {
        QueueRequirement_MinClientVersion requirement = new QueueRequirement_MinClientVersion(1, 8, 0);

        Assert.True(requirement.IsSupportedBy(ProtocolVersion.V1_8));
        Assert.Contains(ProtocolVersion.V1_8, ProtocolVersion.SUPPORTED_PROTO_VERSIONS);
        Assert.False(requirement.IsSupportedBy(ProtocolVersion.VANILLA));
        Assert.False(requirement.IsSupportedBy("89945cd84ac637ac678c60dd840acbda")); // 1.5
    }

    private static byte[] Serialize(QueueRequirement requirement)
    {
        return RequirementCollection.Create([requirement]).RequirementsAsBinaryData.Single();
    }
}
