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
}
