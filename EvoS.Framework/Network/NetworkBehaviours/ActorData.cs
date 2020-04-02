using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [SerializedMonoBehaviour("ActorData")]
    public class ActorData : NetworkBehaviour //, IGameEventListener
    {
        public static int s_invalidActorIndex = -1;
        private static int kCmdCmdSetPausedForDebugging = 1605310550;

        /* Asset fields */
        public int PlayerIndex = -1;
        public PlayerData PlayerData;

//        [Separator("Character Type", true)]
        public CharacterType m_characterType;

        public SerializedComponent m_tauntCamSetData;

//        [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
//        [Header("-- Icon: Portrait, OffscreenIndicator, Team Panel --")]
        public string m_aliveHUDIconResourceString;

//        [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
        public string m_deadHUDIconResourceString;

//        [Header("-- Icon: Last Known Position Indicator --")]
//        [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
        public string m_screenIndicatorIconResourceString;

//        [AssetFileSelector("Assets/UI/Textures/Resources/CharacterIcons/", "CharacterIcons/", ".png")]
        public string m_screenIndicatorBWIconResourceString;

//        public PrefabResourceLink m_actorSkinPrefabLink;
        public CharacterVisualInfo m_visualInfo;
        public CharacterAbilityVfxSwapInfo m_abilityVfxSwapInfo;
        public CharacterModInfo m_selectedMods;
        public CharacterAbilityVfxSwapInfo m_selectedAbilityVfxSwaps;
        public CharacterCardInfo m_selectedCards;

        public SerializedVector<int> m_availableTauntIDs;

//        [Header("-- Stats --")]
        public int m_maxHitPoints = 100;
        public int m_hitPointRegen;
        public int m_maxTechPoints = 100;
        public int m_techPointRegen = 10;
        public int m_techPointsOnSpawn = 100;
        public int m_techPointsOnRespawn = 100;
        public float m_maxHorizontalMovement = 8f;
        public float m_postAbilityHorizontalMovement = 5f;
        public int m_maxVerticalUpwardMovement = 1;
        public int m_maxVerticalDownwardMovement = 2;
        public float m_sightRange = 10f;
        public float m_runSpeed = 8f;
        public float m_vaultSpeed = 4f;

        //        [Tooltip("The speed the actor travels when being knocked back by any ability.")]
        public float m_knockbackSpeed = 8f;

//        [Header("-- Audio Events --")]
        public string m_onDeathAudioEvent = string.Empty;

//        [Header("-- Additional Network Objects to call Register Prefab on")]
//        public SerializedVector<GameObject> m_additionalNetworkObjectsToRegister;
        public SerializedVector<SerializedComponent> m_additionalNetworkObjectsToRegister;
        /* Asset fields end */

        // MARK START
        public static short s_nextActorIndex;
        public static float s_visibleTimeAfterHit = 1f;
        public static float s_visibleTimeAfterMovementHit = 0.3f;
        public float m_endVisibilityForHitTime = -10000f;

//  private EasedQuaternionNoAccel m_targetRotation = new EasedQuaternionNoAccel(Quaternion.identity);
        private bool m_shouldUpdateLastVisibleToClientThisFrame = true;
        public string m_displayName = "Connecting Player";
        private int m_actorIndex = s_invalidActorIndex;
        private bool m_showInGameHud = true;
        private int m_hitPoints = 1;
        private int m_lastSpawnTurn = -1;
        private int m_nextRespawnTurn = -1;
        private List<BoardSquare> m_trueRespawnSquares = new List<BoardSquare>();
        private List<ActorData> m_lineOfSightVisibleExceptions = new List<ActorData>();

        private SerializeHelper m_serializeHelper = new SerializeHelper();
//        private List<IForceActorOutlineChecker> m_forceShowOutlineCheckers = new List<IForceActorOutlineChecker>();

//        [Separator("Taunt Camera Data Reference", true)]
//        public TauntCameraSet m_tauntCamSetData;
//        private JointPopupProperty m_nameplateJoint;
        public const float MAX_HEIGHT = 2.5f;
        public ActorDataDelegate m_onResolvedHitPoints;
        public bool m_callHandleOnSelectInUpdate;
        public bool m_hideNameplate;
        public bool m_needAddToTeam;
        private bool m_alwaysHideNameplate;
        private ActorBehavior m_actorBehavior;
        private ActorModelData m_actorModelData;
        private ActorModelData m_faceActorModelData;
        private ActorMovement m_actorMovement;
        private ActorStats m_actorStats;
        private ActorStatus m_actorStatus;
        private ActorTargeting m_actorTargeting;
        private ActorTurnSM m_actorTurnSM;
        private ActorCover m_actorCover;
        private ActorVFX m_actorVFX;
        private TimeBank m_timeBank;
        private ActorAdditionalVisionProviders m_additionalVisionProvider;
        private AbilityData m_abilityData;
        private ItemData m_itemData;
        private PassiveData m_passiveData;

//        private CombatText m_combatText;
        private ActorTag m_actorTags;
        private FreelancerStats m_freelancerStats;
        private bool m_setTeam;
        private Team m_team;
        private float m_lastIsVisibleToClientTime;
        private bool m_isVisibleToClientCache;
        private int m_lastVisibleTurnToClient;
        private BoardSquare m_clientLastKnownPosSquare;
        private BoardSquare m_serverLastKnownPosSquare;
        private bool m_addedToUI;
        private int _unresolvedDamage;
        private int _unresolvedHealing;
        private int _unresolvedTechPointGain;
        private int _unresolvedTechPointLoss;
        private int m_serverExpectedHoTTotal;
        private int m_serverExpectedHoTThisTurn;
        private int m_techPoints;
        private int m_reservedTechPoints;
        private bool m_ignoreForEnergyForHit;
        private bool m_ignoreFromAbilityHits;
        private int m_absorbPoints;
        private int m_mechanicPoints;
        private int m_spawnerId;

        private Vector3 m_facingDirAfterMovement;

//        private GameEventManager.EventType m_serverMovementWaitForEvent;
        private BoardSquare m_serverMovementDestination;

        private BoardSquarePathInfo m_serverMovementPath;
        private bool m_disappearingAfterCurrentMovement;
        private BoardSquare m_clientCurrentBoardSquare;
        private BoardSquare m_mostRecentDeathSquare;
        private ActorTeamSensitiveData m_teamSensitiveData_friendly;
        private ActorTeamSensitiveData m_teamSensitiveData_hostile;
        private BoardSquare m_trueMoveFromBoardSquare;
        private BoardSquare m_serverInitialMoveStartSquare;
        private bool m_internalQueuedMovementAllowsAbility;
        private bool m_queuedMovementRequest;
        private bool m_queuedChaseRequest;
        private ActorData m_queuedChaseTarget;
        private bool m_knockbackMoveStarted;
        private BoardSquare m_trueRespawnPositionSquare;
        private GameObject m_respawnPositionFlare;
        private BoardSquare m_respawnFlareVfxSquare;
        private bool m_respawnFlareForSameTeam;
        private bool m_hasBotController;
        private bool m_currentlyVisibleForAbilityCast;
        private bool m_movedForEvade;
        private bool m_serverSuppressInvisibility;
        private uint m_debugSerializeSizeBeforeVisualInfo;

        private uint m_debugSerializeSizeBeforeSpawnSquares;

//        private Rigidbody m_cachedHipJoint;
        private bool m_outOfCombat;
        private bool m_wasUpdatingForConfirmedTargeting;
        private bool m_showingTargetingNumAtFullAlpha;
        private const int c_debugCmdHealthFlag = 0;
        private const int c_debugCmdEnergyFlag = 1;

        private BoardSquare m_initialSpawnSquare;

//        private CharacterResourceLink m_characterResourceLink;
        private static int kCmdCmdSetResolutionSingleStepping = -1306898411;
        private static int kCmdCmdSetResolutionSingleSteppingAdvance = -1612013907;
        private static int kCmdCmdSetDebugToggleParam = -77600279;
        private static int kCmdCmdDebugReslotCards = -967932322;
        private static int kCmdCmdDebugSetAbilityMod = 1885146022;
        private static int kCmdCmdDebugReplaceWithBot = -1932690655;
        private static int kCmdCmdDebugSetHealthOrEnergy = 946344949;
        private static int kRpcRpcOnHitPointsResolved = 189834458;
        private static int kRpcRpcCombatText = -1860175530;
        private static int kRpcRpcApplyAbilityModById = -1097840381;
        private static int kRpcRpcMarkForRecalculateClientVisibility = -701731415;
        private static int kRpcRpcForceLeaveGame = -1193160397;
        private SerializedComponent SerializedPlayerData;
        private SerializedPrefabResourceLink m_actionSkinPrefabLink;

        static ActorData()
        {
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdSetPausedForDebugging, InvokeCmdCmdSetPausedForDebugging);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdSetResolutionSingleStepping,
                InvokeCmdCmdSetResolutionSingleStepping);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdSetResolutionSingleSteppingAdvance,
                InvokeCmdCmdSetResolutionSingleSteppingAdvance);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdSetDebugToggleParam, InvokeCmdCmdSetDebugToggleParam);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdDebugReslotCards, InvokeCmdCmdDebugReslotCards);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdDebugSetAbilityMod, InvokeCmdCmdDebugSetAbilityMod);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdDebugReplaceWithBot, InvokeCmdCmdDebugReplaceWithBot);
            RegisterCommandDelegate(typeof(ActorData), kCmdCmdDebugSetHealthOrEnergy,
                InvokeCmdCmdDebugSetHealthOrEnergy);
            RegisterRpcDelegate(typeof(ActorData), kRpcRpcOnHitPointsResolved, InvokeRpcRpcOnHitPointsResolved);
            RegisterRpcDelegate(typeof(ActorData), kRpcRpcCombatText, InvokeRpcRpcCombatText);
            RegisterRpcDelegate(typeof(ActorData), kRpcRpcApplyAbilityModById, InvokeRpcRpcApplyAbilityModById);
            RegisterRpcDelegate(typeof(ActorData), kRpcRpcMarkForRecalculateClientVisibility,
                InvokeRpcRpcMarkForRecalculateClientVisibility);
            RegisterRpcDelegate(typeof(ActorData), kRpcRpcForceLeaveGame, InvokeRpcRpcForceLeaveGame);
        }

        public static int Layer { get; private set; }

        public static int Layer_Mask { get; private set; }

        public bool ForceDisplayTargetHighlight { get; set; }

        public Vector3 PreviousBoardSquarePosition { get; private set; }

        public BoardSquare ClientLastKnownPosSquare
        {
            get => m_clientLastKnownPosSquare;
            private set
            {
                if (m_clientLastKnownPosSquare != value)
                {
                    if (ActorDebugUtils.Get() != null && ActorDebugUtils.Get()
                            .ShowingCategory(ActorDebugUtils.DebugCategory.LastKnownPosition, false))
                        Log.Print(LogType.Warning, (method_95() + "----Setting ClientLastKnownPosSquare from " +
                                                    ((m_clientLastKnownPosSquare == null)
                                                        ? "null"
                                                        : m_clientLastKnownPosSquare.ToString()) + " to " +
                                                    ((value == null) ? "null" : value.ToString())));
                    m_clientLastKnownPosSquare = value;
                }

                m_shouldUpdateLastVisibleToClientThisFrame = false;
            }
        }

        public BoardSquare ServerLastKnownPosSquare
        {
            get => m_serverLastKnownPosSquare;
            set
            {
                if (m_serverLastKnownPosSquare == value)
                    return;
                if (ActorDebugUtils.Get() != null && ActorDebugUtils.Get()
                        .ShowingCategory(ActorDebugUtils.DebugCategory.LastKnownPosition, false))
                    Log.Print(LogType.Warning, (method_95() + "=====ServerLastKnownPosSquare from " +
                                                ((m_serverLastKnownPosSquare == null)
                                                    ? "null"
                                                    : m_serverLastKnownPosSquare.ToString()) + " to " +
                                                ((value == null) ? "null" : value.ToString())));
                m_serverLastKnownPosSquare = value;
            }
        }

        public int method_0()
        {
            return m_lastVisibleTurnToClient;
        }

        public Vector3 method_1()
        {
            return ClientLastKnownPosSquare?.transform.position ?? Vector3.Zero;
        }

        public Vector3 method_2()
        {
            return ServerLastKnownPosSquare?.transform.position ?? Vector3.Zero;
        }

        public void ActorData_OnActorMoved(
            BoardSquare movementSquare,
            bool visibleToEnemies,
            bool updateLastKnownPos)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                return;
            if (updateLastKnownPos)
            {
                ClientLastKnownPosSquare = movementSquare;
                m_lastVisibleTurnToClient = GameFlowData.CurrentTurn;
            }

            m_shouldUpdateLastVisibleToClientThisFrame = false;
        }

        public ActorBehavior method_3()
        {
            return m_actorBehavior;
        }

        public ActorModelData method_4()
        {
            return m_actorModelData;
        }

        public ActorModelData method_5()
        {
            return m_faceActorModelData;
        }

//  public Renderer method_6()
//  {
//    return m_actorModelData.GetModelRenderer(0);
//  }

//  public void EnableRendererAndUpdateVisibility()
//  {
//    if (m_actorModelData == null)
//      return;
//    m_actorModelData.EnableRendererAndUpdateVisibility();
//  }

        public ItemData method_7()
        {
            return m_itemData;
        }

        public AbilityData GetAbilityData()
        {
            return method_8();
        }

        public AbilityData method_8()
        {
            return m_abilityData;
        }

        public ActorMovement method_9()
        {
            return m_actorMovement;
        }

        public ActorStats getActorStats()
        {
            return method_10();
        }

        public ActorStats method_10()
        {
            return m_actorStats;
        }

        public ActorStatus GetActorStatus()
        {
            return method_11();
        }

        public ActorStatus method_11()
        {
            return m_actorStatus;
        }

        public ActorController method_12()
        {
            return GetComponent<ActorController>();
        }

        public ActorTargeting method_13()
        {
            return m_actorTargeting;
        }

        public FreelancerStats method_14()
        {
            return m_freelancerStats;
        }

//  public NPCBrain method_15()
//  {
//    if (method_12() != null)
//    {
//      foreach (NPCBrain component in GetComponents<NPCBrain>())
//      {
//        if (component.enabled)
//          return component;
//      }
//    }
//    return (NPCBrain) null;
//  }

        public ActorTurnSM method_16()
        {
            return m_actorTurnSM;
        }

        public ActorCover method_17()
        {
            return m_actorCover;
        }

        public ActorVFX method_18()
        {
            return m_actorVFX;
        }

        public TimeBank method_19()
        {
            return m_timeBank;
        }

//        public FogOfWar method_20()
//        {
//            return PlayerData.GetFogOfWar();
//        }

        public ActorAdditionalVisionProviders method_21()
        {
            return m_additionalVisionProvider;
        }

        public PassiveData method_22()
        {
            return m_passiveData;
        }

        public string DisplayName => m_displayName;

        public string method_23()
        {
            if (HasBotController && method_70() == 0L &&
                (m_characterType != CharacterType.None && !method_35().m_botsMasqueradeAsHumans))
                return m_characterType.ToString();
//                return StringUtil.TR_CharacterName(m_characterType.ToString());
//            if (CollectTheCoins != null)
//                return $"{m_displayName} ({CollectTheCoins.GetCoinsForActor_Client(this)}c)";
            return m_displayName;
        }

        public void UpdateDisplayName(string newDisplayName)
        {
            m_displayName = newDisplayName;
        }

