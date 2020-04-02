using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Misc
{
    public class Ability : MonoBehaviour, ISerializedItem
    {
        public string m_debugUnlocalizedTooltip = string.Empty;

        public string m_rewardString = string.Empty;

//        private int m_overrideActorDataIndex = ActorData.s_invalidActorIndex;
        public string m_flavorText = string.Empty;
        public string m_abilityName = string.Empty;
        public string m_expectedSequencePrefixForEditor = string.Empty;
        public int m_initialStockAmount = -1;
        public int m_stockConsumedOnCast = 1;
        public AbilityPriority m_runPriority = AbilityPriority.Combat_Damage;
        public ActorModelData.ActionAnimationType m_actionAnimType = ActorModelData.ActionAnimationType.Ability1;
        public MovementAdjustment m_movementAdjustment = MovementAdjustment.ReducedMovement;
        public float m_movementSpeed = 8f;
        public float m_cameraBoundsMinHeight = 2.5f;
        public List<AbilityTags> m_tags = new List<AbilityTags>();

        public List<StatusType> m_statusWhenRequested = new List<StatusType>();

//        private List<AbilityUtil_Targeter> m_targeters = new List<AbilityUtil_Targeter>();
        private List<GameObject> m_backupTargeterHighlights = new List<GameObject>();

//        private List<AbilityTarget> m_backupTargets = new List<AbilityTarget>();
        public const string c_abilityPreviewVideoPath = "Video/AbilityPreviews/";
        public string m_toolTip;
        public List<StatusType> m_savedStatusTypesForTooltips;
        public string m_shortToolTip;

        private string m_toolTipForUI;

//        public GameObject m_sequencePrefab;
        public SerializedComponent m_sequencePrefab;

//        private ActorData m_actorData;
        private bool m_searchedForActorData;

//        private List<AbilityTooltipNumber> m_abilityTooltipNumbers;
//        private List<AbilityTooltipNumber> m_nameplateTargetingNumbers;
        private bool m_lastUpdateShowingAffectedSquares;
        public bool m_ultimate;
        public string m_previewVideo;
        public int m_cooldown;
        public int m_maxStocks;
        public int m_stockRefreshDuration;
        public bool m_refillAllStockOnRefresh;
        public bool m_abilityManagedStockCount;
        public bool m_freeAction;

        public int m_techPointsCost;

        public SerializedVector<TechPointInteraction> m_techPointInteractions;
        public RotationVisibilityMode m_rotationVisibilityMode;

        public float m_movementDuration;

//        public TargetData[] m_targetData;
        public SerializedVector<TargetData> m_targetData;

        public Ability[] m_chainAbilities;

//        public List<TargeterTemplateSwapData> m_targeterTemplateSwaps;
        public SerializedVector<TargeterTemplateSwapData> m_targeterTemplateSwaps;
        private AbilityMod m_currentAbilityMod;

        public virtual bool HasPassivePendingStatus(StatusType status, ActorData owner)
        {
            return false;
        }

//        public Sprite sprite { get; set; }

        public virtual Ability.MovementAdjustment GetMovementAdjustment()
        {
            if (this.m_currentAbilityMod != null && this.m_currentAbilityMod.m_useMovementAdjustmentOverride)
            {
                return this.m_currentAbilityMod.m_movementAdjustmentOverride;
            }
            return this.m_movementAdjustment;
        }

        public bool GetPreventsMovement()
        {
            return this.GetMovementAdjustment() == Ability.MovementAdjustment.NoMovement;
        }

        public bool GetAffectsMovement()
        {
            return this.GetMovementAdjustment() != Ability.MovementAdjustment.FullMovement;
        }

        public virtual List<StatusType> GetStatusToApplyWhenRequested()
        {
            if (this.m_currentAbilityMod != null && this.m_currentAbilityMod.m_useStatusWhenRequestedOverride)
            {
                return this.m_currentAbilityMod.m_statusWhenRequestedOverride;
            }
            return this.m_statusWhenRequested;
        }

        public enum TargetingParadigm
        {
            Position = 1,
            Direction = 2,
            BoardSquare = 3
        }

        public enum ValidateCheckPath
        {
            Ignore,
            CanBuildPath,
            CanBuildPathAllowThroughInvalid
        }

        public enum RotationVisibilityMode
        {
            Default_NonFreeAction,
            OnAllyClientOnly,
            Always,
            Never
        }

        public enum MovementAdjustment
        {
            FullMovement,
            ReducedMovement,
            NoMovement
        }

        public Ability[] GetChainAbilities()
        {
            Ability[] abilityArray = m_chainAbilities;
            if (m_currentAbilityMod != null && m_currentAbilityMod.m_useChainAbilityOverrides)
                abilityArray = m_currentAbilityMod.m_chainAbilityOverrides;
            return abilityArray;
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            m_toolTip = stream.ReadString32();
            m_debugUnlocalizedTooltip = stream.ReadString32();
            m_savedStatusTypesForTooltips = new SerializedVector<StatusType>(assetFile, stream);
            m_shortToolTip = stream.ReadString32();
            m_rewardString = stream.ReadString32();
            m_sequencePrefab = new SerializedComponent(assetFile, stream);
            m_abilityName = stream.ReadString32();
            m_flavorText = stream.ReadString32();
            m_ultimate = stream.ReadBoolean();
            stream.AlignTo();
            m_previewVideo = stream.ReadString32();
            m_expectedSequencePrefixForEditor = stream.ReadString32();
            m_cooldown = stream.ReadInt32();
            m_maxStocks = stream.ReadInt32();
            m_stockRefreshDuration = stream.ReadInt32();
            m_refillAllStockOnRefresh = stream.ReadBoolean();
            stream.AlignTo();
            m_initialStockAmount = stream.ReadInt32();
            m_stockConsumedOnCast = stream.ReadInt32();
            m_abilityManagedStockCount = stream.ReadBoolean();
            stream.AlignTo();
            m_runPriority = (AbilityPriority) stream.ReadInt32();
            m_freeAction = stream.ReadBoolean();
            stream.AlignTo();
            m_techPointsCost = stream.ReadInt32();
            m_techPointInteractions = new SerializedVector<TechPointInteraction>(assetFile, stream);
            m_actionAnimType = (ActorModelData.ActionAnimationType) stream.ReadInt32();
            m_rotationVisibilityMode = (RotationVisibilityMode) stream.ReadInt32();
            m_movementAdjustment = (MovementAdjustment) stream.ReadInt32();
            m_movementSpeed = stream.ReadSingle();
            m_movementDuration = stream.ReadSingle();
            m_cameraBoundsMinHeight = stream.ReadSingle();
            m_targetData = new SerializedArray<TargetData>(assetFile, stream);
            m_tags = new SerializedVector<AbilityTags>(assetFile, stream);
            m_statusWhenRequested = new SerializedVector<StatusType>(assetFile, stream);
            m_chainAbilities = new SerializedVector<SerializedMonoBehaviour>(assetFile, stream).ToChildArray<Ability>();
            m_targeterTemplateSwaps = new SerializedVector<TargeterTemplateSwapData>(assetFile, stream);
        }
    }
}
