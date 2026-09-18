using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Account;
using CentralServer.LobbyServer.Session;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class AccountModuleTest : EvosTest
{
    public AccountModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Minimal IHandlerRegistry that lets tests dispatch messages directly into a module.
    /// </summary>
    private sealed class CapturingRegistry : IHandlerRegistry
    {
        private readonly Dictionary<Type, Action<WebSocketMessage>> _handlers = new();

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            _handlers[typeof(T)] = msg => handler((T)msg);
        }

        public void Dispatch<T>(T message) where T : WebSocketMessage
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                handler(message);
            else
                throw new InvalidOperationException($"No handler registered for {typeof(T).Name}");
        }
    }

    private static (AccountModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new AccountModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    // Use unique per-test AccountIds (> 3_000_000 to avoid collision with StoreModule/TelemetryModule ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 3_000_000L;

    private static PersistedAccountData MakeAccount(long accountId)
    {
        var account = new PersistedAccountData
        {
            AccountId = accountId,
            AccountComponent = new AccountComponent(),
            BankComponent = new BankComponent(new List<CurrencyData>()),
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>(),
        };
        DB.Get().AccountDao.CreateAccount(account);
        return account;
    }

    private static PersistedAccountData GetAccount(long accountId)
        => DB.Get().AccountDao.GetAccount(accountId);

    // --- 1. SelectTitleRequest with an unlocked title id ---

    [Fact]
    public void SelectTitle_UnlockedId_PersistsAndUpdatesVisuals()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        PersistedAccountData account = GetAccount(accountId);
        const int titleId = 5; // put in unlocked list
        account.AccountComponent.UnlockedTitleIDs.Add(titleId);

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new SelectTitleRequest { TitleID = titleId, RequestId = 1 });

        Assert.Equal(1, conn.VisualsUpdates);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SelectTitleResponse>(conn.Sent[0]);
        Assert.Equal(titleId, response.CurrentTitleID);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(titleId, updated.AccountComponent.SelectedTitleID);
    }

    // --- 2. SelectTitleRequest with a locked id ---

    [Fact]
    public void SelectTitle_LockedId_NoVisualsUpdateResponseCarriesOldId()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        PersistedAccountData account = GetAccount(accountId);
        const int lockedId = 999; // not in unlocked list
        // ensure old title is something known
        account.AccountComponent.SelectedTitleID = -1;

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new SelectTitleRequest { TitleID = lockedId, RequestId = 2 });

        // existing behavior: response is still sent even when locked
        Assert.Equal(0, conn.VisualsUpdates);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SelectTitleResponse>(conn.Sent[0]);
        // response carries the old (unchanged) id
        Assert.Equal(-1, response.CurrentTitleID);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(-1, updated.AccountComponent.SelectedTitleID);
    }

    // --- 3. SelectRibbonRequest with a locked id ---

    [Fact]
    public void SelectRibbon_LockedId_FailureResponseNoVisualsUpdate()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        // don't add ribbon 999 to unlocked list
        const int lockedRibbonId = 999;

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new SelectRibbonRequest { RibbonID = lockedRibbonId, RequestId = 3 });

        Assert.Equal(0, conn.VisualsUpdates);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SelectRibbonResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // --- 4. SelectBannerRequest: foreground vs background routing ---

    [Fact]
    public void SelectBanner_Foreground_SetsForegroundBannerID()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);

        // pick one foreground and one background id from InventoryManager (don't hardcode)
        // BannerIsForeground returns true for fg ids
        int fgId = -1;
        int bgId = -1;
        for (int id = 62; id <= 500 && (fgId == -1 || bgId == -1); id++)
        {
            if (fgId == -1 && InventoryManager.BannerIsForeground(id))
                fgId = id;
            else if (bgId == -1 && !InventoryManager.BannerIsForeground(id))
                bgId = id;
        }
        Assert.True(fgId != -1, "Could not find a foreground banner id");
        Assert.True(bgId != -1, "Could not find a background banner id");

        var (_, conn, registry) = MakeModule(accountId);

        // Test foreground
        registry.Dispatch(new SelectBannerRequest { BannerID = fgId, RequestId = 4 });

        Assert.Equal(1, conn.VisualsUpdates);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SelectBannerResponse>(conn.Sent[0]);
        Assert.Equal(fgId, response.ForegroundBannerID);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(fgId, updated.AccountComponent.SelectedForegroundBannerID);
    }

    [Fact]
    public void SelectBanner_Background_SetsBackgroundBannerID()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);

        int bgId = -1;
        for (int id = 62; id <= 500 && bgId == -1; id++)
        {
            if (!InventoryManager.BannerIsForeground(id))
                bgId = id;
        }
        Assert.True(bgId != -1, "Could not find a background banner id");

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new SelectBannerRequest { BannerID = bgId, RequestId = 5 });

        Assert.Equal(1, conn.VisualsUpdates);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SelectBannerResponse>(conn.Sent[0]);
        Assert.Equal(bgId, response.BackgroundBannerID);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(bgId, updated.AccountComponent.SelectedBackgroundBannerID);
    }

    // --- 5. SetDevTagRequest for a non-dev account ---

    [Fact]
    public void SetDevTag_NonDevAccount_ReturnsFalse()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        // account has no dev entitlement by default

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new SetDevTagRequest { active = true, RequestId = 6 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<SetDevTagResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // --- 6. LoadingScreenToggleRequest ---

    [Fact]
    public void LoadingScreenToggle_KnownId_FlipsStateAndReturnsSuccess()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        PersistedAccountData account = GetAccount(accountId);
        const int screenId = 7;
        account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState[screenId] = false;

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new LoadingScreenToggleRequest { LoadingScreenId = screenId, NewState = true, RequestId = 7 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<LoadingScreenToggleResponse>(conn.Sent[0]);
        Assert.True(response.Success);
        Assert.True(response.CurrentState);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.True(updated.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState[screenId]);
    }

    [Fact]
    public void LoadingScreenToggle_UnknownId_ReturnsFalse()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        // don't add loading screen 9999

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new LoadingScreenToggleRequest { LoadingScreenId = 9999, NewState = true, RequestId = 8 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<LoadingScreenToggleResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // --- 7. UpdateUIStateRequest ---

    [Fact]
    public void UpdateUIState_PersistsValue()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);

        var (_, conn, registry) = MakeModule(accountId);
        var key = AccountComponent.UIStateIdentifier.HasResetMods;

        registry.Dispatch(new UpdateUIStateRequest { UIState = key, StateValue = 42, RequestId = 9 });

        // UpdateUIState sends no response message
        Assert.Empty(conn.Sent);

        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(42, updated.AccountComponent.UIStates[key]);
    }

    // --- 8. PlayerMatchDataRequest ---

    [Fact]
    public void PlayerMatchData_SendsResponseWithMatchingResponseId()
    {
        long accountId = UniqueId();
        // no need to create an account; MatchHistoryMockDao returns empty list for any id

        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PlayerMatchDataRequest { RequestId = 10 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PlayerMatchDataResponse>(conn.Sent[0]);
        Assert.Equal(10, response.ResponseId);
        Assert.NotNull(response.MatchData);
    }

    // --- 9. Stubs ---

    [Fact]
    public void CheckRAFStatus_SendsExpectedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var ex = Record.Exception(() =>
            registry.Dispatch(new CheckRAFStatusRequest { RequestId = 11 }));

        Assert.Null(ex);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<CheckRAFStatusResponse>(conn.Sent[0]);
        Assert.Equal(11, response.ResponseId);
    }

    [Fact]
    public void SendRAFReferralEmails_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var ex = Record.Exception(() =>
            registry.Dispatch(new SendRAFReferralEmailsRequest { RequestId = 12 }));

        Assert.Null(ex);
        Assert.Single(conn.Sent);
        var response = Assert.IsType<SendRAFReferralEmailsResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    [Fact]
    public void SetRegion_DoesNotThrow()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var ex = Record.Exception(() =>
            registry.Dispatch(new SetRegionRequest { RequestId = 13 }));

        Assert.Null(ex);
        Assert.Empty(conn.Sent);
    }
}