//  public Sprite method_24()
//  {
//    return (Sprite) Resources.Load(m_aliveHUDIconResourceString, typeof (Sprite));
//  }
//
//  public Sprite method_25()
//  {
//    return (Sprite) Resources.Load(m_deadHUDIconResourceString, typeof (Sprite));
//  }
//
//  public Sprite method_26()
//  {
//    return (Sprite) Resources.Load(m_screenIndicatorIconResourceString, typeof (Sprite));
//  }
//
//  public Sprite method_27()
//  {
//    return (Sprite) Resources.Load(m_screenIndicatorBWIconResourceString, typeof (Sprite));
//  }

        public string method_28()
        {
            return gameObject.Name.Replace("(Clone)", string.Empty);
        }

        public int ActorIndex
        {
            get => m_actorIndex;
            set => m_actorIndex = value;
        }

        public bool ShowInGameGUI
        {
            get => m_showInGameHud;
            set => m_showInGameHud = value;
        }

        public float GetAbilityMovementCost()
        {
            return method_29();
        }

        public float method_29()
        {
            return m_maxHorizontalMovement - m_postAbilityHorizontalMovement;
        }

        public int method_30()
        {
            int num = 1;
            ActorStats actorStats = m_actorStats;
            if (actorStats != null)
                num = actorStats.GetModifiedStatInt(StatType.MaxHitPoints);
            return num;
        }

        public void OnMaxHitPointsChanged(int previousMax)
        {
            if (method_38())
                return;
            HitPoints = Mathf.RoundToInt(method_30() * (HitPoints / (float) previousMax));
        }

        public float method_31()
        {
            return HitPoints / (float) method_30();
        }

        public int method_32()
        {
            int num = 0;
            ActorStats actorStats = m_actorStats;
            if (actorStats != null)
                num = actorStats.GetModifiedStatInt(StatType.HitPointRegen) + Mathf.RoundToInt(
                          actorStats.GetModifiedStatFloat(StatType.HitPointRegenPercentOfMax) *
                          actorStats.GetModifiedStatFloat(StatType.MaxHitPoints));
            if (GameplayMutators != null)
                num = Mathf.RoundToInt(num * GameplayMutators.GetPassiveHpRegenMultiplier());
            return num;
        }

        public int method_33()
        {
            int num = 1;
            ActorStats actorStats = m_actorStats;
            if (actorStats != null)
                num = actorStats.GetModifiedStatInt(StatType.MaxTechPoints);
            return num;
        }

        public void OnMaxTechPointsChanged(int previousMax)
        {
            if (method_38())
                return;
            int b = method_33();
            if (b > previousMax)
            {
                TechPoints += b - previousMax;
            }
            else
            {
                if (previousMax <= b)
                    return;
                int techPoints = TechPoints;
                TechPoints = Mathf.Min(TechPoints, b);
                if (techPoints - TechPoints != 0)
                    ;
            }
        }

        public void TriggerVisibilityForHit(bool movementHit, bool updateClientLastKnownPos = true)
        {
            m_endVisibilityForHitTime = !movementHit
                ? Time.time + s_visibleTimeAfterHit
                : Time.time + s_visibleTimeAfterMovementHit;
            ForceUpdateIsVisibleToClientCache();
            if (!updateClientLastKnownPos || method_74() == null)
                return;
            ClientLastKnownPosSquare = method_73();
            m_lastVisibleTurnToClient = GameFlowData.CurrentTurn;
        }

        private void UpdateClientLastKnownPosSquare()
        {
            if (m_shouldUpdateLastVisibleToClientThisFrame && ClientLastKnownPosSquare != method_74())
            {
                Team team = GameFlowData == null || GameFlowData.activeOwnedActorData == null
                    ? Team.Invalid
                    : GameFlowData.activeOwnedActorData.method_76();
//                bool flag1 = GameFlowData != null && GameFlowData.IsInResolveState();
//                bool flag2 = method_73() == method_74() && !method_9().AmMoving() &&
//                             !method_9().IsYetToCompleteGameplayPath();
//                bool flag3 = method_76() != team;
//                if (flag1 && flag2 && (flag3 && method_48()))
//                {
//                    ForceUpdateIsVisibleToClientCache();
//                    if (method_48())
//                    {
//                        ClientLastKnownPosSquare = method_74();
//                        m_lastVisibleTurnToClient = GameFlowData.CurrentTurn;
//                    }
//                }
            }

            m_shouldUpdateLastVisibleToClientThisFrame = true;
        }

        public Player method_34()
        {
            return PlayerData.GetPlayer();
        }

        public PlayerDetails method_35()
        {
            if (PlayerData != null && GameFlow != null &&
                (GameFlow.playerDetails != null &&
                 GameFlow.playerDetails.ContainsKey(PlayerData.GetPlayer())))
                return GameFlow.playerDetails[PlayerData.GetPlayer()];
            return null;
        }

        public void SetupAbilityModOnReconnect()
        {
            foreach (ActorData actor in GameFlowData.GetActors())
            {
                if (actor != null && actor.method_8() != null)
                {
//                    for (int index = 0; index <= 4; ++index)
//                        actor.ApplyAbilityModById(index, actor.m_selectedMods.GetModForAbility(index));
//        ActorTargeting component = actor.GetComponent<ActorTargeting>();
//        if (component != null)
//          component.MarkForForceRedraw();
                }
            }
        }

        public void SetupForRespawnOnReconnect()
        {
            if (!method_44() || GameFlowData == null ||
                ServerClientUtils.GetCurrentActionPhase() >= ActionBufferPhase.Movement &&
                ServerClientUtils.GetCurrentActionPhase() <= ActionBufferPhase.MovementWait)
                return;
//    ActorModelData actorModelData = method_4();
//    if (actorModelData != null)
//      actorModelData.DisableAndHideRenderers();
            if (RespawnPickedPositionSquare == null)
                return;
            ShowRespawnFlare(RespawnPickedPositionSquare, true);
        }

//        public void SetupAbilityMods(CharacterModInfo characterMods)
//        {
//            m_selectedMods = characterMods;
//            AbilityData abilityData = method_8();
//            List<Ability> abilitiesAsList = abilityData.GetAbilitiesAsList();
//            int abilityIndex = 0;
//            foreach (Ability ability in abilitiesAsList)
//            {
//                int abilityScopeId;
//                if (GameManager.GameConfig.GameType == GameType.Tutorial)
//                {
//                    AbilityMod defaultModForAbility = AbilityModManager.Get().GetDefaultModForAbility(ability);
//                    abilityScopeId = !(defaultModForAbility != null) ? -1 : defaultModForAbility.m_abilityScopeId;
//                }
//                else
//                    abilityScopeId = m_selectedMods.GetModForAbility(abilityIndex);
//
//                AbilityData.ActionType actionTypeOfAbility = abilityData.GetActionTypeOfAbility(ability);
//                if (actionTypeOfAbility != AbilityData.ActionType.INVALID_ACTION && abilityScopeId > 0)
//                    ApplyAbilityModById((int) actionTypeOfAbility, abilityScopeId);
//                ++abilityIndex;
//            }
//        }

        public void UpdateServerLastVisibleTurn()
        {
        }

        public void SynchClientLastKnownPosToServerLastKnownPos()
        {
            if (!EvoSGameConfig.NetworkIsClient || ClientLastKnownPosSquare == ServerLastKnownPosSquare)
                return;
            ClientLastKnownPosSquare = ServerLastKnownPosSquare;
            if (GameFlowData.activeOwnedActorData == null ||
                method_76() == GameFlowData.activeOwnedActorData.method_76())
                ;
        }

        public int method_36()
        {
            int num = 0;
            ActorStats actorStats = m_actorStats;
            if (actorStats != null)
                num = actorStats.GetModifiedStatInt(StatType.TechPointRegen);
            if (GameplayMutators != null)
                num = Mathf.RoundToInt(num * GameplayMutators.GetPassiveEnergyRegenMultiplier());
            return num;
        }

        public float method_37()
        {
            float num = 1f;
            if (m_actorStats != null)
                num = m_actorStats.GetModifiedStatFloat(StatType.SightRange);
            if (m_actorStatus != null && m_actorStatus.HasStatus(StatusType.Blind))
                num = 0.1f;
            return num;
        }

        public int HitPoints
        {
            get => m_hitPoints;
            private set
            {
                MatchLogger?.Log(ToString() + " HitPoints.set " + value + ", old: " + HitPoints);
                bool flag = m_hitPoints > 0;
                m_hitPoints = !EvoSGameConfig.NetworkIsServer ? value : Mathf.Clamp(value, 0, method_30());
                int num = 0;
                if (GameFlowData != null)
                    num = GameFlowData.CurrentTurn;
                if (flag && m_hitPoints == 0)
                {
                    if (GameFlowData != null)
                        LastDeathTurn = GameFlowData.CurrentTurn;
                    LastDeathPosition = gameObject.transform.position;
                    NextRespawnTurn = -1;
//                    FogOfWar.CalculateFogOfWarForTeam(method_76());
                    if (method_74() != null)
                        SetMostRecentDeathSquare(method_74());
//                    gameObject.SendMessage("OnDeath");
//                    if (GameFlowData != null)
//                        GameFlowData.NotifyOnActorDeath(this);
                    UnoccupyCurrentBoardSquare();
                    SetCurrentBoardSquare(null);
                    ClientLastKnownPosSquare = null;
                    ServerLastKnownPosSquare = null;
                }
                else
                {
                    if (flag || m_hitPoints <= 0 || LastDeathTurn <= 0)
                        return;
//                    gameObject.SendMessage("OnRespawn");
                    m_lastVisibleTurnToClient = 0;
                    if (!EvoSGameConfig.NetworkIsServer)
                        return;
                    m_teamSensitiveData_friendly?.MarkAsRespawning();
                    m_teamSensitiveData_hostile?.MarkAsRespawning();
                    if (num > 0)
                        ;
                }
            }
        }

        public int UnresolvedDamage
        {
            get => _unresolvedDamage;
            set
            {
                if (MatchLogger != null)
                    MatchLogger.Log(ToString() + " UnresolvedDamage.set " + value + ", old: " + UnresolvedDamage);
                if (_unresolvedDamage == value)
                    return;
                _unresolvedDamage = value;
                ClientUnresolvedDamage = 0;
            }
        }

        public int UnresolvedHealing
        {
            get => _unresolvedHealing;
            set
            {
                if (MatchLogger != null)
                    MatchLogger.Log(ToString() + " UnresolvedHealing.set " + value + ", old: " + UnresolvedHealing);
                if (_unresolvedHealing == value)
                    return;
                _unresolvedHealing = value;
                ClientUnresolvedHealing = 0;
            }
        }

        public int UnresolvedTechPointGain
        {
            get => _unresolvedTechPointGain;
            set
            {
                MatchLogger?.Log($"{ToString()} UnresolvedTechPointGain.set {value}, old: {UnresolvedTechPointGain}");
                if (_unresolvedTechPointGain == value)
                    return;
                _unresolvedTechPointGain = value;
                ClientUnresolvedTechPointGain = 0;
            }
        }

        public int UnresolvedTechPointLoss
        {
            get => _unresolvedTechPointLoss;
            set
            {
                MatchLogger?.Log($"{ToString()} UnresolvedTechPointLoss.set {value}, old: {UnresolvedTechPointLoss}");
                if (_unresolvedTechPointLoss == value)
                    return;
                _unresolvedTechPointLoss = value;
                ClientUnresolvedTechPointLoss = 0;
            }
        }

        public int ExpectedHoTTotal
        {
            get => m_serverExpectedHoTTotal;
            set
            {
                if (m_serverExpectedHoTTotal == value)
                    return;
                m_serverExpectedHoTTotal = value;
                ClientExpectedHoTTotalAdjust = 0;
            }
        }

        public int ExpectedHoTThisTurn
        {
            get => m_serverExpectedHoTThisTurn;
            set
            {
                if (m_serverExpectedHoTThisTurn == value)
                    return;
                m_serverExpectedHoTThisTurn = value;
            }
        }

        public int ClientUnresolvedDamage { get; set; }

        public int ClientUnresolvedHealing { get; set; }

        public int ClientUnresolvedTechPointGain { get; set; }

        public int ClientUnresolvedTechPointLoss { get; set; }

        public int ClientUnresolvedAbsorb { get; set; }

        public int ClientReservedTechPoints { get; set; }

        public int ClientExpectedHoTTotalAdjust { get; set; }

        public int ClientAppliedHoTThisTurn { get; set; }

        public int TechPoints
        {
            get
            {
                if (DebugParameters.Get() != null && DebugParameters.Get().GetParameterAsBool("InfiniteTP"))
                    return method_33();
                return m_techPoints;
            }
            private set
            {
                if (EvoSGameConfig.NetworkIsServer)
                    m_techPoints = Mathf.Clamp(value, 0, method_33());
                else
                    m_techPoints = value;
            }
        }

        public void SetTechPoints(int value, bool combatText = false, ActorData caster = null,
            string sourceName = null)
        {
            int max = method_33();
            value = Mathf.Clamp(value, 0, max);
            int healAmount = value - TechPoints;
            TechPoints = value;
            if (!combatText || sourceName == null || healAmount == 0)
                return;
            DoTechPointsLogAndCombatText(caster, this, sourceName, healAmount);
        }

        private void DoTechPointsLogAndCombatText(
            ActorData caster,
            ActorData target,
            string sourceName,
            int healAmount)
        {
            var flag = healAmount >= 0;
            var combatText = $"{healAmount}";
            var str1 = caster != null ? $"{caster.DisplayName}'s " : string.Empty;
            string str2;
            if (flag)
                str2 = string.Format("{0}{1} adds {3} Energy to {2}", str1, sourceName, target.DisplayName, healAmount);
            else
                str2 = string.Format("{0}{1} removes {3} Energy from {2}", str1, sourceName, target.DisplayName,
                    healAmount);
            var logText = str2;
            target.CallRpcCombatText(combatText, logText,
                !flag ? CombatTextCategory.TP_Damage : CombatTextCategory.TP_Recovery, BuffIconToDisplay.None);
        }

        public void DoCheatLogAndCombatText(string cheatName)
        {
            var actorData = this;
            var combatText = $"{cheatName}";
            var logText = $"{actorData.DisplayName} used cheat: {cheatName}";
            actorData.CallRpcCombatText(combatText, logText, CombatTextCategory.Other, BuffIconToDisplay.None);
        }

        public void SetHitPoints(int value)
        {
            HitPoints = value;
        }

        public void SetAbsorbPoints(int value)
        {
            AbsorbPoints = value;
        }

        public int ReservedTechPoints
        {
            get => m_reservedTechPoints;
            set
            {
                if (m_reservedTechPoints == value)
                    return;
                m_reservedTechPoints = value;
                ClientReservedTechPoints = 0;
            }
        }

        public bool IsDead() => HitPoints == 0;

        public bool method_38()
        {
            return HitPoints == 0;
        }

        public bool IgnoreForEnergyOnHit
        {
            get => m_ignoreForEnergyForHit;
            set
            {
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                m_ignoreForEnergyForHit = value;
            }
        }

        public bool IgnoreForAbilityHits
        {
            get => m_ignoreFromAbilityHits;
            set
            {
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                m_ignoreFromAbilityHits = value;
            }
        }

        public int AbsorbPoints
        {
            get => m_absorbPoints;
            private set
            {
                if (m_absorbPoints == value)
                    return;
                m_absorbPoints = !EvoSGameConfig.NetworkIsServer ? Mathf.Max(value, 0) : Mathf.Max(value, 0);
                ClientUnresolvedAbsorb = 0;
            }
        }

        public int MechanicPoints
        {
            get => m_mechanicPoints;
            set
            {
                if (m_mechanicPoints == value)
                    return;
                m_mechanicPoints = Mathf.Max(value, 0);
            }
        }

        public int SpawnerId
        {
            get => m_spawnerId;
            set => m_spawnerId = value;
        }

        public bool DisappearingAfterCurrentMovement => m_disappearingAfterCurrentMovement;

        public BoardSquare CurrentBoardSquare => m_clientCurrentBoardSquare;

        public ActorTeamSensitiveData TeamSensitiveData_authority
        {
            get
            {
                if (m_teamSensitiveData_friendly != null)
                    return m_teamSensitiveData_friendly;
                return m_teamSensitiveData_hostile;
            }
        }

        public ActorTeamSensitiveData method_39()
        {
            if (m_teamSensitiveData_friendly != null)
                return m_teamSensitiveData_friendly;
            if (m_teamSensitiveData_hostile != null)
                return m_teamSensitiveData_hostile;
            return null;
        }

        public BoardSquare MoveFromBoardSquare
        {
            get
            {
                if (EvoSGameConfig.NetworkIsServer)
                    return m_trueMoveFromBoardSquare;
                if (m_teamSensitiveData_friendly != null)
                    return m_teamSensitiveData_friendly.MoveFromBoardSquare;
                return CurrentBoardSquare;
            }
            set
            {
                if (!EvoSGameConfig.NetworkIsServer || !(m_trueMoveFromBoardSquare != value))
                    return;
                m_trueMoveFromBoardSquare = value;
                if (!(m_teamSensitiveData_friendly != null))
                    return;
                m_teamSensitiveData_friendly.MoveFromBoardSquare = value;
            }
        }

        public BoardSquare InitialMoveStartSquare
        {
            get
            {
                if (EvoSGameConfig.NetworkIsServer)
                    return m_serverInitialMoveStartSquare;
                if (m_teamSensitiveData_friendly != null)
                    return m_teamSensitiveData_friendly.InitialMoveStartSquare;
                return CurrentBoardSquare;
            }
            set
            {
                if (!EvoSGameConfig.NetworkIsServer || !(m_serverInitialMoveStartSquare != value))
                    return;
                m_serverInitialMoveStartSquare = value;
                if (method_9() != null)
                    method_9().UpdateSquaresCanMoveTo();
                if (m_teamSensitiveData_friendly == null)
                    return;
                m_teamSensitiveData_friendly.InitialMoveStartSquare = value;
            }
        }

        public float RemainingHorizontalMovement { get; set; }

        public float RemainingMovementWithQueuedAbility { get; set; }

        public bool QueuedMovementAllowsAbility
        {
            get => m_internalQueuedMovementAllowsAbility;
            set => m_internalQueuedMovementAllowsAbility = value;
        }

        public bool KnockbackMoveStarted
        {
            get => m_knockbackMoveStarted;
            set => m_knockbackMoveStarted = value;
        }

        public Vector3 LastDeathPosition { get; private set; }

        public int LastDeathTurn { get; private set; }

        public int NextRespawnTurn
        {
            get => m_nextRespawnTurn;
            set => m_nextRespawnTurn = Mathf.Max(value, LastDeathTurn + 1);
        }

        public List<BoardSquare> respawnSquares
        {
            get
            {
                if (EvoSGameConfig.NetworkIsServer)
                    return m_trueRespawnSquares;
                if (m_teamSensitiveData_friendly != null)
                    return m_teamSensitiveData_friendly.RespawnAvailableSquares;
                return new List<BoardSquare>();
            }
            set
            {
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                m_trueRespawnSquares = value;
                if (m_teamSensitiveData_friendly == null)
                    return;
                m_teamSensitiveData_friendly.RespawnAvailableSquares = value;
            }
        }

        public void ClearRespawnSquares()
        {
            m_trueRespawnSquares.Clear();
            if (m_teamSensitiveData_friendly == null)
                return;
            m_teamSensitiveData_friendly.RespawnAvailableSquares = new List<BoardSquare>();
        }

        public bool method_40(BoardSquare boardSquare_0)
        {
            return false;
        }

        public BoardSquare RespawnPickedPositionSquare
        {
            get
            {
                if (EvoSGameConfig.NetworkIsServer)
                    return m_trueRespawnPositionSquare;
                if (m_teamSensitiveData_friendly != null)
                    return m_teamSensitiveData_friendly.RespawnPickedSquare;
                if (m_teamSensitiveData_hostile != null)
                    return m_teamSensitiveData_hostile.RespawnPickedSquare;
                return null;
            }
            set
            {
                if (!EvoSGameConfig.NetworkIsServer)
                    return;
                m_trueRespawnPositionSquare = value;
                if (m_teamSensitiveData_friendly != null)
                    m_teamSensitiveData_friendly.RespawnPickedSquare =
                        GameFlowData.IsInDecisionState() || GameFlowData.CurrentTurn == NextRespawnTurn
                            ? value
                            : null;
                if (m_teamSensitiveData_hostile == null)
                    return;
                if (GameFlowData.CurrentTurn == NextRespawnTurn)
                {
                    if (method_40(value))
                        m_teamSensitiveData_hostile.RespawnPickedSquare = value;
                    else
                        m_teamSensitiveData_hostile.RespawnPickedSquare = null;
                }
                else
                    m_teamSensitiveData_hostile.RespawnPickedSquare = null;
            }
        }

        public bool HasBotController
        {
            get => m_hasBotController;
            set => m_hasBotController = value;
        }

        public bool VisibleTillEndOfPhase { get; set; }

        public bool CurrentlyVisibleForAbilityCast
        {
            get => m_currentlyVisibleForAbilityCast;
            set
            {
                if (m_currentlyVisibleForAbilityCast == value)
                    return;
                if (ActorDebugUtils.Get() != null && ActorDebugUtils.Get()
                        .ShowingCategory(ActorDebugUtils.DebugCategory.LastKnownPosition, false))
                    Log.Print(LogType.Warning, (method_95() + "Setting visible for ability cast to " + value));
                m_currentlyVisibleForAbilityCast = value;
            }
        }

        public bool MovedForEvade
        {
            get => m_movedForEvade;
            set => m_movedForEvade = value;
        }

        public bool ServerSuppressInvisibility
        {
            get => m_serverSuppressInvisibility;
            set => m_serverSuppressInvisibility = value;
        }

        public void AddLineOfSightVisibleException(ActorData visibleActor)
        {
            m_lineOfSightVisibleExceptions.Add(visibleActor);
//            method_20().MarkForRecalculateVisibility();
        }

        public void RemoveLineOfSightVisibleException(ActorData visibleActor)
        {
            m_lineOfSightVisibleExceptions.Remove(visibleActor);
//            method_20().MarkForRecalculateVisibility();
        }

        public bool method_41(ActorData actorData_0)
        {
            return m_lineOfSightVisibleExceptions.Contains(actorData_0);
        }

        public ReadOnlyCollection<ActorData> LineOfSightVisibleExceptions =>
            m_lineOfSightVisibleExceptions.AsReadOnly();

        public ReadOnlyCollection<BoardSquare> LineOfSightVisibleExceptionSquares
        {
            get
            {
                var boardSquareList = new List<BoardSquare>(m_lineOfSightVisibleExceptions.Count);
                foreach (var visibleException in m_lineOfSightVisibleExceptions)
                    boardSquareList.Add(visibleException.method_74());
                return boardSquareList.AsReadOnly();
            }
        }

        public event Action OnTurnStartDelegates = () => { };

