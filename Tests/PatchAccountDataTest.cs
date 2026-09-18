using EvoS.DirectoryServer.Account;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class PatchAccountDataTest : EvosTest
{
    private const int DeveloperTitleId = 26;

    public PatchAccountDataTest(ITestOutputHelper output) : base(output)
    {
    }

    private static PersistedAccountData MakePatchedAccount()
    {
        PersistedAccountData account = AccountManager.CreateAccount(1234, "patchtest");
        EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account);
        return account;
    }

    [Fact]
    public void PatchingIsIdempotent()
    {
        PersistedAccountData account = MakePatchedAccount();
        Assert.False(EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account));
    }

    [Fact]
    public void PatchingReportsChangeAndRestoresPlaceholderCharacters()
    {
        PersistedAccountData account = MakePatchedAccount();

        account.CharacterData.Remove(CharacterType.PendingWillFill);

        Assert.True(EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account));
        Assert.True(account.CharacterData.ContainsKey(CharacterType.PendingWillFill));
        Assert.NotEmpty(account.CharacterData[CharacterType.PendingWillFill].CharacterComponent.CharacterLoadouts);
    }

    [Fact]
    public void PatchingReportsChangeWhenLoadoutsAreMissing()
    {
        PersistedAccountData account = MakePatchedAccount();

        account.CharacterData[CharacterType.TestFreelancer1].CharacterComponent.CharacterLoadouts.Clear();

        Assert.True(EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account));
        Assert.NotEmpty(account.CharacterData[CharacterType.TestFreelancer1].CharacterComponent.CharacterLoadouts);
    }

    [Fact]
    public void PatchingHealsDuplicatedUnlockIds()
    {
        PersistedAccountData account = MakePatchedAccount();

        account.AccountComponent.UnlockedTitleIDs.Add(DeveloperTitleId);
        account.AccountComponent.UnlockedTitleIDs.Add(DeveloperTitleId);
        account.AccountComponent.UnlockedBannerIDs.Add(account.AccountComponent.UnlockedBannerIDs[0]);
        account.AccountComponent.UnlockedOverconIDs.Add(account.AccountComponent.UnlockedOverconIDs[0]);

        Assert.True(EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account));

        Assert.Equal(account.AccountComponent.UnlockedTitleIDs.Count, account.AccountComponent.UnlockedTitleIDs.Distinct().Count());
        Assert.Equal(account.AccountComponent.UnlockedBannerIDs.Count, account.AccountComponent.UnlockedBannerIDs.Distinct().Count());
        Assert.Equal(account.AccountComponent.UnlockedOverconIDs.Count, account.AccountComponent.UnlockedOverconIDs.Distinct().Count());

        Assert.False(EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account));
    }

    [Fact]
    public void DeveloperTitleIsNotDuplicated()
    {
        PersistedAccountData account = MakePatchedAccount();

        account.AccountComponent.SetIsDev(true);
        EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account);
        EvoS.DirectoryServer.DirectoryServer.PatchAccountData(account);

        Assert.Equal(1, account.AccountComponent.UnlockedTitleIDs.Count(t => t == DeveloperTitleId));
    }
}
