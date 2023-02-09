using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Network.NetworkMessages
{
    [Serializable]
    [EvosMessage(617)]
    public class AccountComponent
    {
        public AccountComponent()
        {
            LastCharacter = CharacterType.Scoundrel;
            LastRemoteCharacters = new List<CharacterType>();
            UnlockedTitleIDs = new List<int>();
            TitleLevels = new Dictionary<int, int>();
            UnlockedBannerIDs = new List<int>();
            UnlockedRibbonIDs = new List<int>();
            UnlockedEmojiIDs = new List<int>();
            UnlockedOverconIDs = new List<int>();
            UnlockedLoadingScreenBackgroundIdsToActivatedState = new Dictionary<int, bool>();
            NumGlobalCharacterLoadouts = 2;
            SelectedTitleID = -1;
            SelectedForegroundBannerID = 65; // Defaults
            SelectedBackgroundBannerID = 95; // Defaults
            SelectedRibbonID = -1;
            AppliedEntitlements = new Dictionary<string, int>();
            PendingPurchases = new List<PendingPurchaseDetails>();
            KeyCodeMapping = new Dictionary<int, KeyCodeData>();
            UIStates = new Dictionary<UIStateIdentifier, int>();
            RecentSoloGamesPlayed = new LeakyBucket();
            RankedSortKarma = 0;
            HighestRankedTierReached = new Dictionary<int, Dictionary<string, int>>();
            FactionCompetitionData = new Dictionary<int, PlayerFactionCompetitionData>();
            PushToTalkKeyType = 0;
            PushToTalkKeyCode = 0;
            PushToTalkKeyName = null;
        }

        public CharacterType LastCharacter { get; set; }

        public List<CharacterType> LastRemoteCharacters { get; set; }

        public List<int> UnlockedTitleIDs { get; set; }

        public List<int> UnlockedEmojiIDs { get; set; }

        public List<int> UnlockedOverconIDs { get; set; }

        public List<int> UnlockedBannerIDs { get; set; }

        public List<int> UnlockedRibbonIDs { get; set; }

        public int SelectedTitleID { get; set; }

        public int SelectedForegroundBannerID { get; set; }

        public int SelectedBackgroundBannerID { get; set; }

        public int SelectedRibbonID { get; set; }

        [EvosMessage(619)]
        public Dictionary<int, bool> UnlockedLoadingScreenBackgroundIdsToActivatedState { get; set; }

        [EvosMessage(625)]
        public List<PendingPurchaseDetails> PendingPurchases { get; set; }

        public int NumGlobalCharacterLoadouts { get; set; }

        [EvosMessage(622)]
        public Dictionary<UIStateIdentifier, int> UIStates { get; set; }

        public int RankedSortKarma { get; set; }

        [EvosMessage(627)]
        public Dictionary<int, Dictionary<string, int>> HighestRankedTierReached { get; set; }

        [EvosMessage(630)]
        public Dictionary<int, PlayerFactionCompetitionData> FactionCompetitionData { get; set; }

        public bool DisplayDevTag { get; set; }

        public bool DailyQuestsAvailable { get; set; }

        [EvosMessage(140)]
        public CharacterType[] FreeRotationCharacters { get; set; }

        public DateTime FreeRotationNextUpdateDate { get; set; }

        [EvosMessage(96)]
        public Dictionary<int, KeyCodeData> KeyCodeMapping { get; set; }

        public int PushToTalkKeyType { get; set; }

        public int PushToTalkKeyCode { get; set; }

        public string PushToTalkKeyName { get; set; }

        public int FreelancerExpBonusGames { get; set; }

        public DateTime FreelancerExpBonusTime { get; set; }
    
        /*
         * Methods omitted
         */

        public object Clone()
        {
            return MemberwiseClone();
        }

        public Dictionary<int, int> TitleLevels;

        [EvosMessage(241)]
        public Dictionary<string, int> AppliedEntitlements;

        public const int MinNumGlobalCharacterLoadouts = 2;

        public LeakyBucket RecentSoloGamesPlayed;

        public string RAFReferralCode;

        [EvosMessage(396)]
        public enum UIStateIdentifier
        {
            HasResetMods,
            HasSeenTrustWarEndPopup,
            HasSeenSeasonTwoChapterTwo,
            HasSeenFactionWarSeasonTwoChapterTwo,
            TutorialOverview,
            TutorialPhases,
            TutorialMovement,
            TutorialCatalysts,
            TutorialCooldowns,
            TutorialPowerups,
            TutorialCover,
            TutorialFoW,
            TutorialRespawn,
            HasSeenSeasonTwoChapterThree,
            HasSeenSeasonTwoChapterFour,
            HasSeenSeasonTwoChapterFive,
            CashShopFeaturedItemsVersionViewed,
            NumLootMatrixesOpened,
            HasViewedFluxHighlight,
            HasViewedGGHighlight,
            NumDailiesChosen,
            HasSeenSeasonFourChapterOne,
            HasViewedFreelancerTokenHighlight,
            NONE = 10000
        }
    }
}
