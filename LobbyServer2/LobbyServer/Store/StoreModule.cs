using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Session;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;

namespace CentralServer.LobbyServer.Store;

public class StoreModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(StoreModule));
    private readonly IClientConnection _conn;

    public StoreModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<PricesRequest>(HandlePricesRequest);
        registry.Register<PurchaseTintRequest>(HandlePurchaseTintRequest);
        registry.Register<PurchaseModRequest>(HandlePurchaseModRequest);
        registry.Register<PurchaseTitleRequest>(HandlePurchaseTitleRequest);
        registry.Register<PurchaseTauntRequest>(HandlePurchaseTauntRequest);
        registry.Register<PurchaseChatEmojiRequest>(HandlePurchaseChatEmojiRequest);
        registry.Register<PurchaseLoadoutSlotRequest>(HandlePurchaseLoadoutSlotRequest);
        registry.Register<PaymentMethodsRequest>(HandlePaymentMethodsRequest);
        registry.Register<StoreOpenedMessage>(HandleStoreOpenedMessage);
        registry.Register<PurchaseBannerForegroundRequest>(HandlePurchaseEmblemRequest);
        registry.Register<PurchaseBannerBackgroundRequest>(HandlePurchaseBannerRequest);
        registry.Register<PurchaseAbilityVfxRequest>(HandlePurchaseAbilityVfx);
        registry.Register<PurchaseInventoryItemRequest>(HandlePurchaseInventoryItemRequest);
    }

    private void HandlePricesRequest(PricesRequest request)
    {
        PricesResponse response = StoreManager.GetPricesResponse();
        response.ResponseId = request.RequestId;
        _conn.Send(response);
    }

    private void HandlePurchaseTintRequest(PurchaseTintRequest request)
    {
        log.Info("PurchaseTintRequest " + JsonConvert.SerializeObject(request));

        SkinHelper sk = new SkinHelper();
        sk.AddSkin(request.CharacterType, request.SkinId, request.TextureId, request.TintId);
        sk.Save();

        PurchaseTintResponse response = new PurchaseTintResponse
        {
            Result = PurchaseResult.Success,
            CurrencyType = request.CurrencyType,
            CharacterType = request.CharacterType,
            SkinId = request.SkinId,
            TextureId = request.TextureId,
            TintId = request.TintId,
            ResponseId = request.RequestId
        };
        _conn.Send(response);
    }

    private void HandlePurchaseEmblemRequest(PurchaseBannerForegroundRequest request)
    {
        //Get the users account
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        // Never trust the client double check plus we need this info to deduct it from account
        int cost = InventoryManager.GetBannerCost(request.BannerForegroundId);

        log.Info($"Player {_conn.AccountId} trying to purchase emblem {request.BannerForegroundId} with {request.CurrencyType} for the price {cost}");

        if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
        {
            PurchaseBannerForegroundResponse failedResponse = new PurchaseBannerForegroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                BannerForegroundId = request.BannerForegroundId
            };

            _conn.Send(failedResponse);

            return;
        }

        account.AccountComponent.UnlockedBannerIDs.Add(request.BannerForegroundId);

        account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase emblem");

        DB.Get().AccountDao.UpdateBankComponent(account);
        DB.Get().AccountDao.UpdateAccountComponent(account);

        PurchaseBannerForegroundResponse response = new PurchaseBannerForegroundResponse()
        {
            ResponseId = request.RequestId,
            Result = PurchaseResult.Success,
            CurrencyType = request.CurrencyType,
            BannerForegroundId = request.BannerForegroundId
        };

        _conn.Send(response);

        //Update account curency
        _conn.Send(new PlayerAccountDataUpdateNotification(account));

    }

    private void HandlePurchaseBannerRequest(PurchaseBannerBackgroundRequest request)
    {
        //Get the users account
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        // Never trust the client double check plus we need this info to deduct it from account
        int cost = InventoryManager.GetBannerCost(request.BannerBackgroundId);

        log.Info($"Player {_conn.AccountId} trying to purchase banner {request.BannerBackgroundId} with {request.CurrencyType} for the price {cost}");

        if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
        {
            PurchaseBannerBackgroundResponse failedResponse = new PurchaseBannerBackgroundResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                BannerBackgroundId = request.BannerBackgroundId
            };

            _conn.Send(failedResponse);

            return;
        }

        account.AccountComponent.UnlockedBannerIDs.Add(request.BannerBackgroundId);
        account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase banner");

        DB.Get().AccountDao.UpdateBankComponent(account);
        DB.Get().AccountDao.UpdateAccountComponent(account);

        PurchaseBannerBackgroundResponse response = new PurchaseBannerBackgroundResponse()
        {
            ResponseId = request.RequestId,
            Result = PurchaseResult.Success,
            CurrencyType = request.CurrencyType,
            BannerBackgroundId = request.BannerBackgroundId
        };

        _conn.Send(response);

        //Update account curency
        _conn.Send(new PlayerAccountDataUpdateNotification(account));
    }

    private void HandlePurchaseAbilityVfx(PurchaseAbilityVfxRequest request)
    {
        //Get the users account
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

        // Never trust the client double check plus we need this info to deduct it from account
        int cost = InventoryManager.GetVfxCost(request.VfxId, request.AbilityId);

        log.Info($"Player {_conn.AccountId} trying to purchase vfx {request.VfxId} with {request.CurrencyType} for character {request.CharacterType} and ability {request.AbilityId} for price {cost}");

        if (account.BankComponent.CurrentAmounts.GetCurrentAmount(request.CurrencyType) < cost)
        {
            PurchaseAbilityVfxResponse failedResponse = new PurchaseAbilityVfxResponse()
            {
                ResponseId = request.RequestId,
                Result = PurchaseResult.Failed,
                CurrencyType = request.CurrencyType,
                CharacterType = request.CharacterType,
                AbilityId = request.AbilityId,
                VfxId = request.VfxId
            };

            _conn.Send(failedResponse);

            return;
        }

        PlayerAbilityVfxSwapData abilityVfxSwapData = new PlayerAbilityVfxSwapData()
        {
            AbilityId = request.AbilityId,
            AbilityVfxSwapID = request.VfxId
        };

        account.CharacterData[request.CharacterType].CharacterComponent.AbilityVfxSwaps.Add(abilityVfxSwapData);
        account.BankComponent.ChangeValue(request.CurrencyType, -cost, $"Purchase vfx");

        DB.Get().AccountDao.UpdateBankComponent(account);
        DB.Get().AccountDao.UpdateCharacterComponent(account, request.CharacterType);

        PurchaseAbilityVfxResponse response = new PurchaseAbilityVfxResponse()
        {
            ResponseId = request.RequestId,
            Result = PurchaseResult.Success,
            CurrencyType = request.CurrencyType,
            CharacterType = request.CharacterType,
            AbilityId = request.AbilityId,
            VfxId = request.VfxId
        };

        _conn.Send(response);

        // Update character
        _conn.Send(new PlayerCharacterDataUpdateNotification()
        {
            CharacterData = account.CharacterData[request.CharacterType],
        });

        //Update account curency
        _conn.Send(new PlayerAccountDataUpdateNotification(account));
    }

    private void HandlePurchaseInventoryItemRequest(PurchaseInventoryItemRequest request)
    {
        _conn.Send(new PurchaseInventoryItemResponse
        {
            Result = PurchaseResult.Failed,
            InventoryItemID = request.InventoryItemID,
            CurrencyType = request.CurrencyType,
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePurchaseModRequest(PurchaseModRequest request)
    {
        _conn.Send(new PurchaseModResponse
        {
            Character = request.Character,
            UnlockData = request.UnlockData,
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePurchaseTitleRequest(PurchaseTitleRequest request)
    {
        _conn.Send(new PurchaseTitleResponse
        {
            Result = PurchaseResult.Failed,
            CurrencyType = request.CurrencyType,
            TitleId = request.TitleId,
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePurchaseTauntRequest(PurchaseTauntRequest request)
    {
        _conn.Send(new PurchaseTauntResponse
        {
            Result = PurchaseResult.Failed,
            CurrencyType = request.CurrencyType,
            CharacterType = request.CharacterType,
            TauntId = request.TauntId,
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePurchaseChatEmojiRequest(PurchaseChatEmojiRequest request)
    {
        _conn.Send(new PurchaseChatEmojiResponse
        {
            Result = PurchaseResult.Failed,
            CurrencyType = request.CurrencyType,
            EmojiID = request.EmojiID,
            Success = false,
            ResponseId = request.RequestId
        });
    }

    private void HandlePurchaseLoadoutSlotRequest(PurchaseLoadoutSlotRequest request)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        if (account == null
            || !account.CharacterData.TryGetValue(request.Character, out PersistedCharacterData characterData)
            || characterData.CharacterComponent.CharacterLoadouts.Count >= 10) // hardcoded on the client side too
        {
            _conn.Send(new PurchaseLoadoutSlotResponse
            {
                Character = request.Character,
                Success = false,
                ResponseId = request.RequestId
            });
            return;
        }

        List<CharacterLoadout> loadouts = characterData.CharacterComponent.CharacterLoadouts;
        loadouts.Add(new CharacterLoadout(
            new CharacterModInfo(),
            new CharacterAbilityVfxSwapInfo(),
            $"Loadout {loadouts.Count}",
            ModStrictness.AllModes));

        // DB.Get().AccountDao.UpdateBankComponent(account);
        DB.Get().AccountDao.UpdateCharacterComponent(account, request.Character);

        _conn.Send(new PurchaseLoadoutSlotResponse
        {
            Character = request.Character,
            Success = true,
            ResponseId = request.RequestId
        });
        _conn.Send(new PlayerCharacterDataUpdateNotification
        {
            CharacterData = account.CharacterData[request.Character],
        });
        _conn.Send(new PlayerAccountDataUpdateNotification(account));
    }

    private void HandlePaymentMethodsRequest(PaymentMethodsRequest request)
    {
    }

    private void HandleStoreOpenedMessage(StoreOpenedMessage msg)
    {
    }
}
