using EvoS.Framework.Network;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;

namespace Tests;

public class QueueRequirementProtocolDependentTest
{
    private const string ModdedProtocolVersion = "89945cd84ac637ac678c60dd840acbda";

    private class TestRequirement : QueueRequirement_ProtocolDependent
    {
        public override IReadOnlySet<string> SupportedProtocolVersions { get; } =
            new HashSet<string> { ModdedProtocolVersion };

        public override RequirementType Requirement => RequirementType.AccessLevel;
        public override bool AnyGroupMember => false;

        public override void WriteToJson(JsonWriter writer)
        {
        }
    }

    [Fact]
    public void SupportedClient_KeepsRequirement()
    {
        TestRequirement requirement = new TestRequirement();

        Assert.True(requirement.IsSupportedBy(ModdedProtocolVersion));
        Assert.Same(requirement, requirement.ForProtocolVersion(ModdedProtocolVersion));
    }

    [Theory]
    [InlineData(ProtocolVersion.VANILLA)]
    [InlineData(null)]
    public void UnsupportedClient_GetsAdminDisabled(string protocolVersion)
    {
        TestRequirement requirement = new TestRequirement();

        Assert.False(requirement.IsSupportedBy(protocolVersion));
        QueueRequirement replacement = requirement.ForProtocolVersion(protocolVersion);
        Assert.IsType<QueueRequirement_Never>(replacement);
        Assert.Equal(QueueRequirement.RequirementType.AdminDisabled, replacement.Requirement);
    }

    [Fact]
    public void CollectionWithoutProtocolDependentRequirements_IsReturnedAsIs()
    {
        RequirementCollection collection = RequirementCollection.Create();
        collection.Add(new QueueRequirement_AccessLevel { AccessLevel = ClientAccessLevel.VIP });

        Assert.Same(collection, collection.ForProtocolVersion(ProtocolVersion.VANILLA));
        Assert.Same(collection, collection.ForProtocolVersion(null));
    }
}