//  public event Action<object, GameObject> OnAnimationEventDelegates = (object_0, gameObject_0) => {};

        public event Action<Ability> OnSelectedAbilityChangedDelegates = ability_0 => { };

        public event Action OnClientQueuedActionChangedDelegates = () => { };

        public void OnClientQueuedActionChanged()
        {
            OnClientQueuedActionChangedDelegates?.Invoke();
        }

        public void OnSelectedAbilityChanged(Ability ability)
        {
            OnSelectedAbilityChangedDelegates?.Invoke(ability);
        }

        public void InitializeModel( /*PrefabResourceLink heroPrefabLink, bool addMasterSkinVfx*/)
        {
//            if (m_faceActorModelData != null)
//                m_faceActorModelData.Setup(this);
//            if (!NPCCoordinator.IsSpawnedNPC(this))
//                return;
//
//            NPCCoordinator.Get().OnActorSpawn(this);
//            if (method_4() == null)
//                return;
//            method_4().ForceUpdateVisibility();
        }

        public override void OnStartServer() // Was Start()
        {
            if (EvoSGameConfig.NetworkIsClient)
            {
//                m_nameplateJoint = new JointPopupProperty();
//                m_nameplateJoint.m_joint = "VFX_name";
//                m_nameplateJoint.Initialize(gameObject);
            }

            if (EvoSGameConfig.NetworkIsServer)
            {
                HitPoints = method_30();
                UnresolvedDamage = 0;
                UnresolvedHealing = 0;
                TechPoints = m_techPointsOnSpawn;
                ReservedTechPoints = 0;
            }

            ClientUnresolvedDamage = 0;
            ClientUnresolvedHealing = 0;
            ClientUnresolvedTechPointGain = 0;
            ClientUnresolvedTechPointLoss = 0;
            ClientUnresolvedAbsorb = 0;
            ClientReservedTechPoints = 0;
            ClientAppliedHoTThisTurn = 0;
//            transform.parent = GameFlowData.GetActorRoot().transform;
            GameFlowData.AddActor(this);
//            EnableRagdoll(false, (ActorModelData.ImpulseInfo) null, false);
//            if (!m_addedToUI && HUD_UI.Get() != null && HUD_UI.Get().m_mainScreenPanel != null)
//            {
//                m_addedToUI = true;
//                HUD_UI.Get().m_mainScreenPanel.m_nameplatePanel.AddActor(this);
//                HUD_UI.Get().m_mainScreenPanel.m_offscreenIndicatorPanel.AddActor(this);
//            }

//            PlayerDetails playerDetails = (PlayerDetails) null;
//            if (!GameFlow.playerDetails.TryGetValue(PlayerData.GetPlayer(), out playerDetails) ||
//                !playerDetails.IsLocal())
//                return;
//            Log.Print(LogType.Game, "ActorData.Start {0} {1}", this, playerDetails);
//            GameFlowData.AddOwnedActorData(this);
        }

//        public override void OnStartLocalPlayer()
//        {
//            Log.Print(LogType.Game, "ActorData.OnStartLocalPlayer {0}", this);
//            GameFlowData.AddOwnedActorData(this);
//            if (!ClientBootstrap.LoadTest)
//                return;
//            CallCmdDebugReplaceWithBot();
//        }

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.EndingGame:
                    s_nextActorIndex = 0;
                    break;
                case GameState.BothTeams_Decision:
                    HandleRagdollOnDecisionStart();
                    int currentTurn = GameFlowData.CurrentTurn;
                    List<ActorData> actors = GameFlowData.GetActors();
                    bool flag = false;
                    foreach (ActorData actorData in actors)
                    {
                        if (actorData != null && !actorData.method_38() &&
                            (actorData.method_4() != null && actorData.method_43()))
                        {
                            Log.Print(LogType.Error, ("Unragdolling undead actor on Turn Tick (" + currentTurn +
                                                      "): " +
                                                      actorData.method_95()));
//                            actorData.EnableRagdoll(false, (ActorModelData.ImpulseInfo) null, false);
                            flag = true;
                        }

//                        if (actorData != null && !actorData.method_38() &&
//                            (actorData.method_74() == null &&
//                             actorData.PlayerIndex != PlayerData.s_invalidPlayerIndex) &&
//                            (EvoSGameConfig.NetworkIsClient && !EvoSGameConfig.NetworkIsServer &&
//                             GameFlowData.LocalPlayerData.IsViewingTeam(actorData.method_76())))
//                        {
//                            Log.Print(LogType.Error, ("On client, living friendly-to-client actor " +
//                                                       actorData.method_95() +
//                                                       " has null square on Turn Tick"));
//                            flag = true;
//                        }
                    }

                    if (!EvoSGameConfig.NetworkIsServer || flag)
                        break;
                    break;
                case GameState.BothTeams_Resolve:
                    using (var enumerator = GameFlowData.GetActors().GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;
                            if (current == null) continue;
                            if (current.method_4() != null)
                            {
//                                    Animator animator = current.method_42();
//                                    if (animator != null && current.method_4().HasTurnStartParameter())
//                                        animator.SetBool("TurnStart", false);
                            }

                            if (current.GetComponent<LineData>() != null)
                                current.GetComponent<LineData>().OnResolveStart();
//                                if (HUD_UI.Get() != null)
//                                    HUD_UI.Get().m_mainScreenPanel.m_nameplatePanel
//                                        .UpdateNameplateUntargeted(current, false);
                        }

                        break;
                    }
            }
        }

        private static void HandleRagdollOnDecisionStart()
        {
//            List<ActorData> actors = GameFlowData.GetActors();
//            for (int index = 0; index < actors.Count; ++index)
//            {
//                ActorData actor = actors[index];
//                if (actor != null && actor.method_38() &&
//                    (actor.LastDeathTurn != GameFlowData.CurrentTurn && !actor.method_43()) &&
//                    actor.NextRespawnTurn != GameFlowData.CurrentTurn)
//                    actor.DoVisualDeath(Sequence.CreateImpulseInfoWithActorForward(actor));
//            }
        }

//        public Animator method_42()
//        {
//// server noop
//            return null;
//        }

        public void PlayDamageReactionAnim(string customDamageReactTriggerName)
        {
// server noop
        }

        public bool method_43()
        {
//            Animator animator = method_42();
//            if (animator == null)
            return true;
//            return !animator.enabled;
        }

        public void DoVisualDeath(ActorModelData.ImpulseInfo impulseInfo)
        {
            // server noop
        }

        private void EnableRagdoll(
            bool ragDollOn,
            ActorModelData.ImpulseInfo impulseInfo = null,
            bool isDebugRagdoll = false)
        {
// server noop
        }

        public void OnReplayRestart()
        {
//            EnableRendererAndUpdateVisibility();
            EnableRagdoll(false);
            ShowRespawnFlare(null, false);
        }

        public void OnRespawnTeleport()
        {
//            EnableRagdoll(false, (ActorModelData.ImpulseInfo) null, false);
//            if (!(this == GameFlowData.activeOwnedActorData) || !(SpawnPointManager.Get() != null) ||
//                !SpawnPointManager.Get().m_spawnInDuringMovement)
//                return;
//            InterfaceManager.Get().DisplayAlert(StringUtil.smethod_0("PostRespawnMovement", "Global"),
//                BoardSquare.s_respawnOptionHighlightColor, 60f, true, 0);
        }

        private void OnRespawn()
        {
//            EnableRagdoll(false, (ActorModelData.ImpulseInfo) null, false);
            ActorModelData actorModelData = method_4();
//            if (actorModelData != null)
//                actorModelData.ForceUpdateVisibility();
//            if (!EvoSGameConfig.NetworkIsServer && NPCCoordinator.IsSpawnedNPC(this))
//                NPCCoordinator.Get().OnActorSpawn(this);
            GameEventManager.FireEvent(GameEventManager.EventType.CharacterRespawn,
                new GameEventManager.CharacterRespawnEventArgs
                {
                    respawningCharacter = this
                });
//            if (GameFlowData.activeOwnedActorData == this)
//                CameraManager.Get().SetTargetObject(gameObject, CameraManager.CameraTargetReason.ClientActorRespawned);
            m_lastSpawnTurn = GameFlowData.CurrentTurn;
        }

        public bool method_44()
        {
//            if (!method_38() && NextRespawnTurn > 0 &&
//                (NextRespawnTurn == GameFlowData.CurrentTurn && SpawnPointManager.Get() != null))
//                return SpawnPointManager.Get().m_spawnInDuringMovement;
            return false;
        }

        private void OnDestroy()
        {
            if (GameFlowData != null)
                GameFlowData.RemoveReferencesToDestroyedActor(this);

            if (GameFlowData != null)
            {
                GameFlowData.s_onGameStateChanged -= OnGameStateChanged;
            }

            m_actorModelData = null;
//            GameEventManager.RemoveListener(this, GameEventManager.EventType.GametimeScaleChange);
        }

        private void Update()
        {
            if (m_needAddToTeam && GameFlowData != null)
            {
                m_needAddToTeam = false;
                GameFlowData.AddToTeam(this);
//                TeamStatusDisplay.GetTeamStatusDisplay().RebuildTeamDisplay();
            }

            if (EvoSGameConfig.NetworkIsClient)
                UpdateClientLastKnownPosSquare();
//            if ((double) Quaternion.Angle(transform.localRotation,
//                    (Quaternion) ((Eased<Quaternion>) m_targetRotation)) > 0.00999999977648258)
//                transform.localRotation = (Quaternion) ((Eased<Quaternion>) m_targetRotation);
            if (m_callHandleOnSelectInUpdate)
            {
                HandleOnSelect();
                m_callHandleOnSelectInUpdate = false;
            }

            if (EvoSGameConfig.NetworkIsServer)
                SetDirtyBit(1U);
//            if (m_addedToUI || !(HUD_UI.Get() != null))
//                return;
//            m_addedToUI = true;
//            HUD_UI.Get().m_mainScreenPanel.m_nameplatePanel.AddActor(this);
//            HUD_UI.Get().m_mainScreenPanel.m_offscreenIndicatorPanel.AddActor(this);
        }

        public bool method_45()
        {
            int regionIndex = method_46();
            return regionIndex != -1 && BrushCoordinator.IsRegionFunctioning(regionIndex);
        }

        public int method_46()
        {
            var num = -1;
            var boardSquare = method_73();
//            if (boardSquare != null && boardSquare.method_4())
//                num = boardSquare.BrushRegion;
            return num;
        }

        public bool method_47()
        {
            if (m_hideNameplate || m_alwaysHideNameplate || (!ShowInGameGUI || !method_48()) || method_43())
                return false;
//            if (method_4() != null)
//                return method_4().IsVisibleToClient();
            return true;
        }

        public void SetNameplateAlwaysInvisible(bool alwaysHide)
        {
            m_alwaysHideNameplate = alwaysHide;
        }

