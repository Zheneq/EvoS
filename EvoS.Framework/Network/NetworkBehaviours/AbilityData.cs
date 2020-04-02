using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [SerializedMonoBehaviour("AbilityData")]
    public class AbilityData : NetworkBehaviour
    {
        private const int kListm_cooldownsSync = -1695732229;
        private const int kListm_consumedStockCount = 1389109193;
        private const int kListm_stockRefreshCountdowns = -1016879281;
        private const int kListm_currentCardIds = 384100343;
        private const int kCmdCmdClearCooldowns = 36238543;
        private const int kCmdCmdRefillStocks = 1108895015;

        public bool IgnoreForLocalization { get; set; }
        public Ability Ability0 { get; set; }
        public string SpritePath0 { get; set; }
        public Ability Ability1 { get; set; }
        public string SpritePath1 { get; set; }
        public Ability Ability2 { get; set; }
        public string SpritePath2 { get; set; }
        public Ability Ability3 { get; set; }
        public string SpritePath3 { get; set; }
        public Ability Ability4 { get; set; }
        public string SpritePath4 { get; set; }
        public Ability Ability5 { get; set; }
        public string SpritePath5 { get; set; }
        public Ability Ability6 { get; set; }
        public string SpritePath6 { get; set; }
        public SerializedVector<SerializedComponent> BotDifficultyAbilityModSets { get; set; }
        public SerializedVector<SerializedComponent> CompsToInspectInAbilityKitInspector { get; set; }
        public string SequenceDirNameOverride { get; set; }
        public string AbilitySetupNotes { get; set; }

        private SyncListInt _cooldownsSync = new SyncListInt();
        private SyncListInt _consumedStockCount = new SyncListInt();
        private SyncListInt _stockRefreshCountdowns = new SyncListInt();
        private SyncListInt _currentCardIds = new SyncListInt();
        private AbilityEntry[] m_abilities;
        private List<Ability> m_allChainAbilities;
        private List<Ability> m_cachedCardAbilities = new List<Ability>();
        private List<ActionType> m_allChainAbilityParentActionTypes;
        private Dictionary<string, int> m_cooldowns;
        private ActorData m_actor;
        private ActionType _selectedActionForTargeting = ActionType.INVALID_ACTION;

        public SyncListInt CooldownsSync => _cooldownsSync;
        public SyncListInt ConsumedStockCount => _consumedStockCount;
        public SyncListInt StockRefreshCountdowns => _stockRefreshCountdowns;
        public SyncListInt CurrentCardIds => _currentCardIds;
        public ActionType SelectedActionForTargeting
        {
            get => _selectedActionForTargeting;
            set
            {
                if (_selectedActionForTargeting.Equals(value))
                {
                    return;
                }
                _selectedActionForTargeting = value;
                MarkAsDirty(DirtyBit.SelectedActionForTargetting);
            }
        }

        static AbilityData()
        {
            RegisterSyncListDelegate(typeof(AbilityData), kListm_cooldownsSync, InvokeSyncListm_cooldownsSync);
            RegisterSyncListDelegate(typeof(AbilityData), kListm_consumedStockCount,
                InvokeSyncListm_consumedStockCount);
            RegisterSyncListDelegate(typeof(AbilityData), kListm_stockRefreshCountdowns,
                InvokeSyncListm_stockRefreshCountdowns);
            RegisterSyncListDelegate(typeof(AbilityData), kListm_currentCardIds, InvokeSyncListm_currentCardIds);
        }

        public override void Awake()
        {
            m_abilities = new AbilityEntry[14];
            for (int index = 0; index < 14; ++index)
                m_abilities[index] = new AbilityEntry();
            m_abilities[0].Setup(Ability0);
            m_abilities[1].Setup(Ability1);
            m_abilities[2].Setup(Ability2);
            m_abilities[3].Setup(Ability3);
            m_abilities[4].Setup(Ability4);
            m_allChainAbilities = new List<Ability>();
            m_allChainAbilityParentActionTypes = new List<ActionType>();
            for (int index = 0; index < m_abilities.Length; ++index)
            {
                var ability = m_abilities[index];
                if (ability.ability != null)
                {
                    foreach (var chainAbility in ability.ability.GetChainAbilities())
                    {
                        if (chainAbility != null)
                            AddToAllChainAbilitiesList(chainAbility, (ActionType) index);
                    }
                }
            }

            m_cooldowns = new Dictionary<string, int>();
            m_actor = GetComponent<ActorData>();
            for (int index = 0; index < 3; ++index)
                m_cachedCardAbilities.Add(null);

            _cooldownsSync.InitializeBehaviour(this, kListm_cooldownsSync);
            _consumedStockCount.InitializeBehaviour(this, kListm_consumedStockCount);
            _stockRefreshCountdowns.InitializeBehaviour(this, kListm_stockRefreshCountdowns);
            _currentCardIds.InitializeBehaviour(this, kListm_currentCardIds);
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            stream.AlignTo();

            IgnoreForLocalization = stream.ReadBoolean();
            stream.AlignTo();

            Ability0 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath0 = stream.ReadString32();
            Ability1 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath1 = stream.ReadString32();
            Ability2 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath2 = stream.ReadString32();
            Ability3 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath3 = stream.ReadString32();
            Ability4 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath4 = stream.ReadString32();
            Ability5 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath5 = stream.ReadString32();
            Ability6 = (Ability) new SerializedComponent(assetFile, stream).LoadMonoBehaviourChild();
            SpritePath6 = stream.ReadString32();
            BotDifficultyAbilityModSets = new SerializedVector<SerializedComponent>();
            BotDifficultyAbilityModSets.DeserializeAsset(assetFile, stream);
            CompsToInspectInAbilityKitInspector = new SerializedVector<SerializedComponent>();
            CompsToInspectInAbilityKitInspector.DeserializeAsset(assetFile, stream);
            SequenceDirNameOverride = stream.ReadString32();
            AbilitySetupNotes = stream.ReadString32();
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            Log.Print(LogType.Game, $"AbilityData::OnSerialize: Dirty:{syncVarDirtyBits}, Force all: {forceAll}");
            if (forceAll)
            {
                SyncListInt.WriteInstance(writer, _cooldownsSync);
                SyncListInt.WriteInstance(writer, _consumedStockCount);
                SyncListInt.WriteInstance(writer, _stockRefreshCountdowns);
                SyncListInt.WriteInstance(writer, _currentCardIds);
                writer.Write((int) _selectedActionForTargeting);
                return true;
            }

            bool flag = false;
            if (((int) syncVarDirtyBits & 1) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, _cooldownsSync);
            }

            if (((int) syncVarDirtyBits & 2) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, _consumedStockCount);
            }

            if (((int) syncVarDirtyBits & 4) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, _stockRefreshCountdowns);
            }

            if (((int) syncVarDirtyBits & 8) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, _currentCardIds);
            }

            if (((int) syncVarDirtyBits & 16) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write((int) _selectedActionForTargeting);
            }

            if (!flag)
                writer.WritePackedUInt32(syncVarDirtyBits);
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                SyncListInt.ReadReference(reader, _cooldownsSync);
                SyncListInt.ReadReference(reader, _consumedStockCount);
                SyncListInt.ReadReference(reader, _stockRefreshCountdowns);
                SyncListInt.ReadReference(reader, _currentCardIds);
                _selectedActionForTargeting = (ActionType) reader.ReadInt32();
            }
            else
            {
                int num = (int) reader.ReadPackedUInt32();
                if ((num & 1) != 0)
                    SyncListInt.ReadReference(reader, _cooldownsSync);
                if ((num & 2) != 0)
                    SyncListInt.ReadReference(reader, _consumedStockCount);
                if ((num & 4) != 0)
                    SyncListInt.ReadReference(reader, _stockRefreshCountdowns);
                if ((num & 8) != 0)
                    SyncListInt.ReadReference(reader, _currentCardIds);
                if ((num & 16) == 0)
                    return;
                _selectedActionForTargeting = (ActionType) reader.ReadInt32();
            }
        }

        protected static void InvokeSyncListm_cooldownsSync(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList _cooldownsSync called on server.");
            else
                ((AbilityData) obj)._cooldownsSync.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_consumedStockCount(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList _consumedStockCount called on server.");
            else
                ((AbilityData) obj)._consumedStockCount.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_stockRefreshCountdowns(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList _stockRefreshCountdowns called on server.");
            else
                ((AbilityData) obj)._stockRefreshCountdowns.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_currentCardIds(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList _currentCardIds called on server.");
            else
                ((AbilityData) obj)._currentCardIds.HandleMsg(reader);
        }

        public Ability GetAbilityOfActionType(ActionType type)
        {
            Ability ability;
            if (IsChain(type))
            {
                int index = (int) (type - 10);
                ability = index < 0 || index >= m_allChainAbilities.Count
                    ? null
                    : m_allChainAbilities[index];
            }
            else
            {
                int index = (int) type;
                ability = index < 0 || index >= m_abilities.Length
                    ? null
                    : m_abilities[index].ability;
            }

            return ability;
        }

        private void AddToAllChainAbilitiesList(Ability aChainAbility, ActionType parentActionType)
        {
            m_allChainAbilities.Add(aChainAbility);
            m_allChainAbilityParentActionTypes.Add(parentActionType);
        }

        public bool GetQueuedAbilitiesAllowMovement()
        {
            bool result = true;
            ActorTeamSensitiveData teamSensitiveData_authority = this.m_actor.TeamSensitiveData_authority;
            if (teamSensitiveData_authority != null)
            {
                for (int i = 0; i < 14; i++)
                {
                    AbilityData.ActionType actionType = (AbilityData.ActionType)i;
                    if (teamSensitiveData_authority.HasQueuedAction(actionType))
                    {
                        Ability abilityOfActionType = this.GetAbilityOfActionType(actionType);
                        if (abilityOfActionType != null && abilityOfActionType.GetPreventsMovement())
                        {
                            result = false;
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public Ability.MovementAdjustment GetQueuedAbilitiesMovementAdjustType()
        {
            Ability.MovementAdjustment movementAdjustment = Ability.MovementAdjustment.FullMovement;
            ActorTeamSensitiveData teamSensitiveData_authority = this.m_actor.TeamSensitiveData_authority;
            if (teamSensitiveData_authority != null)
            {
                for (int i = 0; i < 14; i++)
                {
                    AbilityData.ActionType actionType = (AbilityData.ActionType)i;
                    if (teamSensitiveData_authority.HasQueuedAction(actionType))
                    {
                        Ability abilityOfActionType = this.GetAbilityOfActionType(actionType);
                        if (abilityOfActionType != null && abilityOfActionType.GetMovementAdjustment() > movementAdjustment)
                        {
                            movementAdjustment = abilityOfActionType.GetMovementAdjustment();
                        }
                    }
                }
            }
            // TODO ZHENEQ
            //SpawnPointManager spawnPointManager = SpawnPointManager.Get();
            //if (spawnPointManager != null && spawnPointManager.m_spawnInDuringMovement && this.m_actor.NextRespawnTurn == GameFlowData.CurrentTurn && GameplayData.m_movementAllowedOnRespawn < movementAdjustment)
            //{
            //    movementAdjustment = GameplayData.m_movementAllowedOnRespawn;
            //}
            return movementAdjustment;
        }

        public float GetQueuedAbilitiesMovementAdjust()
        {
            float result = 0f;
            Ability.MovementAdjustment queuedAbilitiesMovementAdjustType = this.GetQueuedAbilitiesMovementAdjustType();
            if (queuedAbilitiesMovementAdjustType == Ability.MovementAdjustment.ReducedMovement)
            {
                result = -1f * this.m_actor.method_29();
            }
            return result;
        }

        public List<StatusType> GetQueuedAbilitiesOnRequestStatuses()
        {
            List<StatusType> list = new List<StatusType>();
            ActorTeamSensitiveData teamSensitiveData_authority = this.m_actor.TeamSensitiveData_authority;
            if (teamSensitiveData_authority != null)
            {
                for (int i = 0; i < 14; i++)
                {
                    AbilityData.ActionType actionType = (AbilityData.ActionType)i;
                    if (teamSensitiveData_authority.HasQueuedAction(actionType))
                    {
                        Ability abilityOfActionType = this.GetAbilityOfActionType(actionType);
                        if (abilityOfActionType != null)
                        {
                            list.AddRange(abilityOfActionType.GetStatusToApplyWhenRequested());
                        }
                    }
                }
            }
            return list;
        }

        public bool HasPendingStatusFromQueuedAbilities(StatusType status)
        {
            ActorTeamSensitiveData teamSensitiveData_authority = this.m_actor.TeamSensitiveData_authority;
            if (teamSensitiveData_authority != null)
            {
                for (int i = 0; i < 14; i++)
                {
                    AbilityData.ActionType actionType = (AbilityData.ActionType)i;
                    if (teamSensitiveData_authority.HasQueuedAction(actionType))
                    {
                        Ability abilityOfActionType = this.GetAbilityOfActionType(actionType);
                        if (abilityOfActionType != null && abilityOfActionType.GetStatusToApplyWhenRequested().Contains(status))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return false;
        }

        public override string ToString()
        {
            return $"{nameof(AbilityData)}(" +
                   $"{nameof(IgnoreForLocalization)}: {IgnoreForLocalization}, " +
                   $"{nameof(Ability0)}: {Ability0}, " +
                   $"{nameof(SpritePath0)}: {SpritePath0}, " +
                   $"{nameof(Ability1)}: {Ability1}, " +
                   $"{nameof(SpritePath1)}: {SpritePath1}, " +
                   $"{nameof(Ability2)}: {Ability2}, " +
                   $"{nameof(SpritePath2)}: {SpritePath2}, " +
                   $"{nameof(Ability3)}: {Ability3}, " +
                   $"{nameof(SpritePath3)}: {SpritePath3}, " +
                   $"{nameof(Ability4)}: {Ability4}, " +
                   $"{nameof(SpritePath4)}: {SpritePath4}, " +
                   $"{nameof(Ability5)}: {Ability5}, " +
                   $"{nameof(SpritePath5)}: {SpritePath5}, " +
                   $"{nameof(Ability6)}: {Ability6}, " +
                   $"{nameof(SpritePath6)}: {SpritePath6}, " +
                   $"{nameof(BotDifficultyAbilityModSets)}: {BotDifficultyAbilityModSets.Count} entries, " +
                   $"{nameof(CompsToInspectInAbilityKitInspector)}: {CompsToInspectInAbilityKitInspector.Count} entries, " +
                   $"{nameof(SequenceDirNameOverride)}: {SequenceDirNameOverride}, " +
                   $"{nameof(AbilitySetupNotes)}: {AbilitySetupNotes}" +
                   ")";
        }

        public static bool IsChain(ActionType actionType)
        {
            if (actionType >= ActionType.CHAIN_0)
                return actionType <= ActionType.CHAIN_2;
            return false;
        }

        public void MarkAsDirty(DirtyBit bit)
        {
            SetDirtyBit((uint)bit);
        }

        private bool IsBitDirty(uint setBits, DirtyBit bitToTest)
        {
            return ((DirtyBit)setBits & bitToTest) != ~DirtyBit.All;
        }

        public enum DirtyBit : uint
        {
            CooldownsSync = 1,
            ConsumedStockCount = 2,
            StockRefreshCooldowns = 4,
            CurrentCardIds = 8,
            SelectedActionForTargetting = 16,
            All = 4294967295
        }

        public enum ActionType
        {
            INVALID_ACTION = -1, // 0xFFFFFFFF
            ABILITY_0 = 0,
            ABILITY_1 = 1,
            ABILITY_2 = 2,
            ABILITY_3 = 3,
            ABILITY_4 = 4,
            ABILITY_5 = 5,
            ABILITY_6 = 6,
            CARD_0 = 7,
            CARD_1 = 8,
            CARD_2 = 9,
            CHAIN_0 = 10, // 0x0000000A
            CHAIN_1 = 11, // 0x0000000B
            CHAIN_2 = 12, // 0x0000000C
            CHAIN_3 = 13, // 0x0000000D
            NUM_ACTIONS = 14 // 0x0000000E
        }

        public class AbilityEntry
        {
            public Ability ability;
            public string hotkey;
            private int m_cooldownRemaining;

            public int GetCooldownRemaining()
            {
                if (DebugParameters.Get() != null && DebugParameters.Get().GetParameterAsBool("NoCooldowns"))
                    return 0;
                return m_cooldownRemaining;
            }

            public void SetCooldownRemaining(int remaining)
            {
                if (m_cooldownRemaining == remaining)
                    return;
                m_cooldownRemaining = remaining;
            }

            public void Setup(Ability ability)
            {
                this.ability = ability;
            }
        }
    }
}
