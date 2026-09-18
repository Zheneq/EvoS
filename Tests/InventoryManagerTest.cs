using EvoS.DirectoryServer.Inventory;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class InventoryManagerTest : EvosTest
{
    public InventoryManagerTest(ITestOutputHelper output) : base(output)
    {
    }

    public static IEnumerable<object[]> UnlockLists()
    {
        const long accountId = 1;
        yield return new object[] { "UnlockedBannerIDs", InventoryManager.GetUnlockedBannerIDs(accountId) };
        yield return new object[] { "DefaultUnlockedBannerIDs", InventoryManager.GetDefaultUnlockedBannerIDs(accountId) };
        yield return new object[] { "UnlockedEmojiIDs", InventoryManager.GetUnlockedEmojiIDs(accountId) };
        yield return new object[] { "UnlockedLoadingScreenBackgroundIds", InventoryManager.GetUnlockedLoadingScreenBackgroundIds(accountId) };
        yield return new object[] { "UnlockedOverconIDs", InventoryManager.GetUnlockedOverconIDs(accountId) };
        yield return new object[] { "UnlockedTitleIDs", InventoryManager.GetUnlockedTitleIDs(accountId) };
        yield return new object[] { "UnlockedRibbonIDs", InventoryManager.GetUnlockedRibbonIDs(accountId) };
    }

    [Theory]
    [MemberData(nameof(UnlockLists))]
    public void UnlockListsContainNoDuplicates(string name, List<int> ids)
    {
        List<int> duplicates = ids.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.True(duplicates.Count == 0, $"{name} contains duplicate ids: {string.Join(", ", duplicates)}");
    }
}