//        private bool CalculateIsActorVisibleToClient()
//        {
//            bool flag1 = false;
//            if (GameFlowData != null && GameFlowData.LocalPlayerData != null)
//            {
//                PlayerData localPlayerData = GameFlowData.LocalPlayerData;
//                ActorData activeOwnedActorData = GameFlowData.activeOwnedActorData;
//                if (DebugParameters.Get() != null && DebugParameters.Get().GetParameterAsBool("AllCharactersVisible"))
//                    flag1 = true;
//                else if (GameFlowData.gameState == GameState.Deployment)
//                    flag1 = true;
//                else if (activeOwnedActorData != null && activeOwnedActorData.method_76() == method_76())
//                    flag1 = true;
//                else if (activeOwnedActorData == null && localPlayerData.IsViewingTeam(method_76()))
//                    flag1 = true;
//                else if (m_endVisibilityForHitTime > (double) Time.time)
//                    flag1 = true;
//                else if (m_actorModelData != null && m_actorModelData.IsInCinematicCam())
//                    flag1 = true;
//                else if (CurrentlyVisibleForAbilityCast)
//                    flag1 = true;
//                else if (m_disappearingAfterCurrentMovement && CurrentBoardSquare == null && !method_9().AmMoving())
//                {
//                    flag1 = false;
//                }
//                else
//                {
//                    bool flag2 = method_50(localPlayerData, false);
//                    bool flag3 = method_51(localPlayerData, false);
//                    flag1 = flag2 || !flag3 && (!(FogOfWar.GetClientFog() == null) &&
//                                                FogOfWar.GetClientFog().IsVisible(method_73()));
//                }
//            }
//
//            return flag1;
//        }

        public void ForceUpdateActorModelVisibility()
        {
            if (!EvoSGameConfig.NetworkIsClient || m_actorModelData == null)
                return;
//            m_actorModelData.ForceUpdateVisibility();
        }

        public void ForceUpdateIsVisibleToClientCache()
        {
            m_lastIsVisibleToClientTime = 0.0f;
            UpdateIsVisibleToClientCache();
        }

        private void UpdateIsVisibleToClientCache()
        {
            if (m_lastIsVisibleToClientTime >= (double) Time.time)
                return;
            m_lastIsVisibleToClientTime = Time.time;
//            m_isVisibleToClientCache = CalculateIsActorVisibleToClient();
        }

        public bool method_48()
        {
            UpdateIsVisibleToClientCache();
            return m_isVisibleToClientCache;
        }

        public bool method_49(ActorData actorData_0)
        {
//            bool flag1 = false;
//            if (GameFlowData.IsActorDataOwned(this) && method_76() == actorData_0.method_76())
//                flag1 = true;
//            else if (m_endVisibilityForHitTime > (double) Time.time)
//            {
//                flag1 = true;
//            }
//            else
//            {
//                ActorData actorData = actorData_0;
//                bool flag2 = method_50(actorData.PlayerData);
//                bool flag3 = method_51(actorData.PlayerData);
//                if (flag2)
//                    flag1 = true;
//                else if (flag3)
//                    flag1 = false;
//                else if ((bool) (actorData_0.method_20()))
//                    flag1 = actorData_0.method_20().IsVisible(method_73());
//            }
//
//            return flag1;
            return false;
        }

        public bool method_50(PlayerData playerData_0, bool bool_0 = true)
        {
//            bool flag = false;
//            return playerData_0 != null &&
//                   (method_11().HasStatus(StatusType.Revealed, bool_0) &&
//                    method_76() != playerData_0.GetTeamViewing() ||
//                    (!EvoSGameConfig.NetworkIsServer && CaptureTheFlag.IsActorRevealedByFlag_Client(this) ||
//                     (VisibleTillEndOfPhase && !MovedForEvade ||
//                      (CurrentlyVisibleForAbilityCast || (flag = method_38()) && method_43()))));
            return false;
        }

        public bool method_51(PlayerData playerData_0, bool bool_0 = true, bool bool_1 = false)
        {
//            Team team = Team.TeamA;
//            if (playerData_0 != null)
//                team = !bool_1 || playerData_0.ActorData == null
//                    ? playerData_0.GetTeamViewing()
//                    : playerData_0.ActorData.method_76();
//            return playerData_0 != null && (method_11().IsInvisibleToEnemies(bool_0) && method_76() != team &&
//                                            (playerData_0.ActorData == null || !playerData_0.ActorData.method_11()
//                                                 .HasStatus(StatusType.SeeInvisible, bool_0)));
            return false;
        }

        public bool IsActorVisibleToActor(ActorData actorData_0, bool bool_0 = false) => method_52(actorData_0, bool_0);

        public bool method_52(ActorData actorData_0, bool bool_0 = false)
        {
            if (this == actorData_0)
                return true;
            if (!EvoSGameConfig.NetworkIsServer && actorData_0 == GameFlowData.activeOwnedActorData)
                return method_48();
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Warning,
                    "Calling the server-only method ActorData::IsActorVisibleToActor on a client. Clients can only depend on ActorData::IsActorVisibleToClient.");
            bool flag1 = method_50(actorData_0.PlayerData);
            bool flag2 = method_51(actorData_0.PlayerData, true, bool_0);
//            return flag1 || !flag2 && actorData_0.method_20().IsVisible(method_73());
            return false;
        }

        public bool method_53()
        {
            bool flag = false;
            foreach (ActorData allTeamMember in GameFlowData.GetAllTeamMembers(method_77()))
            {
                if (!allTeamMember.method_38() && method_52(allTeamMember, true))
                {
                    flag = true;
                    break;
                }
            }

            return flag;
        }

        public bool method_54(ActorData actorData_0)
        {
            bool flag1 = method_50(actorData_0.PlayerData);
            bool flag2 = method_51(actorData_0.PlayerData, true, true);
            bool flag3;
            if (flag1)
                flag3 = true;
            else if (flag2)
            {
                flag3 = false;
            }
            else
            {
//                bool flag4 = method_46() < 0 || BrushRegion.HasTeamMemberInRegion(method_77(), method_46());
//                flag3 = !method_45() || flag4;
            }

//            return flag3;
            return false;
        }

        public void ApplyForceFromPoint(Vector3 pos, float amount, Vector3 overrideDir)
        {
//            Vector3 vector3 = method_91("hip_JNT") - pos;
//            if ((double) vector3.Length() >= 1.5)
//                return;
//            if (overrideDir != Vector3.Zero)
//                ApplyForce(Vector3.Normalize(overrideDir), amount);
//            else
//                ApplyForce(Vector3.Normalize(vector3), amount);
        }

        public void ApplyForce(Vector3 dir, float amount)
        {
//            Rigidbody rigidbody = method_89();
//            if (!(bool) (rigidbody))
//                return;
//            rigidbody.AddForce(dir * amount, ForceMode.Impulse);
        }

        public Vector3 method_55(float float_0)
        {
//            if (Camera.main == null || m_nameplateJoint == null)
            return new Vector3();
//            Vector3 position = m_nameplateJoint.m_jointObject.transform.position;
//            Vector3 screenPoint = Camera.main.WorldToScreenPoint(position);
//            Vector3 vector3_1 = Camera.main.WorldToScreenPoint(position + Camera.main.transform.up) - screenPoint;
//            vector3_1.Z = 0.0f;
//            Vector3 vector3_2 = Camera.main.transform.up * (float_0 / vector3_1.Length());
//            return position + vector3_2;
        }

        public Vector3 method_56()
        {
            return method_58(method_73());
        }

        public Vector3 method_57()
        {
            return method_59(method_73());
        }

        public Vector3 method_58(BoardSquare boardSquare_0)
        {
            if (boardSquare_0 != null)
            {
                Vector3 vector3 = method_59(boardSquare_0);
                vector3.Y += BoardSquare.s_LoSHeightOffset;
                return vector3;
            }

            Log.Print(LogType.Warning, "Trying to get LoS check pos wrt a null square (IsDead: " + method_38() +
                                       ")  for " +
                                       DisplayName + " Code issue-- actor probably instantiated but not on Board yet.");
            return m_actorMovement.transform.position;
        }

        public Vector3 method_59(BoardSquare boardSquare_0)
        {
            if (boardSquare_0 != null)
                return boardSquare_0.method_13();
            Log.Print(LogType.Warning, "Trying to get free pos wrt a null square (IsDead: " + method_38() + ").  for " +
                                       DisplayName +
                                       " Code issue-- actor probably instantiated but not on Board yet.");
            return m_actorMovement.transform.position;
        }

        public int method_60()
        {
            var num1 = UnresolvedDamage + ClientUnresolvedDamage;
            var num2 = UnresolvedHealing + ClientUnresolvedHealing;
            var num3 = AbsorbPoints + ClientUnresolvedAbsorb;
            return Mathf.Clamp(HitPoints - Mathf.Max(0, num1 - num3) + num2, 0, method_30());
        }

        public int method_61(int int_0)
        {
            var num1 = UnresolvedDamage + ClientUnresolvedDamage;
            var num2 = UnresolvedHealing + ClientUnresolvedHealing;
            var num3 = AbsorbPoints + ClientUnresolvedAbsorb;
            if (int_0 > 0)
                num2 += int_0;
            else
                num1 -= int_0;
            return Mathf.Clamp(HitPoints - Mathf.Max(0, num1 - num3) + num2, 0, method_30());
        }

        public int method_62()
        {
            return Mathf.Clamp(
                TechPoints + ReservedTechPoints + ClientReservedTechPoints +
                (UnresolvedTechPointGain + ClientUnresolvedTechPointGain) -
                (UnresolvedTechPointLoss + ClientUnresolvedTechPointLoss), 0, method_33());
        }

        public int method_63()
        {
            return Mathf.Max(0, ExpectedHoTTotal + ClientExpectedHoTTotalAdjust - ClientAppliedHoTThisTurn);
        }

        public int method_64()
        {
            return Mathf.Max(0, ExpectedHoTThisTurn - ClientAppliedHoTThisTurn);
        }

        public string method_65()
        {
            var str = $"{method_60()}";
            if (AbsorbPoints > 0)
            {
                var num = Mathf.Max(0,
                    AbsorbPoints + ClientUnresolvedAbsorb - (UnresolvedDamage + ClientUnresolvedDamage));
                str += $" +{num} shield";
            }

            return str;
        }

        public int method_66()
        {
            return Mathf.Max(0, AbsorbPoints + ClientUnresolvedAbsorb - (UnresolvedDamage + ClientUnresolvedDamage));
        }

        public bool method_67()
        {
            if (GameFlow != null && GameFlow.playerDetails != null)
            {
                if (!GameFlow.playerDetails.TryGetValue(PlayerData.GetPlayer(), out var playerDetails))
                    return false;
                return playerDetails.IsHumanControlled;
            }

            Log.Print(LogType.Error, "Method called too early, results may be incorrect");
            return false;
        }

        public bool method_68()
        {
            return GameplayUtils.IsPlayerControlled(this) && GameplayUtils.IsBot(this) &&
                   (GameFlow.playerDetails.TryGetValue(PlayerData.GetPlayer(), out var playerDetails) &&
                    playerDetails != null) && playerDetails.m_botsMasqueradeAsHumans;
        }

        public long method_69()
        {
            if (!GameFlow.playerDetails.TryGetValue(PlayerData.GetPlayer(), out var playerDetails) ||
                !method_68() && !playerDetails.IsLoadTestBot && !method_67())
                return -1;
            return playerDetails.m_accountId;
        }

        public long method_70()
        {
            long num = -1;
            if (PlayerData != null && GameFlow.playerDetails.ContainsKey(PlayerData.GetPlayer()))
                num = GameFlow.playerDetails[PlayerData.GetPlayer()].m_accountId;
            return num;
        }

        private void OnDisable()
        {
            GameFlowData?.RemoveFromTeam(this);
        }

        public static bool Boolean_0
        {
            get { return false; }
        }

        public GridPos method_71()
        {
            var gridPos = new GridPos(-1, -1, 0);
            if (method_74() != null)
            {
                gridPos = method_74().method_3();
                ++gridPos.Height;
            }

            return gridPos;
        }

        public bool CanMoveToBoardSquare(int x, int y)
        {
            return m_actorMovement.CanMoveToBoardSquare(x, y);
        }

        public bool CanMoveToBoardSquare(BoardSquare dest)
        {
            return m_actorMovement.CanMoveToBoardSquare(dest);
        }

        public void ClearFacingDirectionAfterMovement()
        {
            SetFacingDirectionAfterMovement(Vector3.Zero);
        }

        public void SetFacingDirectionAfterMovement(Vector3 facingDirAfterMovement)
        {
            if (!(m_facingDirAfterMovement != facingDirAfterMovement))
                return;
            m_facingDirAfterMovement = facingDirAfterMovement;
            if (!EvoSGameConfig.NetworkIsServer)
                return;
            if (m_teamSensitiveData_friendly != null)
                m_teamSensitiveData_friendly.FacingDirAfterMovement = m_facingDirAfterMovement;
            if (m_teamSensitiveData_hostile != null)
                m_teamSensitiveData_hostile.FacingDirAfterMovement = m_facingDirAfterMovement;
        }

        public Vector3 method_72()
        {
            return m_facingDirAfterMovement;
        }

        public void OnMovementWhileDisappeared(MovementType movementType)
        {
            Log.Print(LogType.Debug, $"{method_95()}: calling OnMovementWhileDisappeared.");
//            if (ClientMovementManager.Get() != null)
//                ClientMovementManager.Get().OnActorMoveStart_Disappeared(this, movementType);
            if (method_74() != null && method_74().occupant == gameObject)
                UnoccupyCurrentBoardSquare();
//            m_actorMovement.ClearPath();
            SetCurrentBoardSquare(null);
//            method_20().MarkForRecalculateVisibility();
        }

        public void MoveToBoardSquareLocal(
            BoardSquare dest,
            MovementType movementType,
            BoardSquarePathInfo path,
            bool moverWillDisappear)
        {
//            m_disappearingAfterCurrentMovement = moverWillDisappear;
//            if (dest == null)
//            {
//                if (moverWillDisappear && path == null)
//                {
//                    UnoccupyCurrentBoardSquare();
//                    SetCurrentBoardSquare(null);
//                    ForceUpdateIsVisibleToClientCache();
//                    ForceUpdateActorModelVisibility();
//                }
//                else
//                    Log.Print(LogType.Error,
//                        $"Actor {DisplayName} in MoveToBoardSquare has null destination (movementType = {movementType}");
//            }
//            else if (path == null && movementType != MovementType.Teleport)
//            {
//                Log.Print(LogType.Error,
//                    $"Actor {DisplayName} in MoveToBoardSquare has null path (movementType = {movementType})");
//            }
//            else
//            {
//                if (ServerClientUtils.GetCurrentAbilityPhase() == AbilityPriority.Evasion)
//                    MovedForEvade = true;
//                bool flag = path != null && path.WillDieAtEnd();
//                BoardSquare dest1 =
//                    path == null || path.GetPathEndpoint() == null || !(path.GetPathEndpoint().square != null)
//                        ? dest
//                        : path.GetPathEndpoint().square;
//                if (ClientMovementManager.Get() != null)
//                {
//                    ClientMovementManager.Get().OnActorMoveStart_ClientMovementManager(this, dest1, movementType, path);
//                    ClientResolutionManager.Get().OnActorMoveStart_ClientResolutionManager(this, path);
//                }
//
//                if (method_74() != null && method_74().occupant == gameObject)
//                    UnoccupyCurrentBoardSquare();
//                BoardSquare currentBoardSquare = CurrentBoardSquare;
//                if (movementType == MovementType.Teleport)
//                    m_actorMovement.ClearPath();
//                if (!flag && !moverWillDisappear)
//                {
//                    SetCurrentBoardSquare(dest);
//                    if (method_74() != null)
//                        OccupyCurrentBoardSquare();
//                }
//                else
//                {
//                    SetCurrentBoardSquare(null);
//                    SetMostRecentDeathSquare(dest);
//                }
//
//                switch (movementType)
//                {
//                    case MovementType.Normal:
//                    case MovementType.Flight:
//                    case MovementType.WaypointFlight:
//                        if (m_actorCover != null)
//                            m_actorCover.DisableCover();
//                        if (path == null)
//                        {
//                            path = new BoardSquarePathInfo();
//                            path.square = currentBoardSquare;
//                            path.prev = null;
//                            path.next = new BoardSquarePathInfo();
//                            path.next.square = dest;
//                            path.next.prev = path;
//                            path.next.next = null;
//                        }
//
//                        m_actorMovement.BeginTravellingAlongPath(path, movementType);
//                        m_actorMovement.UpdatePosition();
//                        break;
//                    case MovementType.Teleport:
//                        ForceUpdateIsVisibleToClientCache();
//                        ForceUpdateActorModelVisibility();
//                        SetTransformPositionToSquare(dest);
//                        m_actorMovement.ClearPath();
//                        if (m_actorCover != null)
//                            m_actorCover.RecalculateCover();
//                        UpdateFacingAfterMovement();
//                        if (currentBoardSquare != null)
//                        {
//                            BoardSquare destination = dest;
//                            BoardSquarePathInfo next =
//                                MovementUtils.Build2PointTeleportPath(currentBoardSquare, destination).next;
//                            if (ClientClashManager.Get() != null)
//                                ClientClashManager.Get().OnActorMoved_ClientClashManager(this, next);
//                            if (ClientResolutionManager.Get() != null)
//                            {
//                                ClientResolutionManager.Get().OnActorMoved_ClientResolutionManager(this, next);
//                            }
//                        }
//
//                        break;
//                    case MovementType.Knockback:
//                    case MovementType.Charge:
//                        if (m_actorCover != null)
//                            m_actorCover.DisableCover();
//                        m_actorMovement.BeginChargeOrKnockback(currentBoardSquare, dest, path, movementType);
//                        m_actorMovement.UpdatePosition();
//                        if (!flag && !moverWillDisappear && (path.square == dest && path.next == null))
//                            UpdateFacingAfterMovement();
//                        if (movementType == MovementType.Knockback)
//                        {
//                            KnockbackMoveStarted = true;
//                        }
//
//                        break;
//                }
//
//                m_actorMovement.UpdateSquaresCanMoveTo();
//                method_20().MarkForRecalculateVisibility();
//            }
        }

        public void AppearAtBoardSquare(BoardSquare dest)
        {
            if (dest == null)
            {
                Log.Print(LogType.Error, $"Actor {DisplayName} in AppearAtBoardSquare has null destination)");
            }
            else
            {
                if (method_74() != null && method_74().occupant == gameObject)
                    UnoccupyCurrentBoardSquare();
                SetCurrentBoardSquare(dest);
                SetTransformPositionToSquare(dest);
                if (method_74() == null)
                    return;
                OccupyCurrentBoardSquare();
            }
        }

        public void SetTransformPositionToSquare(BoardSquare refSquare)
        {
            if (refSquare == null)
                return;
            SetTransformPositionToVector(refSquare.method_13());
        }

        public void SetTransformPositionToVector(Vector3 newPos)
        {
//            if (!(transform.position != newPos))
//                return;
//            BoardSquare boardSquare1 = Board.method_7(transform.position);
//            BoardSquare boardSquare2 = Board.method_7(newPos);
//            if (boardSquare1 != boardSquare2 && boardSquare1 != null)
//                PreviousBoardSquarePosition = boardSquare1.ToVector3();
//            transform.position = newPos;
        }

        public void UnoccupyCurrentBoardSquare()
        {
            if (method_74() == null || method_74().occupant != gameObject)
                return;
            method_74().occupant = (GameObject) null;
        }

        public BoardSquare method_73()
        {
            return m_actorMovement.GetTravelBoardSquare();
        }

        public BoardSquare GetCurrentBoardSquare()
        {
            return method_74();
        }

        public BoardSquare method_74()
        {
            return CurrentBoardSquare;
        }

        public BoardSquare method_75()
        {
            return m_mostRecentDeathSquare;
        }

        public void SetMostRecentDeathSquare(BoardSquare square)
        {
            m_mostRecentDeathSquare = square;
        }

        public void OccupyCurrentBoardSquare()
        {
            if (method_74() == null)
                return;
            method_74().occupant = gameObject;
        }

        private void SetCurrentBoardSquare(BoardSquare square)
        {
            if (square == CurrentBoardSquare)
                return;
            m_clientCurrentBoardSquare = square;

            if (MoveFromBoardSquare == null)
                MoveFromBoardSquare = square;
            InitialMoveStartSquare = square;
        }

        public void ClearCurrentBoardSquare()
        {
            if (CurrentBoardSquare != null)
                UnoccupyCurrentBoardSquare();
            m_clientCurrentBoardSquare = null;
            MoveFromBoardSquare = null;
        }

        public void ClearPreviousMovementInfo()
        {
//            if (!EvoSGameConfig.NetworkIsServer)
//                return;
//            if (m_teamSensitiveData_friendly != null)
//                m_teamSensitiveData_friendly.ClearPreviousMovementInfo();
//            if (m_teamSensitiveData_hostile == null)
//                return;
//            m_teamSensitiveData_hostile.ClearPreviousMovementInfo();
        }

        public void SetClientFriendlyTeamSensitiveData(ActorTeamSensitiveData friendlyTSD)
        {
            if (m_teamSensitiveData_friendly == friendlyTSD)
                return;
            Log.Print(LogType.Game, "Setting Friendly TeamSensitiveData for " + method_95());
            m_teamSensitiveData_friendly = friendlyTSD;
//            m_teamSensitiveData_friendly.OnClientAssociatedWithActor(this);
        }

        public void SetClientHostileTeamSensitiveData(ActorTeamSensitiveData hostileTSD)
        {
            if (m_teamSensitiveData_hostile == hostileTSD)
                return;
            Log.Print(LogType.Game, "Setting Hostile TeamSensitiveData for " + method_95());
            m_teamSensitiveData_hostile = hostileTSD;
//            m_teamSensitiveData_hostile.OnClientAssociatedWithActor(this);
        }

        public void UpdateFacingAfterMovement()
        {
            if (!(m_facingDirAfterMovement != Vector3.Zero))
                return;
            TurnToDirection(m_facingDirAfterMovement);
        }

        public void SetTeam(Team team)
        {
            m_team = team;
            GameFlowData.AddToTeam(this);
//            TeamStatusDisplay.GetTeamStatusDisplay().RebuildTeamDisplay();
//            if (EvoSGameConfig.NetworkIsServer)
//                ;
        }

        public Team Team => m_team;

        public Team method_76()
        {
            return m_team;
        }

        public Team method_77()
        {
            if (m_team == Team.TeamA)
                return Team.TeamB;
            return m_team == Team.TeamB ? Team.TeamA : Team.Objects;
        }

