using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Store;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class StoreModuleTest : EvosTest
{
    public StoreModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// A minimal IHandlerRegistry that stores delegates for each registered type so tests
    /// can dispatch requests directly into a StoreModule under test.
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

    private static (StoreModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new StoreModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static PersistedAccountData MakeAccount(long accountId, int freecurrencyAmount = 0)
    {
        var account = new PersistedAccountData
        {
            AccountId = accountId,
            AccountComponent = new AccountComponent(),
            BankComponent = new BankComponent(new List<CurrencyData>()),
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>(),
        };

        if (freecurrencyAmount > 0)
        {
            account.BankComponent.CurrentAmounts.SetValue(new CurrencyData
            {
                Type = CurrencyType.FreelancerCurrency,
                Amount = freecurrencyAmount,
            });
        }

        DB.Get().AccountDao.CreateAccount(account);
        return account;
    }

    private static PersistedAccountData GetAccount(long accountId)
    {
        return DB.Get().AccountDao.GetAccount(accountId);
    }

    // Use unique per-test AccountIds to avoid cross-test interference
    // (mock DAO state is process-global; tests run in parallel).
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 1_000_000L;

    // --- Banner emblem (foreground) ---

    [Fact]
    public void EmblemPurchase_SufficientFunds_UnlocksAndDeducts()
    {
        const int bannerId = 376; // cost = 300 from InventoryManager
        long accountId = UniqueId();
        int cost = InventoryManager.GetBannerCost(bannerId);
        MakeAccount(accountId, freecurrencyAmount: cost + 100);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseBannerForegroundRequest
        {
            BannerForegroundId = bannerId,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 1
        });

        // Exactly 2 messages: success response + account update notification
        Assert.Equal(2, conn.Sent.Count);
        var purchase = Assert.IsType<PurchaseBannerForegroundResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Success, purchase.Result);
        Assert.Equal(bannerId, purchase.BannerForegroundId);
        Assert.IsType<PlayerAccountDataUpdateNotification>(conn.Sent[1]);

        // Banner added, balance reduced
        PersistedAccountData updated = GetAccount(accountId);
        Assert.Contains(bannerId, updated.AccountComponent.UnlockedBannerIDs);
        Assert.Equal(100, updated.BankComponent.CurrentAmounts.GetCurrentAmount(CurrencyType.FreelancerCurrency));
    }

    [Fact]
    public void EmblemPurchase_InsufficientFunds_FailedResponseOnly()
    {
        const int bannerId = 376; // cost = 300
        long accountId = UniqueId();
        int cost = InventoryManager.GetBannerCost(bannerId);
        MakeAccount(accountId, freecurrencyAmount: cost - 1); // one short
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseBannerForegroundRequest
        {
            BannerForegroundId = bannerId,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 2
        });

        // Only 1 message: failed response
        Assert.Single(conn.Sent);
        var purchase = Assert.IsType<PurchaseBannerForegroundResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Failed, purchase.Result);

        // No banner added, no deduction
        PersistedAccountData updated = GetAccount(accountId);
        Assert.DoesNotContain(bannerId, updated.AccountComponent.UnlockedBannerIDs);
        Assert.Equal(cost - 1, updated.BankComponent.CurrentAmounts.GetCurrentAmount(CurrencyType.FreelancerCurrency));
    }

    // --- Loadout slot purchase ---

    [Fact]
    public void LoadoutSlotPurchase_BelowCap_AddsLoadoutAndSendsResponses()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        // Add character data with 1 loadout
        PersistedAccountData account = GetAccount(accountId);
        var charData = new PersistedCharacterData(CharacterType.Scoundrel);
        charData.CharacterComponent.CharacterLoadouts.Add(
            new CharacterLoadout(new CharacterModInfo(), new CharacterAbilityVfxSwapInfo(), "Loadout 0", ModStrictness.AllModes));
        account.CharacterData[CharacterType.Scoundrel] = charData;
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseLoadoutSlotRequest
        {
            Character = CharacterType.Scoundrel,
            RequestId = 3
        });

        // 3 messages: PurchaseLoadoutSlotResponse (Success), PlayerCharacterDataUpdateNotification, PlayerAccountDataUpdateNotification
        Assert.Equal(3, conn.Sent.Count);
        var purchase = Assert.IsType<PurchaseLoadoutSlotResponse>(conn.Sent[0]);
        Assert.True(purchase.Success);
        Assert.IsType<PlayerCharacterDataUpdateNotification>(conn.Sent[1]);
        Assert.IsType<PlayerAccountDataUpdateNotification>(conn.Sent[2]);

        // Loadout was added
        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(2, updated.CharacterData[CharacterType.Scoundrel].CharacterComponent.CharacterLoadouts.Count);
    }

    [Fact]
    public void LoadoutSlotPurchase_AtCap_FailedResponseOnly()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        // Add character data with exactly 10 loadouts
        PersistedAccountData account = GetAccount(accountId);
        var charData = new PersistedCharacterData(CharacterType.Scoundrel);
        for (int i = 0; i < 10; i++)
        {
            charData.CharacterComponent.CharacterLoadouts.Add(
                new CharacterLoadout(new CharacterModInfo(), new CharacterAbilityVfxSwapInfo(), $"Loadout {i}", ModStrictness.AllModes));
        }
        account.CharacterData[CharacterType.Scoundrel] = charData;
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseLoadoutSlotRequest
        {
            Character = CharacterType.Scoundrel,
            RequestId = 4
        });

        // Only 1 message: failed response
        Assert.Single(conn.Sent);
        var purchase = Assert.IsType<PurchaseLoadoutSlotResponse>(conn.Sent[0]);
        Assert.False(purchase.Success);

        // No loadout added
        PersistedAccountData updated = GetAccount(accountId);
        Assert.Equal(10, updated.CharacterData[CharacterType.Scoundrel].CharacterComponent.CharacterLoadouts.Count);
    }

    // --- Stub purchases ---

    [Fact]
    public void PurchaseMod_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseModRequest
        {
            Character = CharacterType.Scoundrel,
            UnlockData = new PlayerModData(),
            RequestId = 5
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PurchaseModResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    [Fact]
    public void PurchaseTitle_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseTitleRequest
        {
            TitleId = 1,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 6
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PurchaseTitleResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Failed, response.Result);
    }

    [Fact]
    public void PurchaseTaunt_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseTauntRequest
        {
            CharacterType = CharacterType.Scoundrel,
            TauntId = 1,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 7
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PurchaseTauntResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Failed, response.Result);
    }

    [Fact]
    public void PurchaseChatEmoji_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseChatEmojiRequest
        {
            EmojiID = 1,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 8
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PurchaseChatEmojiResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Failed, response.Result);
    }

    [Fact]
    public void PurchaseInventoryItem_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new PurchaseInventoryItemRequest
        {
            InventoryItemID = 1,
            CurrencyType = CurrencyType.FreelancerCurrency,
            RequestId = 9
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PurchaseInventoryItemResponse>(conn.Sent[0]);
        Assert.Equal(PurchaseResult.Failed, response.Result);
    }
}
