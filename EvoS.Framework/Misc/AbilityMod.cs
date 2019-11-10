using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Misc
{
    public class AbilityMod : MonoBehaviour
    {
        public string m_name = string.Empty;
        public bool m_availableInGame = true;
        public int m_equipCost = 1;
        public string m_tooltip = string.Empty;
        public string m_flavorText = string.Empty;
        public string m_debugUnlocalizedTooltip = string.Empty;
        public AbilityPriority m_runPriorityOverride = AbilityPriority.Combat_Damage;
        public Ability.MovementAdjustment m_movementAdjustmentOverride = Ability.MovementAdjustment.ReducedMovement;

//        [Header("    The above effects use this 0-1 value")]
        public float m_effectTriggerChance = 1f;

        public List<AbilityTags> m_abilityTagsInMod = new List<AbilityTags>();
        public List<StatusType> m_statusWhenRequestedOverride = new List<StatusType>();

//        [Header("-- ID (only need to be unique among mods of the same ability)")]
        public int m_abilityScopeId;

        public AbilityModGameTypeReq m_gameTypeReq;
        public bool m_defaultEquip;

        public List<StatusType> m_savedStatusTypesForTooltips;
        public SerializedComponent m_iconSprite;

//        [Separator("Run Priority Mod", "orange")]
        public bool m_useRunPriorityOverride;

//        [Separator("Energy Cost, Cooldown/Stock", "orange")]
        public AbilityModPropertyInt m_techPointCostMod;

//        [Header("-- Cooldown / Stock")]
        public AbilityModPropertyInt m_maxCooldownMod;
        public AbilityModPropertyInt m_maxStocksMod;
        public AbilityModPropertyInt m_stockRefreshDurationMod;

        public AbilityModPropertyBool m_refillAllStockOnRefreshMod;

//        [Header("-- Free Action Override")]
        public AbilityModPropertyBool m_isFreeActionMod;

//        [Header("-- Auto Queue Override")]
        public AbilityModPropertyBool m_autoQueueIfValidMod;

//        [Separator("Targeter Range Mods (applies to all entries in Target Data)", "orange")]
        public AbilityModPropertyFloat m_targetDataMaxRangeMod;

        public AbilityModPropertyFloat m_targetDataMinRangeMod;

        public AbilityModPropertyBool m_targetDataCheckLosMod;

//        [Header("    Target Data override")]
        public bool m_useTargetDataOverrides;
        public SerializedVector<TargetData> m_targetDataOverrides;

//        [Separator("Tech Point Interaction", "orange")]
        public SerializedVector<TechPointInteractionMod> m_techPointInteractionMods;

//        [Header("-- Anim type override")] 
        public bool m_useActionAnimTypeOverride;
        public ActorModelData.ActionAnimationType m_actionAnimTypeOverride;

//        [Header("-- Movement Adjustment Override")]
        public bool m_useMovementAdjustmentOverride;

//        [Header("-- Effect to Self / Targeted Enemies or Allies")]
        public StandardEffectInfo m_effectToSelfOnCast;

        public StandardEffectInfo m_effectToTargetEnemyOnHit;

//        [Tooltip("NOTE: the ability doesn't automatically make allies targetable for this stuff. Each ability needs to do that if allies are only targetable with certain mods.")]
        public StandardEffectInfo m_effectToTargetAllyOnHit;

//        [Tooltip("This would be in addition to the Effect To Self On Cast if both are set. Use mainly for single-target abilities.")]
        public bool m_useAllyEffectForTargetedCaster;

//        [Header("    If you want more hits to increase the chance")]
        public bool m_effectTriggerChanceMultipliedPerHit;

//        [Header("-- Cooldown Reductions on Cast")]
        public AbilityModCooldownReduction m_cooldownReductionsOnSelf;

//        [Header("-- Sequence for self hit timing (for effect/cooldown reduction). If empty, will use placeholder")]
        public SerializedComponent m_selfHitTimingSequencePrefab;

//        [Header("-- Chain Ability's Additional Effects/Cooldown Reductions")]
        public List<ChainAbilityAdditionalModInfo> m_chainAbilityModInfo;

//        [Header("    Chain Ability override")]
        public bool m_useChainAbilityOverrides;

        public Ability[] m_chainAbilityOverrides;

//        [Header("-- Ability Tag Override")]
        public AbilityMod.TagOverrideType m_tagsModType;

//        [Header("-- Stat Mods While Mod is Equipped")]
        public SerializedVector<AbilityStatMod> m_statModsWhileEquipped;

//        [Header("-- Buff/Debuff Status when ability is requested (apply in Decision)")]
        public bool m_useStatusWhenRequestedOverride;

//        [Space(25f)] [Separator("End of Base Ability Mod", "orange")]
        public bool beginningOfModSpecificData;

        public AbilityMod()
        {
        }

        public AbilityMod(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            m_abilityScopeId = stream.ReadInt32();
            m_name = stream.ReadString32();
            m_availableInGame = stream.ReadBoolean();
            stream.AlignTo();
            m_gameTypeReq = (AbilityModGameTypeReq) stream.ReadInt32();
            m_equipCost = stream.ReadInt32();
            m_defaultEquip = stream.ReadBoolean();
            stream.AlignTo();
            m_tooltip = stream.ReadString32();
            m_flavorText = stream.ReadString32();
            m_debugUnlocalizedTooltip = stream.ReadString32();
            m_savedStatusTypesForTooltips = new SerializedVector<StatusType>(assetFile, stream);
            m_iconSprite = new SerializedComponent(assetFile, stream);
            m_useRunPriorityOverride = stream.ReadBoolean();
            stream.AlignTo();
            m_runPriorityOverride = (AbilityPriority) stream.ReadInt32();
            m_techPointCostMod = new AbilityModPropertyInt(assetFile, stream);
            m_maxCooldownMod = new AbilityModPropertyInt(assetFile, stream);
            m_maxStocksMod = new AbilityModPropertyInt(assetFile, stream);
            m_stockRefreshDurationMod = new AbilityModPropertyInt(assetFile, stream);
            m_refillAllStockOnRefreshMod = new AbilityModPropertyBool(assetFile, stream);
            m_isFreeActionMod = new AbilityModPropertyBool(assetFile, stream);
            m_autoQueueIfValidMod = new AbilityModPropertyBool(assetFile, stream);
            m_targetDataMaxRangeMod = new AbilityModPropertyFloat(assetFile, stream);
            m_targetDataMinRangeMod = new AbilityModPropertyFloat(assetFile, stream);
            m_targetDataCheckLosMod = new AbilityModPropertyBool(assetFile, stream);
            m_useTargetDataOverrides = stream.ReadBoolean();
            stream.AlignTo();
            m_targetDataOverrides = new SerializedArray<TargetData>(assetFile, stream);
            m_techPointInteractionMods = new SerializedArray<TechPointInteractionMod>(assetFile, stream);
            m_useActionAnimTypeOverride = stream.ReadBoolean();
            stream.AlignTo();
            m_actionAnimTypeOverride = (ActorModelData.ActionAnimationType) stream.ReadInt32();
            m_useMovementAdjustmentOverride = stream.ReadBoolean();
            stream.AlignTo();
            m_movementAdjustmentOverride = (Ability.MovementAdjustment) stream.ReadInt32();
            m_effectToSelfOnCast = new StandardEffectInfo(assetFile, stream);
            m_effectToTargetEnemyOnHit = new StandardEffectInfo(assetFile, stream);
            m_effectToTargetAllyOnHit = new StandardEffectInfo(assetFile, stream);
            m_useAllyEffectForTargetedCaster = stream.ReadBoolean();
            stream.AlignTo();
            m_effectTriggerChance = stream.ReadSingle();
            m_effectTriggerChanceMultipliedPerHit = stream.ReadBoolean();
            stream.AlignTo();
            m_cooldownReductionsOnSelf = new AbilityModCooldownReduction(assetFile, stream);
            m_selfHitTimingSequencePrefab = new SerializedComponent(assetFile, stream);
            m_chainAbilityModInfo = new SerializedVector<ChainAbilityAdditionalModInfo>(assetFile, stream);
            m_useChainAbilityOverrides = stream.ReadBoolean();
            stream.AlignTo();
            m_chainAbilityOverrides = new SerializedVector<SerializedMonoBehaviour>(assetFile, stream).ToChildArray<Ability>();
            m_tagsModType = (TagOverrideType) stream.ReadInt32();
            m_abilityTagsInMod = new SerializedVector<AbilityTags>(assetFile, stream);
            m_statModsWhileEquipped = new SerializedArray<AbilityStatMod>(assetFile, stream);
            m_useStatusWhenRequestedOverride = stream.ReadBoolean();
            stream.AlignTo();
            m_statusWhenRequestedOverride = new SerializedVector<StatusType>(assetFile, stream);
            beginningOfModSpecificData = stream.ReadBoolean();
            stream.AlignTo();
        }

        public override string ToString()
        {
            return $"{nameof(AbilityMod)}>(" +
                   $"{nameof(m_abilityScopeId)}: {m_abilityScopeId}, " +
                   $"{nameof(m_name)}: {m_name}, " +
                   $"{nameof(m_availableInGame)}: {m_availableInGame}, " +
                   $"{nameof(m_gameTypeReq)}: {m_gameTypeReq}, " +
                   $"{nameof(m_equipCost)}: {m_equipCost}, " +
                   $"{nameof(m_defaultEquip)}: {m_defaultEquip}, " +
                   $"{nameof(m_tooltip)}: {m_tooltip}, " +
                   $"{nameof(m_flavorText)}: {m_flavorText}, " +
                   $"{nameof(m_debugUnlocalizedTooltip)}: {m_debugUnlocalizedTooltip}, " +
                   $"{nameof(m_savedStatusTypesForTooltips)}: {m_savedStatusTypesForTooltips}, " +
                   $"{nameof(m_iconSprite)}: {m_iconSprite}, " +
                   $"{nameof(m_useRunPriorityOverride)}: {m_useRunPriorityOverride}, " +
                   $"{nameof(m_runPriorityOverride)}: {m_runPriorityOverride}, " +
                   $"{nameof(m_techPointCostMod)}: {m_techPointCostMod}, " +
                   $"{nameof(m_maxCooldownMod)}: {m_maxCooldownMod}, " +
                   $"{nameof(m_maxStocksMod)}: {m_maxStocksMod}, " +
                   $"{nameof(m_stockRefreshDurationMod)}: {m_stockRefreshDurationMod}, " +
                   $"{nameof(m_refillAllStockOnRefreshMod)}: {m_refillAllStockOnRefreshMod}, " +
                   $"{nameof(m_isFreeActionMod)}: {m_isFreeActionMod}, " +
                   $"{nameof(m_autoQueueIfValidMod)}: {m_autoQueueIfValidMod}, " +
                   $"{nameof(m_targetDataMaxRangeMod)}: {m_targetDataMaxRangeMod}, " +
                   $"{nameof(m_targetDataMinRangeMod)}: {m_targetDataMinRangeMod}, " +
                   $"{nameof(m_targetDataCheckLosMod)}: {m_targetDataCheckLosMod}, " +
                   $"{nameof(m_useTargetDataOverrides)}: {m_useTargetDataOverrides}, " +
                   $"{nameof(m_targetDataOverrides)}: {m_targetDataOverrides}, " +
                   $"{nameof(m_techPointInteractionMods)}: {m_techPointInteractionMods}, " +
                   $"{nameof(m_useActionAnimTypeOverride)}: {m_useActionAnimTypeOverride}, " +
                   $"{nameof(m_actionAnimTypeOverride)}: {m_actionAnimTypeOverride}, " +
                   $"{nameof(m_useMovementAdjustmentOverride)}: {m_useMovementAdjustmentOverride}, " +
                   $"{nameof(m_movementAdjustmentOverride)}: {m_movementAdjustmentOverride}, " +
                   $"{nameof(m_effectToSelfOnCast)}: {m_effectToSelfOnCast}, " +
                   $"{nameof(m_effectToTargetEnemyOnHit)}: {m_effectToTargetEnemyOnHit}, " +
                   $"{nameof(m_effectToTargetAllyOnHit)}: {m_effectToTargetAllyOnHit}, " +
                   $"{nameof(m_useAllyEffectForTargetedCaster)}: {m_useAllyEffectForTargetedCaster}, " +
                   $"{nameof(m_effectTriggerChance)}: {m_effectTriggerChance}, " +
                   $"{nameof(m_effectTriggerChanceMultipliedPerHit)}: {m_effectTriggerChanceMultipliedPerHit}, " +
                   $"{nameof(m_cooldownReductionsOnSelf)}: {m_cooldownReductionsOnSelf}, " +
                   $"{nameof(m_selfHitTimingSequencePrefab)}: {m_selfHitTimingSequencePrefab}, " +
                   $"{nameof(m_chainAbilityModInfo)}: {m_chainAbilityModInfo}, " +
                   $"{nameof(m_useChainAbilityOverrides)}: {m_useChainAbilityOverrides}, " +
                   $"{nameof(m_chainAbilityOverrides)}: {m_chainAbilityOverrides}, " +
                   $"{nameof(m_tagsModType)}: {m_tagsModType}, " +
                   $"{nameof(m_abilityTagsInMod)}: {m_abilityTagsInMod}, " +
                   $"{nameof(m_statModsWhileEquipped)}: {m_statModsWhileEquipped}, " +
                   $"{nameof(m_useStatusWhenRequestedOverride)}: {m_useStatusWhenRequestedOverride}, " +
                   $"{nameof(m_statusWhenRequestedOverride)}: {m_statusWhenRequestedOverride}, " +
                   $"{nameof(beginningOfModSpecificData)}: {beginningOfModSpecificData}, " +
                   ")";
        }


        public virtual System.Type GetTargetAbilityType()
        {
            return typeof(Ability);
        }

        public enum TagOverrideType
        {
            Ignore,
            Override,
            Append,
        }
    }
}