//        public List<Team> method_78()
//        {
//            return GameplayUtils.GetOtherTeamsThan(m_team);
//        }

        public List<Team> method_79()
        {
            return new List<Team> {method_76()};
        }

        public List<Team> method_80()
        {
            return new List<Team> {method_77()};
        }

        public string method_81()
        {
            return m_team != Team.TeamA ? "Orange" : "Blue";
        }

        public string method_82()
        {
            return m_team == Team.TeamA ? "Orange" : "Blue";
        }

//        public Color method_83()
//        {
//            return m_team != Team.TeamA ? ActorData.s_teamBColor : ActorData.s_teamAColor;
//        }
//
//        public Color method_84()
//        {
//            return m_team != Team.TeamA ? ActorData.s_teamAColor : ActorData.s_teamBColor;
//        }
//
//        public Color method_85(Team team_0)
//        {
//            return team_0 != method_76() ? ActorData.s_hostilePlayerColor : ActorData.s_friendlyPlayerColor;
//        }

        public void OnTurnTick()
        {
//            CurrentlyVisibleForAbilityCast = false;
//            MovedForEvade = false;
//            m_actorMovement.ClearPath();
//            method_20().MarkForRecalculateVisibility();
//            if (!EvoSGameConfig.NetworkIsServer && m_serverMovementWaitForEvent != GameEventManager.EventType.Invalid &&
//                (m_serverMovementDestination != method_74() && !method_38()))
//                MoveToBoardSquareLocal(m_serverMovementDestination, MovementType.Teleport,
//                    m_serverMovementPath, false);
//
//            if (EvoSGameConfig.NetworkIsClient)
//            {
//                if (ClientUnresolvedDamage != 0)
//                {
//                    Log.Print(LogType.Error, "ClientUnresolvedDamage not cleared on TurnTick for " + method_95());
//                    ClientUnresolvedDamage = 0;
//                }
//
//                if (ClientUnresolvedHealing != 0)
//                {
//                    Log.Print(LogType.Error, "ClientUnresolvedHealing not cleared on TurnTick for " + method_95());
//                    ClientUnresolvedHealing = 0;
//                }
//
//                if (ClientUnresolvedTechPointGain != 0)
//                    ClientUnresolvedTechPointGain = 0;
//                if (ClientUnresolvedTechPointLoss != 0)
//                    ClientUnresolvedTechPointLoss = 0;
//                if (ClientReservedTechPoints != 0)
//                    ClientReservedTechPoints = 0;
//                if (ClientUnresolvedAbsorb != 0)
//                {
//                    Log.Print(LogType.Error, "ClientUnresolvedAbsorb not cleared on TurnTick for " + method_95());
//                    ClientUnresolvedAbsorb = 0;
//                }
//
//                ClientExpectedHoTTotalAdjust = 0;
//                ClientAppliedHoTThisTurn = 0;
//                SynchClientLastKnownPosToServerLastKnownPos();
//                if (method_39() != null)
//                    method_39().OnTurnTick();
//            }
//
//            m_actorVFX.OnTurnTick();
//            m_wasUpdatingForConfirmedTargeting = false;
//            KnockbackMoveStarted = false;
//            if (method_3() != null)
//                method_3().Client_ResetKillAssistContribution();
//            if (method_17() != null)
//            {
//                method_17().RecalculateCover();
//            }
//
//            if (OnTurnStartDelegates == null)
//                return;
//            OnTurnStartDelegates();
        }

        public bool HasQueuedMovement()
        {
            if (!m_queuedMovementRequest)
                return m_queuedChaseRequest;
            return true;
        }

        public bool HasQueuedChase()
        {
            return m_queuedChaseRequest;
        }

        public ActorData method_86()
        {
            return m_queuedChaseTarget;
        }

        public void TurnToDirection(Vector3 dir)
        {
//            Quaternion quaternion = Quaternion.LookRotation(dir);
//            if ((double) Quaternion.Angle(quaternion, m_targetRotation.GetEndValue()) <= 0.00999999977648258)
//                return;
//            transform.localRotation = m_targetRotation.GetEndValue();
//            m_targetRotation.EaseTo(quaternion, 0.1f);
        }

        public void TurnToPosition(Vector3 position, float turnDuration = 0.2f)
        {
//            Vector3 view = position - transform.position;
//            view.Y = 0.0f;
//            if (!(view != Vector3.Zero))
//                return;
//            transform.localRotation = m_targetRotation.GetEndValue();
//            Quaternion quaternion = new Quaternion();
//            quaternion.SetLookRotation(view);
//            if ((double) Quaternion.Angle(quaternion, m_targetRotation.GetEndValue()) <= 0.00999999977648258)
//                return;
//            m_targetRotation.EaseTo(quaternion, turnDuration);
        }

        public float method_87()
        {
            return 0; //m_targetRotation.CalcTimeRemaining();
        }

        public void TurnToPositionInstant(Vector3 position)
        {
//            Vector3 view = position - transform.position;
//            view.Y = 0.0f;
//            if (!(view != Vector3.Zero))
//                return;
//            Quaternion quaternion = new Quaternion();
//            quaternion.SetLookRotation(view);
//            transform.localRotation = quaternion;
//            m_targetRotation.SnapTo(quaternion);
        }

//        public Rigidbody method_88(string string_0)
//        {
//            Rigidbody rigidbody = (Rigidbody) null;
//            GameObject inChildren = gameObject.FindInChildren(string_0, 0);
//            if ((bool) (inChildren))
//                rigidbody = inChildren.GetComponentInChildren<Rigidbody>();
//            else
//                Log.Print(LogType.Warning, string.Format(
//                    "GetRigidBody trying to find body of bone {0} on actor '{1}' (obj name '{2}'), but the bone cannot be found.",
//                    string_0, DisplayName, gameObject.Name));
//            return rigidbody;
//        }
//
//        public Rigidbody method_89()
//        {
//            if (m_cachedHipJoint == null)
//                m_cachedHipJoint = method_88("hip_JNT");
//            return m_cachedHipJoint;
//        }
//
//        public Vector3 method_90()
//        {
//            Rigidbody rigidbody = method_89();
//            if (rigidbody != null)
//                return rigidbody.transform.position;
//            return gameObject.transform.position;
//        }

        public Vector3 method_91(string string_0)
        {
//            GameObject inChildren = gameObject.FindInChildren(string_0, 0);
//            return inChildren == null ? gameObject.transform.position : inChildren.transform.position;
            return gameObject.transform.position;
        }

        public Quaternion method_92(string string_0)
        {
            Quaternion identity = Quaternion.Identity;
//            GameObject inChildren = gameObject.FindInChildren(string_0, 0);
//            Quaternion rotation;
//            if ((inChildren) != null)
//            {
//                rotation = inChildren.transform.rotation;
//            }
//            else
//            {
//                Log.Print(LogType.Warning,
//                    $"GetBoneRotation trying to find rotation of bone {string_0} on actor '{DisplayName}' (obj name '{gameObject.Name}'), but the bone cannot be found.");
//                rotation = gameObject.transform.rotation;
//            }

//            return rotation;
            return identity;
        }

        public void OnDeselect()
        {
            RespawnPickedPositionSquare = RespawnPickedPositionSquare;
        }

        public void OnSelect()
        {
            m_callHandleOnSelectInUpdate = true;
        }

        private void HandleOnSelect()
        {
//            m_actorTurnSM.OnSelect();
//            method_20().MarkForRecalculateVisibility();
//            CameraManager.Get().OnActiveOwnedActorChange(this);
            if (method_9() == null)
                return;
            method_9().UpdateSquaresCanMoveTo();
        }

        public void OnMovementChanged(MovementChangeType changeType, bool forceChased = false)
        {
        }

        public bool OutOfCombat
        {
            get => m_outOfCombat;
            private set => m_outOfCombat = value;
        }

        public bool BeingTargetedByClientAbility(out bool inCover, out bool updatingInConfirm)
        {
            // Server noop
            inCover = false;
            updatingInConfirm = false;
            return false;
        }

//        public void AddForceShowOutlineChecker(IForceActorOutlineChecker checker)
//        {
//            if (checker == null || m_forceShowOutlineCheckers.Contains(checker))
//                return;
//            m_forceShowOutlineCheckers.Add(checker);
//        }
//
//        public void RemoveForceShowOutlineChecker(IForceActorOutlineChecker checker)
//        {
//            if (m_forceShowOutlineCheckers == null)
//                return;
//            m_forceShowOutlineCheckers.Remove(checker);
//        }
//
//        public bool ShouldForceTargetOutlineForActor(ActorData actor)
//        {
//            if (GameFlowData.gameState != GameState.BothTeams_Decision || !(GameFlowData.activeOwnedActorData != null))
//                return false;
//            bool flag = false;
//            for (int index = 0; index < m_forceShowOutlineCheckers.Count && !flag; ++index)
//            {
//                IForceActorOutlineChecker showOutlineChecker = m_forceShowOutlineCheckers[index];
//                if (showOutlineChecker != null)
//                    flag = showOutlineChecker.ShouldForceShowOutline(actor);
//            }
//
//            return flag;
//        }

