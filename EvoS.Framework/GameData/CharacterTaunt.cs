using System;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;

namespace EvoS.Framework.GameData;

[Serializable]
public class CharacterTaunt
{
    public string m_tauntName;
    public string m_flavorText;
    public string m_obtainedText;
    public bool m_isHidden;
    public int m_uniqueID;
    public ActionType m_actionForTaunt;
    // [AssetFileSelector("Assets/StreamingAssets/Video/taunts/", "", ".ogv")]
    public string m_tauntVideoPath;
    public GameBalanceVars.TauntUnlockData m_tauntUnlockData;

    public int DebugGetIsoPriceFromUnlockCondition()
    {
        if (m_tauntUnlockData == null
            || m_tauntUnlockData.m_unlockData == null
            || m_tauntUnlockData.m_unlockData.UnlockConditions == null)
        {
            return 0;
        }
        foreach (GameBalanceVars.UnlockCondition unlockCondition in m_tauntUnlockData.m_unlockData.UnlockConditions)
        {
            if (unlockCondition.ConditionType == GameBalanceVars.UnlockData.UnlockType.Purchase)
            {
                return unlockCondition.typeSpecificData2;
            }
        }
        return 0;
    }

    public int DebugGetExpectedIsoPrice()
    {
        if (m_tauntUnlockData == null)
        {
            return 0;
        }
        switch (m_tauntUnlockData.Rarity)
        {
            case InventoryItemRarity.Uncommon:
                return 100;
            case InventoryItemRarity.Rare:
                return 300;
            case InventoryItemRarity.Epic:
                return 1200;
            case InventoryItemRarity.Legendary:
                return 1500;
            default:
                return 0;
        }
    }

    public static bool DebugIsRarityExpected(InventoryItemRarity rarity)
    {
        return rarity == InventoryItemRarity.Uncommon
               || rarity == InventoryItemRarity.Rare
               || rarity == InventoryItemRarity.Epic
               || rarity == InventoryItemRarity.Legendary;
    }
}
