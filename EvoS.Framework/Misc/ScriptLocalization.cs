using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Misc
{
 public static class ScriptLocalization
  {
    public static string Get(string Term)
    {
      return ScriptLocalization.Get(Term, false, 0);
    }

    public static string Get(string Term, bool FixForRTL)
    {
      return ScriptLocalization.Get(Term, FixForRTL, 0);
    }

    public static string Get(string Term, bool FixForRTL, int maxLineLengthForRTL)
    {
      return Term; // LocalizationManager.GetTermTranslation(Term, FixForRTL, maxLineLengthForRTL);
    }

    public static string GetLoadingSubtypeHeader(int setIndex, int displayIndex)
    {
      return string.Format("GameSubType_{0}_DisplayIndex_{1}_Header@GameSubTypeData", (object) setIndex, (object) displayIndex);
    }

    public static string GetLoadingSubtypeTooltip(int setIndex, int displayIndex)
    {
      return string.Format("GameSubType_{0}_DisplayIndex_{1}_Tooltip@GameSubTypeData", (object) setIndex, (object) displayIndex);
    }

    public static string GetPersistedStatBucketNameKey(PersistedStatBucket bucket)
    {
      return "PersistedStatBucket_" + bucket.ToString() + "@Global";
    }

    public static string GetQuestNameKey(int questId)
    {
      return "Quest_" + Convert.ToString(questId) + "_Name@QuestWideData";
    }

    public static string GetQuestDescription(int questId)
    {
      return "Quest_" + Convert.ToString(questId) + "_Desc@QuestWideData";
    }

    public static string GetQuestFlavorTextKey(int questId)
    {
      return "Quest_" + Convert.ToString(questId) + "_Flavor@QuestWideData";
    }

    public static string GetQuestLongDescriptionKey(int questId)
    {
      return "Quest_" + Convert.ToString(questId) + "_LongDesc@QuestWideData";
    }

    public static string GetQuestTypeDisplayNameKey(int questId)
    {
      return "Quest_" + Convert.ToString(questId) + "_TypeDisplayName@QuestWideData";
    }

    public static string GetQuestObjectiveKey(int questId, int objectiveId)
    {
      return "Quest_" + Convert.ToString(questId) + "_Obj_" + Convert.ToString(objectiveId) + "@QuestWideData";
    }

    public static string GetInventoryItemNameKey(int invItemId)
    {
      return "InventoryItem_" + Convert.ToString(invItemId) + "_Name@InventoryWideData";
    }

    public static string GetInventoryItemDescriptionKey(int invItemId)
    {
      return "InventoryItem_" + Convert.ToString(invItemId) + "_Desc@InventoryWideData";
    }

    public static string GetInventoryObtainedDescriptionKey(int invItemId)
    {
      return "InventoryItem_" + Convert.ToString(invItemId) + "_Obtained@InventoryWideData";
    }

    public static string GetInventoryFlavorTextKey(int invItemId)
    {
      return "InventoryItem_" + Convert.ToString(invItemId) + "_Flavor@InventoryWideData";
    }

    public static string GetLootTableNameKey(int lootId)
    {
      return "LootTable_" + Convert.ToString(lootId) + "_Name@InventoryWideData";
    }

    public static string GetLootTableDescriptionKey(int lootId)
    {
      return "LootTable_" + Convert.ToString(lootId) + "_Desc@InventoryWideData";
    }

    public static string GetKarmaNameKey(int karmaId)
    {
      return "Karma_" + Convert.ToString(karmaId) + "_Name@InventoryWideData";
    }

    public static string GetKarmaDescriptionKey(int karmaId)
    {
      return "Karma_" + Convert.ToString(karmaId) + "_Desc@InventoryWideData";
    }

    public static string GetLoreTitleKey(int loreId)
    {
      return "Lore_" + Convert.ToString(loreId) + "_Title@LoreWideData";
    }

    public static string GetLoreArticleTextKey(int loreId)
    {
      return "Lore_" + Convert.ToString(loreId) + "_ArticleText@LoreWideData";
    }

    public static string GetSeasonNameKey(int seasonId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Name@SeasonWideData";
    }

    public static string GetSeasonSubTitleKey(int seasonId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_SubTitle@SeasonWideData";
    }

    public static string GetSeasonEndHeaderKey(int seasonId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_EndHeader@SeasonWideData";
    }

    public static string GetSeasonChapterNameKey(int seasonId, int chapterId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Chapter_" + Convert.ToString(chapterId) + "_Name@SeasonWideData";
    }

    public static string GetSeasonChapterUnlockKey(int seasonId, int chapterId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Chapter_" + Convert.ToString(chapterId) + "_Unlock@SeasonWideData";
    }

    public static string GetSeasonStorytimeHeaderKey(int seasonId, int chapterId, int storyId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Chapter_" + Convert.ToString(chapterId) + "_Story_" + Convert.ToString(storyId) + "_Header@SeasonWideData";
    }

    public static string GetSeasonStorytimeBodyKey(int seasonId, int chapterId, int storyId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Chapter_" + Convert.ToString(chapterId) + "_Story_" + Convert.ToString(storyId) + "_Body@SeasonWideData";
    }

    public static string GetSeasonStorytimeLongBodyKey(int seasonId, int chapterId, int storyId)
    {
      return "Season_" + Convert.ToString(seasonId) + "_Chapter_" + Convert.ToString(chapterId) + "_Story_" + Convert.ToString(storyId) + "_LongBody@SeasonWideData";
    }

    public static string GetPlayerTitleKey(int titleId)
    {
      return "PlayerTitle_" + Convert.ToString(titleId) + "@GameWideData";
    }

    public static string GetPlayerTitleUnlockConditionKey(int titleId, int unlockConditionId)
    {
      return "PlayerTitle_" + Convert.ToString(titleId) + "_Unlock_" + Convert.ToString(unlockConditionId) + "_Condition@GameWideData";
    }

    public static string GetPlayerBannerKey(int bannerId)
    {
      return "PlayerBanner_" + Convert.ToString(bannerId) + "@GameWideData";
    }

    public static string GetPlayerBannerUnlockConditionKey(int bannerId, int unlockConditionId)
    {
      return "PlayerBanner_" + Convert.ToString(bannerId) + "_Unlock_" + Convert.ToString(unlockConditionId) + "_Condition@GameWideData";
    }

    public static string GetBannerObtainedDescriptionKey(int bannerId)
    {
      return "PlayerBanner_" + Convert.ToString(bannerId) + "_ObtainedDescription@GameWideData";
    }

    public static string GetTitleObtainedDescriptionKey(int titleId)
    {
      return "PlayerTitle_" + Convert.ToString(titleId) + "_ObtainedDescription@GameWideData";
    }

    public static string GetPlayerRibbonKey(int ribbonId)
    {
      return "PlayerRibbon_" + Convert.ToString(ribbonId) + "@GameWideData";
    }

    public static string GetPlayerRibbonUnlockConditionKey(int ribbonId, int unlockConditionId)
    {
      return "PlayerRibbon_" + Convert.ToString(ribbonId) + "_Unlock_" + Convert.ToString(unlockConditionId) + "_Condition@GameWideData";
    }

    public static string GetRibbonObtainedDescriptionKey(int ribbonId)
    {
      return "PlayerRibbon_" + Convert.ToString(ribbonId) + "Obtn@GameWideData";
    }

    public static string GetEmojiNameKey(int emojiId)
    {
      return "Emoji_" + Convert.ToString(emojiId) + "_Name@GameWideData";
    }

    public static string GetEmojiObtainedDescriptionKey(int emojiId)
    {
      return "Emoji_" + Convert.ToString(emojiId) + "_ObtainedDescription@GameWideData";
    }

    public static string GetEmojiPurchaseDescriptionKey(int emojiId)
    {
      return "Emoji_" + Convert.ToString(emojiId) + "_PurchaseDescription@GameWideData";
    }

    public static string GetEmojiTagKey(int emojiId)
    {
      return "Emoji_" + Convert.ToString(emojiId) + "_Tag@GameWideData";
    }

    public static string GetEmojiUnlockKey(int emojiId)
    {
      return "Emoji_" + Convert.ToString(emojiId) + "_Unlock@GameWideData";
    }

    public static string GetMapNameKey(string mapId)
    {
      return "Map_" + mapId + "_Name@GameWideData";
    }

    public static string GetMatrixPackEventTextKey(int matrixId)
    {
      return "MatrixPack_" + (object) matrixId + "_EventText@GameWideData";
    }

    public static string GetMatrixPackDescriptionKey(int matrixId)
    {
      return "MatrixPack_" + (object) matrixId + "_Description@GameWideData";
    }

    public static string GetGamePackNameKey(int packId)
    {
      return "GamePack_" + (object) packId + "_Name@GameWideData";
    }

    public static string GetGamePackDescKey(int packId)
    {
      return "GamePack_" + (object) packId + "_Desc@GameWideData";
    }

    public static string GetCharacterNameKey(string characterId)
    {
      return "Character_" + characterId + "_Name@GameWideData";
    }

    public static string GetCharacterSelectTooltipKey(string characterId)
    {
      return "Character_" + characterId + "_SelectTooltip@GameWideData";
    }

    public static string GetCharacterSelectAboutKey(string characterId)
    {
      return "Character_" + characterId + "_AboutDesc@GameWideData";
    }

    public static string GetCharacterBioKey(string characterId)
    {
      return "Character_" + characterId + "_Bio@GameWideData";
    }

    public static string GetCharacterSkinNameKey(string characterId, int skinId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Name@GameWideData";
    }

    public static string GetCharacterSkinDescriptionKey(string characterId, int skinId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Desc@GameWideData";
    }

    public static string GetCharacterSkinFlavorKey(string characterId, int skinId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Flavor@GameWideData";
    }

    public static string GetCharacterPatternNameKey(string characterId, int skinId, int patternId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Name@GameWideData";
    }

    public static string GetPatternColorNameKey(
      string characterId,
      int skinId,
      int patternId,
      int colorId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Color_" + Convert.ToString(colorId) + "_Name@GameWideData";
    }

    public static string GetPatternColorDescKey(
      string characterId,
      int skinId,
      int patternId,
      int colorId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Color_" + Convert.ToString(colorId) + "_Desc@GameWideData";
    }

    public static string GetPatternColorFlavorKey(
      string characterId,
      int skinId,
      int patternId,
      int colorId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Color_" + Convert.ToString(colorId) + "_Flavor@GameWideData";
    }

    public static string GetPatternColorObtainedDescKey(
      string characterId,
      int skinId,
      int patternId,
      int colorId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Color_" + Convert.ToString(colorId) + "_Obtn@GameWideData";
    }

    public static string GetPatternColorPurchaseDescKey(
      string characterId,
      int skinId,
      int patternId,
      int colorId)
    {
      return "Character_" + characterId + "_Skin_" + Convert.ToString(skinId) + "_Pattern_" + Convert.ToString(patternId) + "_Color_" + Convert.ToString(colorId) + "_Pur@GameWideData";
    }

    public static string GetCharacterTauntNameKey(string characterId, int tauntId)
    {
      return "Character_" + characterId + "_Taunt_" + Convert.ToString(tauntId) + "_Name@GameWideData";
    }

    public static string GetCharacterTauntObtainedDescKey(string characterId, int tauntId)
    {
      return "Character_" + characterId + "_Taunt_" + Convert.ToString(tauntId) + "_Obtn@GameWideData";
    }

    public static string GetCharacterTauntPurchaseDescKey(string characterId, int tauntId)
    {
      return "Character_" + characterId + "_Taunt_" + Convert.ToString(tauntId) + "_Pur@GameWideData";
    }

    public static string GetCharacterVFXSwapNameKey(
      string characterId,
      int abilityId,
      int vfxSwapId)
    {
      return "Character_" + characterId + "_Ability_" + Convert.ToString(abilityId) + "_VFX_" + Convert.ToString(vfxSwapId) + "_Name@GameWideData";
    }

    public static string GetCharacterVFXSwapObtainedDescKey(
      string characterId,
      int abilityId,
      int vfxSwapId)
    {
      return "Character_" + characterId + "_Ability_" + Convert.ToString(abilityId) + "_VFX_" + Convert.ToString(vfxSwapId) + "_Obtn@GameWideData";
    }

    public static string GetCharacterVFXSwapPurchaseDescKey(
      string characterId,
      int abilityId,
      int vfxSwapId)
    {
      return "Character_" + characterId + "_Ability_" + Convert.ToString(abilityId) + "_VFX_" + Convert.ToString(vfxSwapId) + "_Pur@GameWideData";
    }

    public static string GetLoadingScreenTipKey(int tipId)
    {
      return "LoadingScreenTip_" + Convert.ToString(tipId) + "@GameWideData";
    }

    public static string GetKeyBindCommandNameKey(string keyBindId)
    {
      return "KeyBindCommand_" + keyBindId + "_Name@GameWideData";
    }

    public static string GetCardDisplayNameKey(CardType m_cardType)
    {
      return "Catalyst_" + m_cardType.ToString() + "_Name@CatalystData";
    }

    public static string GetAbilityNameKey(string abilityType, string abilityId)
    {
      return "Ability_" + abilityType + "_" + abilityId + "_Name@AbilitiesData";
    }

    public static string GetAbilityFinalFullTooltipKey(string abilityType, string abilityId)
    {
      return "Ability_" + abilityType + "_" + abilityId + "_Tooltip@AbilitiesData";
    }

    public static string GetAbilityRewardKey(string abilityType, string abilityId)
    {
      return "Ability_" + abilityType + "_" + abilityId + "_Reward@AbilitiesData";
    }

    public static string GetAbilityModNameKey(string abilityType, string modId)
    {
      return "Mod_" + abilityType + "_" + modId + "_Name@AbilitiesData";
    }

    public static string GetAbilityModFinalTooltipKey(string abilityType, string modId)
    {
      return "Mod_" + abilityType + "_" + modId + "_Tooltip@AbilitiesData";
    }

    public static string GetFreelancerStatNameKey(string freelancerType, int statIndex)
    {
      return "FreelancerStat_" + freelancerType + "_" + (object) statIndex + "_Name@FreelancerStats";
    }

    public static string GetFreelancerStatDescriptionKey(string freelancerType, int statIndex)
    {
      return "FreelancerStat_" + freelancerType + "_" + (object) statIndex + "_Description@FreelancerStats";
    }

    public static string GetBadgeGroupName(int badgeGroupIndex)
    {
      return "BadgeGroup_" + (object) badgeGroupIndex + "_Name@Badges";
    }

    public static string GetBadgeGroupDescription(int badgeGroupIndex)
    {
      return "BadgeGroup_" + (object) badgeGroupIndex + "_Description@Badges";
    }

    public static string GetStatNameKey(StatDisplaySettings.StatType statType)
    {
      return "Stat_" + statType.ToString() + "_Name@Badges";
    }

    public static string GetStatDescriptionKey(StatDisplaySettings.StatType statType)
    {
      return "Stat_" + statType.ToString() + "_Description@Badges";
    }

    public static string GetBadgeNameKey(int badgeIndex)
    {
      return "Badge_" + (object) badgeIndex + "_Name@Badges";
    }

    public static string GetBadgeDescriptionKey(int badgeIndex)
    {
      return "Badge_" + (object) badgeIndex + "_Description@Badges";
    }

    public static string GetBadgeGroupRequirementDescriptionKey(int badgeIndex)
    {
      return "Badge_" + (object) badgeIndex + "_GroupRequirementDescription@Badges";
    }

    public static string GetStoreItemOverlayKey(int itemId)
    {
      return "StoreItem_" + (object) itemId + "_Overlay@GameWideData";
    }

    public static string GetSlashCommandKey(string slashcommand)
    {
      return slashcommand + "@SlashCommand";
    }

    public static string GetSlashCommandDescKey(string slashcommand)
    {
      return slashcommand + "@SlashCommandDesc";
    }

    public static string GetSlashCommandAliasKey(string slashcommand, int aliasId)
    {
      return slashcommand + "@SlashCommandAlias" + Convert.ToString(aliasId);
    }

    public static string GetFactionGroupNameKey(int factionGroupId)
    {
      return "FactionGroup_" + Convert.ToString(factionGroupId) + "_" + Convert.ToString(factionGroupId) + "_Name@FactionWideData";
    }

    public static string GetFactionNameKey(int factionCompletionId, int factionId)
    {
      return "FactionCompletion_" + Convert.ToString(factionCompletionId) + "_Faction_" + Convert.ToString(factionId) + "_Name@FactionWideData";
    }

    public static string GetFactionLongNameKey(int factionCompletionId, int factionId)
    {
      return "FactionCompletion_" + Convert.ToString(factionCompletionId) + "_Faction_" + Convert.ToString(factionId) + "_Long@FactionWideData";
    }

    public static string GetFactionLoreDescriptionKey(int factionCompletionId, int factionId)
    {
      return "FactionCompletion_" + Convert.ToString(factionCompletionId) + "_Faction_" + Convert.ToString(factionId) + "_Lore@FactionWideData";
    }

    public static string GetStatusIconPopupTextKey(int statusIconId)
    {
      return "StatusIcon_" + Convert.ToString(statusIconId) + "_PopupText@AbilitiesData";
    }

    public static string GetStatusIconBuffNameKey(int statusIconId)
    {
      return "StatusIcon_" + Convert.ToString(statusIconId) + "_BuffName@AbilitiesData";
    }

    public static string GetStatusIconBuffDescKey(int statusIconId)
    {
      return "StatusIcon_" + Convert.ToString(statusIconId) + "_BuffDesc@AbilitiesData";
    }

    public static string GetOverconNameKey(int overconId)
    {
      return "Overcon_" + Convert.ToString(overconId) + "_Name@UIOverconData";
    }

    public static string GetOverconCommandKey(int overconId)
    {
      return "Overcon_" + Convert.ToString(overconId) + "_Command@UIOverconData";
    }

    public static string GetOverconObtainedDescKey(int overconId)
    {
      return "Overcon_" + Convert.ToString(overconId) + "_ObtainedDesc@UIOverconData";
    }

    public static string GetOverconUnlockConditionKey(int overconId, int unlockConditionId)
    {
      return "Overcon_" + Convert.ToString(overconId) + "_Unlock_" + Convert.ToString(unlockConditionId) + "_Condition@UIOverconData";
    }

    public static string GetLoadingScreenBackgroundNameKey(int loadingScreenBgId)
    {
      return "LoadingScreenBg_" + Convert.ToString(loadingScreenBgId) + "_Name@GameWideData";
    }

    public static string GetLoadingScreenBackgroundObtainedDescriptionKey(int loadingScreenBgId)
    {
      return "LoadingScreenBg_" + Convert.ToString(loadingScreenBgId) + "_ObtainedDesc@GameWideData";
    }

    public static string GetLoadingScreenBackgroundPurchaseDescriptionKey(int loadingScreenBgId)
    {
      return "LoadingScreenBg_" + Convert.ToString(loadingScreenBgId) + "_PurchaseDesc@GameWideData";
    }
  }
}