//        [Client]
        private void UpdateNameplateForTargetingAbility(
            ActorData targetingActor,
            Ability selectedAbility,
            bool targeted,
            bool inCover,
            int currentTargeterIndex,
            bool inConfirm)
        {
            if (!EvoSGameConfig.NetworkIsClient)
            {
                Log.Print(LogType.Warning,
                    "[Client] function 'System.Void ActorData::UpdateNameplateForTargetingAbility(ActorData,Ability,System.Boolean,System.Boolean,System.Int32,System.Boolean)' called on server");
            }
        }

//        [Client]
        private bool ShouldUpdateForConfirmedTargeting(Ability lastSelectedAbility, int numAbilityTargets)
        {
            if (!EvoSGameConfig.NetworkIsClient)
            {
                Log.Print(LogType.Warning,
                    "[Client] function 'System.Boolean ActorData::ShouldUpdateForConfirmedTargeting(Ability,System.Int32)' called on server");
                return false;
            }

            if (lastSelectedAbility == null)
                return false;
            if (ForceDisplayTargetHighlight)
                return true;
//            if (lastSelectedAbility.Targeter == null ||
//                (double) lastSelectedAbility.Targeter.GetConfirmedTargetingRemainingTime() <= 0.0)
//                return false;
//            if (!lastSelectedAbility.IsSimpleAction())
//                return numAbilityTargets > 0;
            return true;
        }

        public bool WouldSquareBeChasedByClient(BoardSquare square, bool IgnoreChosenChaseTarget = false)
        {
            ActorData activeOwnedActorData = GameFlowData.activeOwnedActorData;
            if (!activeOwnedActorData.method_93(square))
                return false;
            if (!activeOwnedActorData.HasQueuedMovement() && !activeOwnedActorData.HasQueuedChase())
                return true;
            if (activeOwnedActorData.HasQueuedChase())
                return IgnoreChosenChaseTarget || square != activeOwnedActorData.method_86().method_74();
            return square == activeOwnedActorData.MoveFromBoardSquare ||
                   !activeOwnedActorData.CanMoveToBoardSquare(square);
        }

        public bool method_93(BoardSquare boardSquare_0)
        {
            bool flag;
            if (boardSquare_0 != null && boardSquare_0.occupant?.GetComponent<ActorData>() != null)
            {
                if (GameFlowData != null && GameFlowData.gameState == GameState.BothTeams_Decision)
                {
                    var component1 = boardSquare_0.occupant.GetComponent<ActorData>();
                    var actorData_0 = this;
                    var component2 = actorData_0.GetComponent<AbilityData>();
                    flag = false;
//                    flag = !component1.method_38() && (component1 != actorData_0 &&
//                                                       (component1.method_49(actorData_0) &&
//                                                        (component2.GetQueuedAbilitiesAllowMovement() &&
//                                                         !component1.IgnoreForAbilityHits)));
                }
                else
                    flag = false;
            }
            else
                flag = false;

            return flag;
        }

//        public void OnHitWhileInCover(Vector3 hitOrigin, ActorData caster)
//        {
//            if (method_38() || !(m_actorVFX != null))
//                return;
//            m_actorVFX.ShowHitWhileInCoverVfx(method_57(), hitOrigin, caster);
//            AudioManager.PostEvent("ablty/generic/feedback/behind_cover_hit", gameObject);
//        }
//
//        public void OnKnockbackWhileUnstoppable(Vector3 hitOrigin, ActorData caster)
//        {
//            if (method_38() || !(m_actorVFX != null))
//                return;
//            m_actorVFX.ShowKnockbackWhileUnstoppableVfx(method_57(), hitOrigin, caster);
//            AudioManager.PostEvent("ablty/generic/feedback/unstoppable", gameObject);
//        }
//
//        public void PostAnimationAudioEvent(string eventAndTag)
//        {
//            int length = eventAndTag.IndexOf(':');
//            string audioTag;
//            string eventName;
//            if (length == -1)
//            {
//                audioTag = "default";
//                eventName = eventAndTag;
//            }
//            else
//            {
//                audioTag = eventAndTag.Substring(0, length);
//                eventName = eventAndTag.Substring(length + 1);
//            }
//
//            CharacterResourceLink characterResourceLink = method_99();
//            if (characterResourceLink != null && !characterResourceLink.AllowAudioTag(audioTag, m_visualInfo))
//                return;
//            PostAudioEvent(eventName, (OnEventNotify) null, AudioManager.EventAction.PlaySound);
//        }
//
//        public void PostAudioEvent(
//            string eventName,
//            OnEventNotify notifyCallback = null,
//            AudioManager.EventAction action = AudioManager.EventAction.PlaySound)
//        {
//            CharacterResourceLink characterResourceLink = method_99();
//            string eventName1 = !(characterResourceLink != null)
//                ? eventName
//                : characterResourceLink.ReplaceAudioEvent(eventName, m_visualInfo);
//            if (eventName1 != eventName)
//                ;
//            if (notifyCallback != null)
//                AudioManager.PostEventNotify(eventName1, action, notifyCallback, null, gameObject);
//            else
//                AudioManager.PostEvent(eventName1, action, null, gameObject);
//        }

//        [Command]
        public void CmdSetPausedForDebugging(bool pause)
        {
            if (!EvoSGameConfig.AllowDebugCommands)
                return;
//            GameFlowData.SetPausedForDebugging(pause);
        }

//        [Command]
        public void CmdSetResolutionSingleStepping(bool singleStepping)
        {
            if (GameFlowData == null)
                return;
//            GameFlowData.SetResolutionSingleStepping(singleStepping);
        }

//        [Command]
        public void CmdSetResolutionSingleSteppingAdvance()
        {
            if (GameFlowData == null)
                return;
//            GameFlowData.SetResolutionSingleSteppingAdvance();
        }

//        [Command]
        public void CmdSetDebugToggleParam(string name, bool value)
        {
        }

//        [Command]
        public void CmdDebugReslotCards(bool reslotAll, int cardTypeInt)
        {
        }

//        [Command]
        public void CmdDebugSetAbilityMod(int abilityIndex, int modId)
        {
        }

//        [Command]
        private void CmdDebugReplaceWithBot()
        {
        }

//        [Command]
        public void CmdDebugSetHealthOrEnergy(int actorIndex, int valueToSet, int flag)
        {
        }

        public void HandleDebugSetHealth(int actorIndex, int valueToSet)
        {
            CallCmdDebugSetHealthOrEnergy(actorIndex, valueToSet, 0);
        }

        public void HandleDebugSetEnergy(int actorIndex, int valueToSet)
        {
            CallCmdDebugSetHealthOrEnergy(actorIndex, valueToSet, 1);
        }

//        [ClientRpc]
        public void RpcOnHitPointsResolved(int resolvedHitPoints)
        {
            if (EvoSGameConfig.NetworkIsServer)
                return;
            bool flag = method_38();
            UnresolvedDamage = 0;
            UnresolvedHealing = 0;
            ClientUnresolvedDamage = 0;
            ClientUnresolvedHealing = 0;
            ClientUnresolvedAbsorb = 0;
            SetHitPoints(resolvedHitPoints);
            if (flag || !method_38() || method_43())
                return;
            Log.Print(LogType.Error, ("Actor " + method_95() +
                                      " died on HP resolved; he should have already been ragdolled, but wasn't."));
            DoVisualDeath(new ActorModelData.ImpulseInfo(method_56(), VectorUtils.up));
        }

//        [ClientRpc]
//        public void RpcCombatText(
//            string combatText,
//            string logText,
//            CombatTextCategory category,
//            BuffIconToDisplay icon)
//        {
//            AddCombatText(combatText, logText, category, icon);
//        }
//
//        public void AddCombatText(
//            string combatText,
//            string logText,
//            CombatTextCategory category,
//            BuffIconToDisplay icon)
//        {
//            if (m_combatText == null)
//                Log.Print(LogType.Error, gameObject.Name + " does not have a combat text component.");
//            else
//                m_combatText.Add(combatText, logText, category, icon);
//        }

//        [Client]
        public void ShowDamage(string combatText)
        {
            if (EvoSGameConfig.NetworkIsClient)
                return;
            Log.Print(LogType.Warning,
                "[Client] function 'System.Void ActorData::ShowDamage(System.String)' called on server");
        }

//        [ClientRpc]
//        public void RpcApplyAbilityModById(int actionTypeInt, int abilityScopeId)
//        {
//            if (EvoSGameConfig.NetworkIsServer || !EvoSGameConfig.NetworkIsClient || abilityScopeId < 0)
//                return;
//            ApplyAbilityModById(actionTypeInt, abilityScopeId);
//        }

//        public void ApplyAbilityModById(int actionTypeInt, int abilityScopeId)
//        {
//            if ((GameManager.GameConfig.GameType == GameType.Tutorial
//                    ? 1
//                    : (AbilityModHelper.IsModAllowed(m_characterType, actionTypeInt, abilityScopeId) ? 1 : 0)) == 0)
//            {
//                Log.Print(LogType.Warning, ("Mod with ID " + abilityScopeId + " is not allowed on ability at index " +
//                                            actionTypeInt + " for character " + m_characterType.ToString()));
//            }
//            else
//            {
//                AbilityData component = GetComponent<AbilityData>();
//                if (!(component != null))
//                    return;
//                Ability abilityOfActionType = component.GetAbilityOfActionType((AbilityData.ActionType) actionTypeInt);
//                AbilityMod modForAbilityById = AbilityModManager.Get()
//                    .GetAbilityModForAbilityById(abilityOfActionType, abilityScopeId);
//                if (!(modForAbilityById != null))
//                    return;
//                GameType gameType = GameManager.GameConfig.GameType;
//                GameSubType instanceSubType = GameManager.GameConfig.InstanceSubType;
//                if (modForAbilityById.EquippableForGameType())
//                {
//                    ApplyAbilityModToAbility(abilityOfActionType, modForAbilityById, false);
//                    if (!EvoSGameConfig.NetworkIsServer)
//                        return;
//                    CallRpcApplyAbilityModById(actionTypeInt, abilityScopeId);
//                }
//                else
//                    Log.Print(LogType.Warning, "Mod with ID " + modForAbilityById.m_abilityScopeId +
//                                               " is not allowed in game type: " +
//                                               gameType.ToString() + ", subType: " + instanceSubType.LocalizedName);
//            }
//        }

        public void method_94(int int_0, int int_1)
        {
        }

//        private void ApplyAbilityModToAbility(Ability ability, AbilityMod abilityMod, bool log = false)
//        {
//            if (ability.GetType() != abilityMod.GetTargetAbilityType())
//                return;
//            ability.ApplyAbilityMod(abilityMod, this);
//            if (abilityMod.m_useChainAbilityOverrides)
//            {
//                ability.SanitizeChainAbilities();
//                method_8().ReInitializeChainAbilityList();
//                Ability[] chainAbilities = ability.GetChainAbilities();
//                if (chainAbilities != null)
//                {
//                    foreach (Ability ability1 in chainAbilities)
//                    {
//                        if (ability1 != null)
//                            ability1.sprite = ability.sprite;
//                    }
//                }
//            }
//
//            if (!log)
//                return;
//            Log.Print(LogType.Warning, ("Applied " + abilityMod.GetDebugIdentifier("white") + " to ability " +
//                                        ability.GetDebugIdentifier("orange")));
//        }

//        [ClientRpc]
        public void RpcMarkForRecalculateClientVisibility()
        {
//            if (!(method_20() != null))
//                return;
//            method_20().MarkForRecalculateVisibility();
        }

        public void ShowRespawnFlare(BoardSquare flareSquare, bool respawningThisTurn)
        {
            // server noop
        }

//        [ClientRpc]
//        public void RpcForceLeaveGame(GameResult gameResult)
//        {
//            if (GameFlowData.activeOwnedActorData != this || ClientGameManager.IsFastForward)
//                return;
//            ClientGameManager.LeaveGame(false, gameResult);
//        }

//        public void SendPingRequestToServer(
//            int teamIndex,
//            Vector3 worldPosition,
//            ActorController.PingType pingType)
//        {
//            if (method_12() == null)
//                return;
//            method_12().CallCmdSendMinimapPing(teamIndex, worldPosition, pingType);
//        }
//
//        public void SendAbilityPingRequestToServer(
//            int teamIndex,
//            LocalizationArg_AbilityPing localizedPing)
//        {
//            if (method_12() == null)
//                return;
//            method_12().CallCmdSendAbilityPing(teamIndex, localizedPing);
//        }

        public string method_95()
        {
            return "[" + method_28() + " (" + DisplayName + "), " + ActorIndex + "]";
        }

        public string method_96(string string_0)
        {
            return "<color=" + string_0 + ">" + method_95() + "</color>";
        }

        public string method_97()
        {
            return "Max HP: " + method_30() + "\nHP to Display: " + method_60() + "\n HP: " + HitPoints +
                   "\n Damage: " + UnresolvedDamage + "\n Healing: " + UnresolvedHealing + "\n Absorb: " +
                   AbsorbPoints + "\n CL Damage: " + ClientUnresolvedDamage + "\n CL Healing: " +
                   ClientUnresolvedHealing + "\n CL Absorb: " + ClientUnresolvedAbsorb + "\n\n Energy to Display: " +
                   method_62() + "\n  Energy: " + TechPoints + "\n Reserved Energy: " + ReservedTechPoints +
                   "\n EnergyGain: " + UnresolvedTechPointGain + "\n EnergyLoss: " + UnresolvedTechPointLoss +
                   "\n CL Reserved Energy: " + ClientReservedTechPoints + "\n CL EnergyGain: " +
                   ClientUnresolvedTechPointGain + "\n CL EnergyLoss: " + ClientUnresolvedTechPointLoss +
                   "\n CL Total HoT: " + (ExpectedHoTTotal + ClientExpectedHoTTotalAdjust) +
                   "\n CL HoT This Turn/Applied: " + ExpectedHoTThisTurn + " / " + ClientAppliedHoTThisTurn;
        }

        public string method_98()
        {
            string str = string.Empty;
            if (method_16() != null)
                str = str + "ActorTurnSM: CurrentState= " + method_16().CurrentState + " | PrevState= " +
                      method_16().PreviousState + "\n";
            return str;
        }

        public ActorData[] AsArray()
        {
            return new ActorData[1] {this};
        }

        public List<ActorData> AsList()
        {
            return new List<ActorData> {this};
        }

        public bool HasTag(string tag)
        {
            if (m_actorTags != null)
                return m_actorTags.HasTag(tag);
            return false;
        }

        public void AddTag(string tag)
        {
            if (m_actorTags == null)
                m_actorTags = gameObject.AddComponent<ActorTag>();
            m_actorTags.AddTag(tag);
        }

        public void RemoveTag(string tag)
        {
            if (!(m_actorTags != null))
                return;
            m_actorTags.RemoveTag(tag);
        }

        public BoardSquare InitialSpawnSquare
        {
            get { return m_initialSpawnSquare; }
        }

//        public CharacterResourceLink method_99()
//        {
//            if (m_characterResourceLink == null && m_characterType != CharacterType.None)
//            {
//                GameWideData gameWideData = GameWideData.Get();
//                if ((bool) (gameWideData))
//                    m_characterResourceLink = gameWideData.GetCharacterResourceLink(m_characterType);
//            }
//
//            return m_characterResourceLink;
//        }

        public GameObject ReplaceSequence(GameObject originalSequencePrefab)
        {
            return null;
//            if (originalSequencePrefab == null)
//                return (GameObject) null;
//            CharacterResourceLink characterResourceLink = method_99();
//            if (characterResourceLink == null)
//                return originalSequencePrefab;
//            return characterResourceLink.ReplaceSequence(originalSequencePrefab, m_visualInfo, m_abilityVfxSwapInfo);
        }

        public void OnAnimEvent(object eventObject, GameObject sourceObject)
        {
//            if (OnAnimationEventDelegates == null)
//                return;
//            OnAnimationEventDelegates(eventObject, sourceObject);
        }

