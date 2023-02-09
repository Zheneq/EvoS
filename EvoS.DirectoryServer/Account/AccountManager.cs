using System.Collections.Generic;
using EvoS.DirectoryServer.Character;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace EvoS.DirectoryServer.Account
{
    public class AccountManager
    {
        public static int DefaultISOAmount = 100000;
        public static int DefaultFluxAmount = 50000;
        public static int DefaultGGAmount = 508;
        public static int DefaultRankedCurrencyAmount = 3;

        public static bool DailyQuestsAvailable = false;

        public static CharacterType DefaultCharacterType = CharacterType.Tracker;
        public static GameType DefaultGameType = GameType.PvP;
        
        
        public static PersistedAccountData CreateAccount(long accountId, string username)
        {
            PersistedAccountData accountData = new PersistedAccountData
            {
                AccountComponent = GetAccountComponent(accountId),
                AccountId = accountId,
                BankComponent = CreateBankComponent(accountId),
                CharacterData = CharacterManager.GetPersistedCharacterData(accountId),
                Handle = $"{username}#{accountId % 900 + 100}",
                InventoryComponent = InventoryManager.GetInventoryComponent(accountId),
                QuestComponent = new QuestComponent()
                {
                    ActiveSeason = 9,
                    SeasonExperience = new Dictionary<int, ExperienceComponent>()
                    {
                        {9, new ExperienceComponent()}
                    }
                },
                SchemaVersion = new SchemaVersion<AccountSchemaChange>(0x1FFFF),
                UserName = username
            };

            return accountData;
        }
        public static AccountComponent GetAccountComponent(long accountId)
        {
            AccountComponent accountComponent = new AccountComponent()
            {
                AppliedEntitlements = new Dictionary<string, int>(),
                DailyQuestsAvailable = DailyQuestsAvailable,
                DisplayDevTag = false,
                FactionCompetitionData = new Dictionary<int, PlayerFactionCompetitionData>(),
                FreeRotationCharacters = new CharacterType[] { },
                LastCharacter = DefaultCharacterType,
                SelectedBackgroundBannerID = 95, // Defaults
                SelectedForegroundBannerID = 65, // Defaults
                SelectedRibbonID = -1,
                SelectedTitleID = -1,
                UnlockedBannerIDs = InventoryManager.GetDefaultUnlockedBannerIDs(accountId),
                UIStates = new Dictionary<AccountComponent.UIStateIdentifier, int>
                {
                    { AccountComponent.UIStateIdentifier.HasViewedFluxHighlight, 1 },
                    { AccountComponent.UIStateIdentifier.HasViewedGGHighlight, 1 },
                    // Do not Show flashy New text next to store tab
                    { AccountComponent.UIStateIdentifier.CashShopFeaturedItemsVersionViewed, 14 }
                },
                UnlockedEmojiIDs = InventoryManager.GetUnlockedEmojiIDs(accountId),
                UnlockedLoadingScreenBackgroundIdsToActivatedState = InventoryManager.GetActivatedLoadingScreenBackgroundIds(accountId),
                UnlockedOverconIDs = InventoryManager.GetUnlockedOverconIDs(accountId),
                UnlockedTitleIDs = InventoryManager.GetUnlockedTitleIDs(accountId),
                UnlockedRibbonIDs = InventoryManager.GetUnlockedRibbonIDs(accountId)
            };

            return accountComponent;
        }
        
        public static BankComponent CreateBankComponent(long accountId)
        {
            // TODO
            BankComponent bank = new BankComponent()
            {
                CurrentAmounts = new CurrencyWallet()
                {
                    Data = new List<CurrencyData>()
                    {
                        new CurrencyData() { Type = CurrencyType.ISO, Amount = DefaultISOAmount },
                        new CurrencyData() { Type = CurrencyType.FreelancerCurrency, Amount = DefaultFluxAmount },
                        new CurrencyData() { Type = CurrencyType.GGPack, Amount = DefaultGGAmount },
                        new CurrencyData() { Type = CurrencyType.RankedCurrency, Amount = DefaultRankedCurrencyAmount }
                    }
                }
            };

            return bank;
        }
    }
}
