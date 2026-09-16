using EvoS.Framework;

namespace Tests;

public class EvosConfigurationTest
{
    [Fact]
    public void ValidateConfiguration_DevModeWithPersistentDb_Throws()
    {
        Assert.Throws<EvosException>(() =>
            EvosConfiguration.ValidateConfiguration(devMode: true, dbType: EvosConfiguration.DBType.Mongo));
    }

    [Theory]
    [InlineData(false, EvosConfiguration.DBType.None)]
    [InlineData(false, EvosConfiguration.DBType.Mongo)] // production: real DB, DevMode off
    [InlineData(true, EvosConfiguration.DBType.None)]   // local dev: DevMode on, in-memory only
    public void ValidateConfiguration_AllowedCombinations_DoNotThrow(bool devMode, EvosConfiguration.DBType dbType)
    {
        EvosConfiguration.ValidateConfiguration(devMode, dbType);
    }

    [Theory]
    [InlineData("")]    // disabled feature - valid
    [InlineData(null)]  // disabled feature - valid
    public void ValidateApiKeyStrength_EmptyKey_IsAllowed(string key)
    {
        EvosConfiguration.ValidateApiKeyStrength("TestKey", key);
    }

    [Fact]
    public void ValidateApiKeyStrength_ShortKey_Throws()
    {
        Assert.Throws<EvosException>(() =>
            EvosConfiguration.ValidateApiKeyStrength("TestKey", new string('x', EvosConfiguration.MinApiKeyLength - 1)));
    }

    [Theory]
    [InlineData(EvosConfiguration.MinApiKeyLength)]      // exactly at the floor
    [InlineData(EvosConfiguration.MinApiKeyLength + 20)] // comfortably above
    public void ValidateApiKeyStrength_StrongKey_DoesNotThrow(int length)
    {
        EvosConfiguration.ValidateApiKeyStrength("TestKey", new string('x', length));
    }
}