//        public void OnGameEvent(GameEventManager.EventType eventType, GameEventManager.GameEventArgs args)
//        {
//            if (eventType != GameEventManager.EventType.GametimeScaleChange)
//                return;
//            Animator animator = method_42();
//            if (animator == null)
//                return;
//            animator.speed = GameTime.scale;
//        }

        protected static void InvokeCmdCmdSetPausedForDebugging(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSetPausedForDebugging called on client.");
            else
                ((ActorData) obj).CmdSetPausedForDebugging(reader.ReadBoolean());
        }

        protected static void InvokeCmdCmdSetResolutionSingleStepping(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSetResolutionSingleStepping called on client.");
            else
                ((ActorData) obj).CmdSetResolutionSingleStepping(reader.ReadBoolean());
        }

        protected static void InvokeCmdCmdSetResolutionSingleSteppingAdvance(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSetResolutionSingleSteppingAdvance called on client.");
            else
                ((ActorData) obj).CmdSetResolutionSingleSteppingAdvance();
        }

        protected static void InvokeCmdCmdSetDebugToggleParam(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSetDebugToggleParam called on client.");
            else
                ((ActorData) obj).CmdSetDebugToggleParam(reader.ReadString(), reader.ReadBoolean());
        }

        protected static void InvokeCmdCmdDebugReslotCards(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdDebugReslotCards called on client.");
            else
                ((ActorData) obj).CmdDebugReslotCards(reader.ReadBoolean(), (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdDebugSetAbilityMod(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdDebugSetAbilityMod called on client.");
            else
                ((ActorData) obj).CmdDebugSetAbilityMod((int) reader.ReadPackedUInt32(),
                    (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdDebugReplaceWithBot(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdDebugReplaceWithBot called on client.");
            else
                ((ActorData) obj).CmdDebugReplaceWithBot();
        }

        protected static void InvokeCmdCmdDebugSetHealthOrEnergy(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdDebugSetHealthOrEnergy called on client.");
            else
                ((ActorData) obj).CmdDebugSetHealthOrEnergy((int) reader.ReadPackedUInt32(),
                    (int) reader.ReadPackedUInt32(), (int) reader.ReadPackedUInt32());
        }

        public void CallCmdSetPausedForDebugging(bool pause)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSetPausedForDebugging called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdSetPausedForDebugging(pause);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSetPausedForDebugging);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(pause);
//                SendCommandInternal(writer, 0, "CmdSetPausedForDebugging");
            }
        }

        public void CallCmdSetResolutionSingleStepping(bool singleStepping)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSetResolutionSingleStepping called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdSetResolutionSingleStepping(singleStepping);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSetResolutionSingleStepping);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(singleStepping);
//                SendCommandInternal(writer, 0, "CmdSetResolutionSingleStepping");
            }
        }

        public void CallCmdSetResolutionSingleSteppingAdvance()
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSetResolutionSingleSteppingAdvance called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdSetResolutionSingleSteppingAdvance();
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSetResolutionSingleSteppingAdvance);
                writer.Write(GetComponent<NetworkIdentity>().netId);
//                SendCommandInternal(writer, 0, "CmdSetResolutionSingleSteppingAdvance");
            }
        }

        public void CallCmdSetDebugToggleParam(string name, bool value)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSetDebugToggleParam called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdSetDebugToggleParam(name, value);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSetDebugToggleParam);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(name);
                writer.Write(value);
//                SendCommandInternal(writer, 0, "CmdSetDebugToggleParam");
            }
        }

        public void CallCmdDebugReslotCards(bool reslotAll, int cardTypeInt)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdDebugReslotCards called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdDebugReslotCards(reslotAll, cardTypeInt);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdDebugReslotCards);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(reslotAll);
                writer.WritePackedUInt32((uint) cardTypeInt);
//                SendCommandInternal(writer, 0, "CmdDebugReslotCards");
            }
        }

        public void CallCmdDebugSetAbilityMod(int abilityIndex, int modId)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdDebugSetAbilityMod called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdDebugSetAbilityMod(abilityIndex, modId);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdDebugSetAbilityMod);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) abilityIndex);
                writer.WritePackedUInt32((uint) modId);
//                SendCommandInternal(writer, 0, "CmdDebugSetAbilityMod");
            }
        }

        public void CallCmdDebugReplaceWithBot()
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdDebugReplaceWithBot called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdDebugReplaceWithBot();
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdDebugReplaceWithBot);
                writer.Write(GetComponent<NetworkIdentity>().netId);
//                SendCommandInternal(writer, 0, "CmdDebugReplaceWithBot");
            }
        }

        public void CallCmdDebugSetHealthOrEnergy(int actorIndex, int valueToSet, int flag)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdDebugSetHealthOrEnergy called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdDebugSetHealthOrEnergy(actorIndex, valueToSet, flag);
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdDebugSetHealthOrEnergy);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) actorIndex);
                writer.WritePackedUInt32((uint) valueToSet);
                writer.WritePackedUInt32((uint) flag);
//                SendCommandInternal(writer, 0, "CmdDebugSetHealthOrEnergy");
            }
        }

        protected static void InvokeRpcRpcOnHitPointsResolved(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcOnHitPointsResolved called on server.");
            else
                ((ActorData) obj).RpcOnHitPointsResolved((int) reader.ReadPackedUInt32());
        }

        protected static void InvokeRpcRpcCombatText(NetworkBehaviour obj, NetworkReader reader)
        {
//            if (!EvoSGameConfig.NetworkIsClient)
//                Log.Print(LogType.Error, "RPC RpcCombatText called on server.");
//            else
//                ((ActorData) obj).RpcCombatText(reader.ReadString(), reader.ReadString(),
//                    (CombatTextCategory) reader.ReadInt32(), (BuffIconToDisplay) reader.ReadInt32());
        }

        protected static void InvokeRpcRpcApplyAbilityModById(NetworkBehaviour obj, NetworkReader reader)
        {
//            if (!EvoSGameConfig.NetworkIsClient)
//                Log.Print(LogType.Error, "RPC RpcApplyAbilityModById called on server.");
//            else
//                ((ActorData) obj).RpcApplyAbilityModById((int) reader.ReadPackedUInt32(),
//                    (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeRpcRpcMarkForRecalculateClientVisibility(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcMarkForRecalculateClientVisibility called on server.");
            else
                ((ActorData) obj).RpcMarkForRecalculateClientVisibility();
        }

        protected static void InvokeRpcRpcForceLeaveGame(NetworkBehaviour obj, NetworkReader reader)
        {
//            if (!EvoSGameConfig.NetworkIsClient)
//                Log.Print(LogType.Error, "RPC RpcForceLeaveGame called on server.");
//            else
//                ((ActorData) obj).RpcForceLeaveGame((GameResult) reader.ReadInt32());
        }

        public void CallRpcOnHitPointsResolved(int resolvedHitPoints)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcOnHitPointsResolved called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcOnHitPointsResolved);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) resolvedHitPoints);
//                SendRPCInternal(writer, 0, "RpcOnHitPointsResolved");
            }
        }

        public void CallRpcCombatText(
            string combatText,
            string logText,
            CombatTextCategory category,
            BuffIconToDisplay icon)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcCombatText called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcCombatText);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(combatText);
                writer.Write(logText);
                writer.Write((int) category);
                writer.Write((int) icon);
//                SendRPCInternal(writer, 0, "RpcCombatText");
            }
        }

        public void CallRpcApplyAbilityModById(int actionTypeInt, int abilityScopeId)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcApplyAbilityModById called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcApplyAbilityModById);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) actionTypeInt);
                writer.WritePackedUInt32((uint) abilityScopeId);
//                SendRPCInternal(writer, 0, "RpcApplyAbilityModById");
            }
        }

        public void CallRpcMarkForRecalculateClientVisibility()
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcMarkForRecalculateClientVisibility called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcMarkForRecalculateClientVisibility);
                writer.Write(GetComponent<NetworkIdentity>().netId);
//                SendRPCInternal(writer, 0, "RpcMarkForRecalculateClientVisibility");
            }
        }

        public void CallRpcForceLeaveGame(GameResult gameResult)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcForceLeaveGame called on client.");
            }
            else
            {
                var writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcForceLeaveGame);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write((int) gameResult);
//                SendRPCInternal(writer, 0, "RpcForceLeaveGame");
            }
        }
        // MARK END


        public override void Awake()
        {
            PlayerData = GetComponent<PlayerData>();
            if (PlayerData == null)
                throw new Exception($"Character {gameObject.Name} needs a PlayerData component");
            m_actorMovement = gameObject.GetComponent<ActorMovement>();
            if (m_actorMovement == null)
                m_actorMovement = gameObject.AddComponent<ActorMovement>();
            m_actorTurnSM = gameObject.GetComponent<ActorTurnSM>();
            if (m_actorTurnSM == null)
                m_actorTurnSM = gameObject.AddComponent<ActorTurnSM>();
            m_actorCover = gameObject.GetComponent<ActorCover>();
            if (m_actorCover == null)
                m_actorCover = gameObject.AddComponent<ActorCover>();
            m_actorVFX = gameObject.GetComponent<ActorVFX>();
            if (m_actorVFX == null)
                m_actorVFX = gameObject.AddComponent<ActorVFX>();
            m_timeBank = gameObject.GetComponent<TimeBank>();
            if (m_timeBank == null)
                m_timeBank = gameObject.AddComponent<TimeBank>();
            m_additionalVisionProvider = gameObject.GetComponent<ActorAdditionalVisionProviders>();
            if (m_additionalVisionProvider == null)
                m_additionalVisionProvider = gameObject.AddComponent<ActorAdditionalVisionProviders>();
            m_actorBehavior = GetComponent<ActorBehavior>();
            m_abilityData = GetComponent<AbilityData>();
            m_itemData = GetComponent<ItemData>();
            m_actorStats = GetComponent<ActorStats>();
            m_actorStatus = GetComponent<ActorStatus>();
            m_actorTargeting = GetComponent<ActorTargeting>();
            m_passiveData = GetComponent<PassiveData>();
//            m_combatText = GetComponent<CombatText>();
            m_actorTags = GetComponent<ActorTag>();
            m_freelancerStats = GetComponent<FreelancerStats>();
//            if (EvoSGameConfig.NetworkIsServer)
//                ActorIndex = (int) checked(++ActorData.s_nextActorIndex);
//            ActorData.Layer = LayerMask.NameToLayer("Actor");
//            ActorData.Layer_Mask = 1 << ActorData.Layer;
//            _lastSpawnTurn = !(bool) (GameFlowDat)
//                ? 1
//                : Mathf.Max(1, GameFlowDat.CurrentTurn);
            LastDeathTurn = -2;
            NextRespawnTurn = -1;
            HasBotController = false;
            SpawnerId = -1;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            return OnSerializeHelper(new NetworkWriterAdapter(writer), initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            uint num = uint.MaxValue;
            if (!initialState)
                num = reader.ReadPackedUInt32();
            if (num != 0U)
                OnSerializeHelper(new NetworkReaderAdapter(reader), initialState);
        }

        private bool OnSerializeHelper(IBitStream stream, bool initialState)
        {
            if (!initialState && m_serializeHelper.ShouldReturnImmediately(ref stream))
                return false;
            uint position = stream.Position;
            sbyte num1 = 0;
            sbyte num2 = 0;
            string displayName = DisplayName;
            float num3 = 0.0f;
            float num4 = 0.0f;
            sbyte num5 = (sbyte) s_invalidActorIndex;
            bool out0_1 = true;
            bool out1_1 = false;
            bool out2_1 = false;
            byte bitField1 = 0;
            sbyte num6 = 0;
            short num7 = 0;
            short num8 = 0;
            short num9 = 0;
            short num10 = 0;
            short num11 = 0;
            short num12 = 0;
            short num13 = 0;
            short num14 = 0;
            short num15 = 0;
            short num16 = 0;
            short num17 = 0;
            byte bitField2 = 0;
            int num18 = -1;
            int num19 = -1;
            int num20 = -1;
            sbyte num21 = -1;
            sbyte num22 = 0;
            int num23 = 0;
            short num24 = 0;
            short num25 = 0;
            bool out0_2 = false;
            bool out1_2 = true;
            bool out2_2 = false;
            bool out3_1 = false;
            bool out4_1 = false;
            byte bitField3 = 0;
            if (stream.isWriting)
            {
                num1 = (sbyte) PlayerIndex;
                num2 = (sbyte) ActorIndex;
                num6 = (sbyte) m_team;
                num23 = m_lastVisibleTurnToClient;
                if (ServerLastKnownPosSquare != null)
                {
                    num24 = (short) ServerLastKnownPosSquare.X;
                    num25 = (short) ServerLastKnownPosSquare.Y;
                }
                else
                {
                    num24 = -1;
                    num25 = -1;
                }

                num3 = RemainingHorizontalMovement;
                num4 = RemainingMovementWithQueuedAbility;
                out0_1 = QueuedMovementAllowsAbility;
                out1_1 = m_queuedMovementRequest;
                out2_1 = m_queuedChaseRequest;
                bitField1 = ServerClientUtils.CreateBitfieldFromBools(out0_1, out1_1, out2_1, false, false, false,
                    false, false);
                num5 = (sbyte) (m_queuedChaseTarget?.ActorIndex ?? s_invalidActorIndex);
                num7 = (short) HitPoints;
                num8 = (short) UnresolvedDamage;
                num9 = (short) UnresolvedHealing;
                num10 = (short) TechPoints;
                num11 = (short) ReservedTechPoints;
                num12 = (short) UnresolvedTechPointGain;
                num13 = (short) UnresolvedTechPointLoss;
                num14 = (short) AbsorbPoints;
                num15 = (short) MechanicPoints;
                num16 = (short) m_serverExpectedHoTTotal;
                num17 = (short) m_serverExpectedHoTThisTurn;
                bool b7 = num16 > 0 || num17 > 0;
                bitField2 = ServerClientUtils.CreateBitfieldFromBools(num8 > 0, num9 > 0,
                    num12 > 0, num13 > 0, num11 != 0, num14 > 0, num15 > 0, b7);
                num18 = LastDeathTurn;
                num19 = m_lastSpawnTurn;
                num20 = NextRespawnTurn;
                num21 = (sbyte) SpawnerId;
                num22 = (sbyte) LineOfSightVisibleExceptions.Count;
                out0_2 = HasBotController;
                out1_2 = m_showInGameHud;
                out2_2 = VisibleTillEndOfPhase;
                out3_1 = m_ignoreFromAbilityHits;
                out4_1 = m_alwaysHideNameplate;
                bitField3 = ServerClientUtils.CreateBitfieldFromBools(out0_2, out1_2, out2_2, out3_1, out4_1, false,
                    false, false);
                stream.Serialize(ref num1);
                stream.Serialize(ref num2);
                stream.Serialize(ref displayName);
                stream.Serialize(ref num6);
                stream.Serialize(ref bitField1);
                stream.Serialize(ref num3);
                stream.Serialize(ref num4);
                stream.Serialize(ref num5);
                stream.Serialize(ref num7);
                stream.Serialize(ref num10);
                stream.Serialize(ref bitField2);
                if (num8 > 0)
                    stream.Serialize(ref num8);
                if (num9 > 0)
                    stream.Serialize(ref num9);
                if (num12 > 0)
                    stream.Serialize(ref num12);
                if (num13 > 0)
                    stream.Serialize(ref num13);
                if (num11 != 0)
                    stream.Serialize(ref num11);
                if (num14 > 0)
                    stream.Serialize(ref num14);
                if (num15 > 0)
                    stream.Serialize(ref num15);
                if (b7)
                {
                    stream.Serialize(ref num16);
                    stream.Serialize(ref num17);
                }

                stream.Serialize(ref num18);
                stream.Serialize(ref num19);
                stream.Serialize(ref num20);
                stream.Serialize(ref num21);
                stream.Serialize(ref bitField3);
//                DebugSerializeSizeBeforeVisualInfo = stream.Position - position;
                SerializeCharacterVisualInfo(stream, ref m_visualInfo);
                SerializeCharacterCardInfo(stream, ref m_selectedCards);
                SerializeCharacterModInfo(stream, ref m_selectedMods);
                SerializeCharacterAbilityVfxSwapInfo(stream, ref m_abilityVfxSwapInfo);
//                DebugSerializeSizeBeforeSpawnSquares = stream.Position - position;
                stream.Serialize(ref num22);
                foreach (ActorData visibleException in LineOfSightVisibleExceptions)
                {
                    short actorIndex = (short) visibleException.ActorIndex;
                    stream.Serialize(ref actorIndex);
                }

                stream.Serialize(ref num23);
                stream.Serialize(ref num24);
                stream.Serialize(ref num25);
            }

            if (stream.isReading)
            {
                stream.Serialize(ref num1);
                stream.Serialize(ref num2);
                stream.Serialize(ref displayName);
                stream.Serialize(ref num6);
                stream.Serialize(ref bitField1);
                ServerClientUtils.GetBoolsFromBitfield(bitField1, out out0_1, out out1_1, out out2_1);
                stream.Serialize(ref num3);
                stream.Serialize(ref num4);
                stream.Serialize(ref num5);
                stream.Serialize(ref num7);
                stream.Serialize(ref num10);
                stream.Serialize(ref bitField2);
                bool out0_3 = false;
                bool out1_3 = false;
                bool out2_3 = false;
                bool out3_2 = false;
                bool out4_2 = false;
                bool out5 = false;
                bool out6 = false;
                bool out7 = false;
                ServerClientUtils.GetBoolsFromBitfield(bitField2, out out0_3, out out1_3, out out2_3, out out3_2,
                    out out4_2, out out5, out out6, out out7);
                if (out0_3)
                    stream.Serialize(ref num8);
                if (out1_3)
                    stream.Serialize(ref num9);
                if (out2_3)
                    stream.Serialize(ref num12);
                if (out3_2)
                    stream.Serialize(ref num13);
                if (out4_2)
                    stream.Serialize(ref num11);
                if (out5)
                    stream.Serialize(ref num14);
                if (out6)
                    stream.Serialize(ref num15);
                if (out7)
                {
                    stream.Serialize(ref num16);
                    stream.Serialize(ref num17);
                }

                stream.Serialize(ref num18);
                stream.Serialize(ref num19);
                stream.Serialize(ref num20);
                stream.Serialize(ref num21);
                stream.Serialize(ref bitField3);
                ServerClientUtils.GetBoolsFromBitfield(bitField3, out out0_2, out out1_2, out out2_2, out out3_1,
                    out out4_1);
                SerializeCharacterVisualInfo(stream, ref m_visualInfo);
                SerializeCharacterCardInfo(stream, ref m_selectedCards);
                SerializeCharacterModInfo(stream, ref m_selectedMods);
                SerializeCharacterAbilityVfxSwapInfo(stream, ref m_abilityVfxSwapInfo);
                PlayerIndex = num1;
                ActorIndex = num2;
                m_team = (Team) num6;
                SpawnerId = num21;

                m_displayName = displayName;
//                if (initialState)
//                    TeamSensitiveDataMatchmaker.Get().SetTeamSensitiveDataForActor(this);
                stream.Serialize(ref num22);
                m_lineOfSightVisibleExceptions.Clear();
                for (var index = 0; index < (int) num22; ++index)
                {
                    sbyte num26 = 0;
                    stream.Serialize(ref num26);
//                    ActorData actorByActorIndex = GameFlowDat.FindActorByActorIndex((int) num26);
//                    if (actorByActorIndex != null)
//                        LineOfSightVisibleExceptions.Add(actorByActorIndex);
                }

                stream.Serialize(ref num23);
                stream.Serialize(ref num24);
                stream.Serialize(ref num25);
                if (num23 > m_lastVisibleTurnToClient)
                    m_lastVisibleTurnToClient = num23;
                ServerLastKnownPosSquare = num24 != -1 || num25 != -1
                    ? Board.GetBoardSquare(num24, num25)
                    : null;
                m_ignoreFromAbilityHits = out3_1;
                m_alwaysHideNameplate = out4_1;
//                \u000E().MarkForRecalculateVisibility();
                m_showInGameHud = out1_2;
                VisibleTillEndOfPhase = out2_2;

                UnresolvedDamage = num8;
                UnresolvedHealing = num9;
                ReservedTechPoints = num11;
                UnresolvedTechPointGain = num12;
                UnresolvedTechPointLoss = num13;
                LastDeathTurn = num18;
                m_lastSpawnTurn = num19;
                NextRespawnTurn = num20;
                HasBotController = out0_2;
                AbsorbPoints = num14;
                TechPoints = num10;
                HitPoints = num7;
                MechanicPoints = num15;
                m_serverExpectedHoTTotal = num16;
                m_serverExpectedHoTThisTurn = num17;
                RemainingHorizontalMovement = num3;
                RemainingMovementWithQueuedAbility = num4;
                QueuedMovementAllowsAbility = out0_1;
                m_queuedMovementRequest = out1_1;
                m_queuedChaseRequest = out2_1;
            }

            return m_serializeHelper.End(initialState, syncVarDirtyBits);
        }

        public static void SerializeCharacterVisualInfo(
            IBitStream stream,
            ref CharacterVisualInfo visualInfo)
        {
            sbyte skinIndex = (sbyte) visualInfo.skinIndex;
            sbyte patternIndex = (sbyte) visualInfo.patternIndex;
            short colorIndex = (short) visualInfo.colorIndex;
            stream.Serialize(ref skinIndex);
            stream.Serialize(ref patternIndex);
            stream.Serialize(ref colorIndex);
            if (!stream.isReading)
                return;
            visualInfo.skinIndex = skinIndex;
            visualInfo.patternIndex = patternIndex;
            visualInfo.colorIndex = colorIndex;
        }

        public static void SerializeCharacterAbilityVfxSwapInfo(
            IBitStream stream,
            ref CharacterAbilityVfxSwapInfo abilityVfxSwapInfo)
        {
            short vfxSwapForAbility0 = (short) abilityVfxSwapInfo.VfxSwapForAbility0;
            short vfxSwapForAbility1 = (short) abilityVfxSwapInfo.VfxSwapForAbility1;
            short vfxSwapForAbility2 = (short) abilityVfxSwapInfo.VfxSwapForAbility2;
            short vfxSwapForAbility3 = (short) abilityVfxSwapInfo.VfxSwapForAbility3;
            short vfxSwapForAbility4 = (short) abilityVfxSwapInfo.VfxSwapForAbility4;
            stream.Serialize(ref vfxSwapForAbility0);
            stream.Serialize(ref vfxSwapForAbility1);
            stream.Serialize(ref vfxSwapForAbility2);
            stream.Serialize(ref vfxSwapForAbility3);
            stream.Serialize(ref vfxSwapForAbility4);
            if (!stream.isReading)
                return;
            abilityVfxSwapInfo.VfxSwapForAbility0 = vfxSwapForAbility0;
            abilityVfxSwapInfo.VfxSwapForAbility1 = vfxSwapForAbility1;
            abilityVfxSwapInfo.VfxSwapForAbility2 = vfxSwapForAbility2;
            abilityVfxSwapInfo.VfxSwapForAbility3 = vfxSwapForAbility3;
            abilityVfxSwapInfo.VfxSwapForAbility4 = vfxSwapForAbility4;
        }

        public static void SerializeCharacterCardInfo(IBitStream stream, ref CharacterCardInfo cardInfo)
        {
            sbyte num1 = 0;
            sbyte num2 = 0;
            sbyte num3 = 0;
            if (stream.isWriting)
            {
                sbyte prepCard = (sbyte) cardInfo.PrepCard;
                sbyte dashCard = (sbyte) cardInfo.DashCard;
                sbyte combatCard = (sbyte) cardInfo.CombatCard;
                stream.Serialize(ref prepCard);
                stream.Serialize(ref dashCard);
                stream.Serialize(ref combatCard);
            }
            else
            {
                stream.Serialize(ref num1);
                stream.Serialize(ref num2);
                stream.Serialize(ref num3);
                cardInfo.PrepCard = (CardType) num1;
                cardInfo.DashCard = (CardType) num2;
                cardInfo.CombatCard = (CardType) num3;
            }
        }

        public static void SerializeCharacterModInfo(IBitStream stream, ref CharacterModInfo modInfo)
        {
            sbyte num1 = -1;
            sbyte num2 = -1;
            sbyte num3 = -1;
            sbyte num4 = -1;
            sbyte num5 = -1;
            if (stream.isWriting)
            {
                sbyte modForAbility0 = (sbyte) modInfo.ModForAbility0;
                sbyte modForAbility1 = (sbyte) modInfo.ModForAbility1;
                sbyte modForAbility2 = (sbyte) modInfo.ModForAbility2;
                sbyte modForAbility3 = (sbyte) modInfo.ModForAbility3;
                sbyte modForAbility4 = (sbyte) modInfo.ModForAbility4;
                stream.Serialize(ref modForAbility0);
                stream.Serialize(ref modForAbility1);
                stream.Serialize(ref modForAbility2);
                stream.Serialize(ref modForAbility3);
                stream.Serialize(ref modForAbility4);
            }
            else
            {
                stream.Serialize(ref num1);
                stream.Serialize(ref num2);
                stream.Serialize(ref num3);
                stream.Serialize(ref num4);
                stream.Serialize(ref num5);
                modInfo.ModForAbility0 = num1;
                modInfo.ModForAbility1 = num2;
                modInfo.ModForAbility2 = num3;
                modInfo.ModForAbility3 = num4;
                modInfo.ModForAbility4 = num5;
            }
        }

        public string ToStringOrig()
        {
            return $"[ActorData: {m_displayName}, {method_28()}, ActorIndex: {m_actorIndex}, {m_team}] {PlayerData}";
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            stream.AlignTo();
            PlayerIndex = stream.ReadInt32();
            SerializedPlayerData = new SerializedComponent();
            SerializedPlayerData.DeserializeAsset(assetFile, stream);
            m_characterType = (CharacterType) stream.ReadInt32();
            m_tauntCamSetData = new SerializedComponent();
            m_tauntCamSetData.DeserializeAsset(assetFile, stream);
            m_aliveHUDIconResourceString = stream.ReadString32();
            m_deadHUDIconResourceString = stream.ReadString32();
            m_screenIndicatorIconResourceString = stream.ReadString32();
            m_screenIndicatorBWIconResourceString = stream.ReadString32();
            m_actionSkinPrefabLink = new SerializedPrefabResourceLink();
            m_actionSkinPrefabLink.DeserializeAsset(assetFile, stream);
            m_visualInfo = new CharacterVisualInfo();
            m_visualInfo.DeserializeAsset(assetFile, stream);
            m_abilityVfxSwapInfo = new CharacterAbilityVfxSwapInfo();
            m_abilityVfxSwapInfo.DeserializeAsset(assetFile, stream);
            m_selectedMods = new CharacterModInfo();
            m_selectedMods.DeserializeAsset(assetFile, stream);
            m_selectedAbilityVfxSwaps = new CharacterAbilityVfxSwapInfo();
            m_selectedAbilityVfxSwaps.DeserializeAsset(assetFile, stream);
            m_selectedCards = new CharacterCardInfo();
            m_selectedCards.DeserializeAsset(assetFile, stream);
            m_availableTauntIDs = new SerializedVector<int>();
            m_availableTauntIDs.DeserializeAsset(assetFile, stream);
            m_maxHitPoints = stream.ReadInt32();
            m_hitPointRegen = stream.ReadInt32();
            m_maxTechPoints = stream.ReadInt32();
            m_techPointRegen = stream.ReadInt32();
            m_techPointsOnSpawn = stream.ReadInt32();
            m_techPointsOnRespawn = stream.ReadInt32();
            m_maxHorizontalMovement = stream.ReadSingle();
            m_postAbilityHorizontalMovement = stream.ReadSingle();
            m_maxVerticalUpwardMovement = stream.ReadInt32();
            m_maxVerticalDownwardMovement = stream.ReadInt32();
            m_sightRange = stream.ReadSingle();
            m_runSpeed = stream.ReadSingle();
            m_vaultSpeed = stream.ReadSingle();
            m_knockbackSpeed = stream.ReadSingle();
            m_onDeathAudioEvent = stream.ReadString32();
            m_additionalNetworkObjectsToRegister = new SerializedVector<SerializedComponent>();
            m_additionalNetworkObjectsToRegister.DeserializeAsset(assetFile, stream);
        }

        public override string ToString()
        {
            return $"{nameof(ActorData)}(" +
                   $"{nameof(PlayerIndex)}: {PlayerIndex}, " +
                   $"{nameof(PlayerData)}: {PlayerData}, " +
                   $"{nameof(m_characterType)}: {m_characterType}, " +
                   $"{nameof(m_tauntCamSetData)}: {m_tauntCamSetData}, " +
                   $"{nameof(m_aliveHUDIconResourceString)}: {m_aliveHUDIconResourceString}, " +
                   $"{nameof(m_deadHUDIconResourceString)}: {m_deadHUDIconResourceString}, " +
                   $"{nameof(m_screenIndicatorIconResourceString)}: {m_screenIndicatorIconResourceString}, " +
                   $"{nameof(m_screenIndicatorBWIconResourceString)}: {m_screenIndicatorBWIconResourceString}, " +
                   $"{nameof(m_actionSkinPrefabLink)}: {m_actionSkinPrefabLink}, " +
                   $"{nameof(m_visualInfo)}: {m_visualInfo}, " +
                   $"{nameof(m_abilityVfxSwapInfo)}: {m_abilityVfxSwapInfo}, " +
                   $"{nameof(m_selectedMods)}: {m_selectedMods}, " +
                   $"{nameof(m_selectedAbilityVfxSwaps)}: {m_selectedAbilityVfxSwaps}, " +
                   $"{nameof(m_selectedCards)}: {m_selectedCards}, " +
                   $"{nameof(m_availableTauntIDs)}: {m_availableTauntIDs}, " +
                   $"{nameof(m_maxHitPoints)}: {m_maxHitPoints}, " +
                   $"{nameof(m_hitPointRegen)}: {m_hitPointRegen}, " +
                   $"{nameof(m_maxTechPoints)}: {m_maxTechPoints}, " +
                   $"{nameof(m_techPointRegen)}: {m_techPointRegen}, " +
                   $"{nameof(m_techPointsOnSpawn)}: {m_techPointsOnSpawn}, " +
                   $"{nameof(m_techPointsOnRespawn)}: {m_techPointsOnRespawn}, " +
                   $"{nameof(m_maxHorizontalMovement)}: {m_maxHorizontalMovement}, " +
                   $"{nameof(m_postAbilityHorizontalMovement)}: {m_postAbilityHorizontalMovement}, " +
                   $"{nameof(m_maxVerticalUpwardMovement)}: {m_maxVerticalUpwardMovement}, " +
                   $"{nameof(m_maxVerticalDownwardMovement)}: {m_maxVerticalDownwardMovement}, " +
                   $"{nameof(m_sightRange)}: {m_sightRange}, " +
                   $"{nameof(m_runSpeed)}: {m_runSpeed}, " +
                   $"{nameof(m_vaultSpeed)}: {m_vaultSpeed}, " +
                   $"{nameof(m_knockbackSpeed)}: {m_knockbackSpeed}, " +
                   $"{nameof(m_onDeathAudioEvent)}: {m_onDeathAudioEvent}, " +
                   $"{nameof(m_additionalNetworkObjectsToRegister)}: {m_additionalNetworkObjectsToRegister.Count} entries" +
                   ")";
        }

        public delegate void ActorDataDelegate();

        public enum IntType
        {
            HP,
            TP,
            ABSORB,
            MAX_HP,
            MAX_TP,
            HP_UNRESOLVED_DAMAGE,
            HP_UNRESOLVED_HEALING,
            NUM_TYPES
        }

        public enum NetChannelIdOffset
        {
            Default,
            ActorData,
            Count
        }

        public enum MovementType
        {
            None = -1, // 0xFFFFFFFF
            Normal = 0,
            Teleport = 1,
            Knockback = 2,
            Charge = 3,
            Flight = 4,
            WaypointFlight = 5
        }

        public enum TeleportType
        {
            NotATeleport,
            InitialSpawn,
            Respawn,
            Evasion_DontAdjustToVision,
            Evasion_AdjustToVision,
            Reappear,
            Failsafe,
            u001D,
            TricksterAfterImage
        }

        public enum MovementChangeType
        {
            MoreMovement,
            LessMovement
        }
    }
}
