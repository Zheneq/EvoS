using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Mime;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Misc
{
    public abstract class Sequence : MonoBehaviour
    {
        public static string s_defaultHitAttachJoint = "upperRoot_JNT";
        public static string s_defaultFallbackHitAttachJoint = "root_JNT";
        private static IExtraSequenceParams[] s_emptyParams = new IExtraSequenceParams[0];
        private static string c_casterToken = "<color=white>[Caster]</color>";
        private static string c_targetActorToken = "<color=white>[TargetActor]</color>";
        private static string c_targetPosToken = "<color=lightblue>[TargetPos]</color>";
        private static string c_seqPosToken = "<color=lightblue>[SeqPos]</color>";
        private static string c_clientActorToken = "<color=white>[ClientActor]</color>";

        public static string s_seqPosNote =
            "note: <color=lightblue>[SeqPos]</color> is usually only relevant for projectiles which is projectile's current position\n";

        public static string s_targetPosNote =
            "note: <color=lightblue>[TargetPos]</color> is passed in from ability, usually Caster or Target's position for attached vfx, or end position for projectiles\n";

//  [Separator("For Hit React Animation", true)]
        public bool m_targetHitAnimation = true;

        public string m_customHitReactTriggerName = string.Empty;

//  [Space(5f)]
        public bool m_turnOffVFXDuringCinematicCam = true;
        public int m_keepCasterVFXForAnimIndex = -1;
        public bool m_keepCasterVFXForTurnOfSpawnOnly = true;
        internal int m_casterId = ActorData.s_invalidActorIndex;
        private bool m_waitForClientEnable = true;
        private bool m_lastSetVisibleValue = true;
        public SequenceNotes m_setupNotes;

        public bool m_canTriggerHitReactOnAllyHit;

//  [Separator("Visibility (please don't forget me T_T ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~)", true)]
//  [Tooltip("What visibility rules to use for this sequence")]
        public VisibilityType m_visibilityType;

//  [Tooltip("What visibility rules to use for the hitFX")]
        public HitVisibilityType m_hitVisibilityType;

        public PhaseBasedVisibilityType m_phaseVisibilityType;

//  [Separator("How to Hide Vfx Object", true)]
        public SequenceHideType m_sequenceHideType;

//  [Separator("For keeping VFX in Caster Taunts", true)]
        public bool m_keepVFXInCinematicCamForCaster;

//  [Header("-- For Debugging Only --")]
        public bool m_debugFlag;
        private ActorData m_caster;
        private BoardSquare m_targetBoardSquare;
        private GameObject m_fxParent;
        private List<GameObject> m_parentedFXs;
        protected bool m_forceAlwaysVisible;
        protected float m_startTime;
        protected bool m_initialized;
        private bool m_debugHasReceivedAnimEventBeforeReady;
        public const string c_editorDescWarning = "<color=yellow>WARNING: </color>";
        public const string c_canDoGameplayHit = "<color=cyan>Can do Gameplay Hits</color>\n";
        public const string c_ignoreGameplayHit = "Ignoring Gameplay Hits\n";

//        public GameObject GetReferenceModel(
//            ActorData referenceActorData,
//            ReferenceModelType referenceModelType)
//        {
//            GameObject gameObject = null;
//            switch (referenceModelType)
//            {
//                case ReferenceModelType.Actor:
//                    if (referenceActorData != null)
//                    {
//                        gameObject = referenceActorData.gameObject;
//                    }
//
//                    break;
//                case ReferenceModelType.TempSatellite:
//                    gameObject = SequenceManager.Get().FindTempSatellite(Source);
//                    break;
//                case ReferenceModelType.PersistentSatellite1:
//                    if (referenceActorData != null)
//                    {
//                        SatelliteController component = referenceActorData.GetComponent<SatelliteController>();
//                        if (component != null && component.GetSatellite(0) != null)
//                        {
//                            gameObject = component.GetSatellite(0).gameObject;
//                        }
//                    }
//
//                    break;
//            }
//
//            return gameObject;
//        }

        public short PrefabLookupId { get; private set; }

        public void InitPrefabLookupId(short lookupId)
        {
            PrefabLookupId = lookupId;
        }

        internal ActorData[] Targets { get; set; }

        internal ActorData Target
        {
            get
            {
                if (Targets != null && Targets.Length != 0)
                    return Targets[0];
                return null;
            }
        }

        internal ActorData Caster
        {
            get { return m_caster; }
            set
            {
                m_caster = value;
                if (!(value != null))
                    return;
                m_casterId = value.ActorIndex;
            }
        }

        internal BoardSquare TargetSquare
        {
            get { return m_targetBoardSquare; }
            set { m_targetBoardSquare = value; }
        }

        internal Vector3 TargetPos { get; set; }

        internal Quaternion TargetRotation { get; set; }

        internal int AgeInTurns { get; set; }

        internal bool Ready
        {
            get
            {
                if (m_initialized && enabled)
                    return !MarkedForRemoval;
                return false;
            }
        }

        internal bool InitializedEver { get; private set; }

        internal bool MarkedForRemoval { get; private set; }

        internal void MarkForRemoval()
        {
            MarkedForRemoval = true;
            enabled = false;
        }

        internal static void MarkSequenceArrayForRemoval(Sequence[] sequencesArray)
        {
            if (sequencesArray == null)
                return;
            for (int index = 0; index < sequencesArray.Length; ++index)
            {
                if (sequencesArray[index] != null)
                    sequencesArray[index].MarkForRemoval();
            }
        }

        internal bool RequestsHitAnimation(ActorData target)
        {
            if (!m_targetHitAnimation || target == null || Caster == null)
                return false;
            if (!m_canTriggerHitReactOnAllyHit)
                return Caster.method_77() == target.method_76();
            return true;
        }

        internal bool RemoveAtTurnEnd { get; set; }

        internal int Id { get; set; }

        internal SequenceSource Source { get; private set; }

        protected virtual void Awake()
        {
        }

//        protected virtual void Start()
//        {
//            m_startTime = GameTime.time;
//        }

//        internal void OnDoClientEnable()
//        {
//            if (!m_waitForClientEnable)
//                return;
//            FinishSetup();
//            DoneInitialization();
//            ProcessSequenceVisibility();
//            if (SequenceManager.SequenceDebugTraceOn)
//                Debug.LogWarning(
//                    $"<color=yellow>Client Enable: </color><<color=lightblue>{gameObject.name} | {GetType()}</color>> @time= {GameTime.time}");
//            enabled = true;
//        }
//
//        protected virtual void OnDestroy()
//        {
//            if (m_parentedFXs != null)
//            {
//                foreach (UnityEngine.Object parentedFx in m_parentedFXs)
//                    UnityEngine.Object.Destroy(parentedFx);
//            }
//
//            if (!(SequenceManager.Get() != null))
//                return;
//            SequenceManager.Get().OnDestroySequence(this);
//        }

        private void DoneInitialization()
        {
            if (InitializedEver)
                return;
            m_initialized = true;
            InitializedEver = true;
        }

        internal virtual void Initialize(IExtraSequenceParams[] extraParams)
        {
        }

        public virtual void FinishSetup()
        {
        }

//        internal void BaseInitialize_Client(
//            BoardSquare targetSquare,
//            Vector3 targetPos,
//            Quaternion targetRotation,
//            ActorData[] targets,
//            ActorData caster,
//            int id,
//            GameObject prefab,
//            short baseSequenceLookupId,
//            SequenceSource source,
//            IExtraSequenceParams[] extraParams)
//        {
//            RemoveAtTurnEnd = source.RemoveAtEndOfTurn;
//            TargetSquare = targetSquare;
//            TargetPos = targetPos;
//            TargetRotation = targetRotation;
//            Targets = targets;
//            Caster = caster;
//            Id = id;
//            Source = source;
//            InitPrefabLookupId(baseSequenceLookupId);
//            m_startTime = GameTime.time;
//            Initialize(extraParams ?? s_emptyParams);
//            m_waitForClientEnable = Source.WaitForClientEnable;
//            enabled = !m_waitForClientEnable;
//            if (m_waitForClientEnable)
//                return;
//            FinishSetup();
//            DoneInitialization();
//        }

        protected virtual void OnStopVfxOnClient()
        {
        }

//        private void OnActorAdded(ActorData actor)
//        {
//            if (InitializedEver || !enabled || (MarkedForRemoval || !(m_caster == null)) ||
//                m_casterId == ActorData.s_invalidActorIndex)
//                return;
//            m_caster = GameFlowData.Get().FindActorByActorIndex(m_casterId);
//            if (m_caster == null)
//                return;
//            FinishSetup();
//            DoneInitialization();
//            GameFlowData.s_onAddActor -= OnActorAdded;
//        }

//        internal bool IsCasterOrTargetsVisible()
//        {
//            bool flag = false;
//            if (IsActorConsideredVisible(Caster))
//                flag = true;
//            else if (Targets != null)
//            {
//                foreach (ActorData target in Targets)
//                {
//                    if (IsActorConsideredVisible(target))
//                    {
//                        flag = true;
//                        break;
//                    }
//                }
//            }
//
//            return flag;
//        }

        internal virtual Vector3 GetSequencePos()
        {
            return TargetPos;
        }

//        internal bool IsSequencePosVisible()
//        {
//            bool flag = false;
//            FogOfWar clientFog = FogOfWar.GetClientFog();
//            if (clientFog != null)
//            {
//                if ((double) GetSequencePos().magnitude > 0.0 &&
//                    (!Board.smethod_0().m_showLOS || clientFog.IsVisible(Board.smethod_0().method_7(GetSequencePos()))))
//                    flag = true;
//            }
//            else
//                flag = true;
//
//            return flag;
//        }
//
//        internal bool IsTargetPosVisible()
//        {
//            bool flag = false;
//            FogOfWar clientFog = FogOfWar.GetClientFog();
//            if (clientFog != null)
//            {
//                if (clientFog.IsVisible(Board.smethod_0().method_7(TargetPos)))
//                    flag = true;
//            }
//            else
//                flag = true;
//
//            return flag;
//        }
//
//        internal bool IsCasterOrTargetPosVisible()
//        {
//            bool flag = false;
//            FogOfWar clientFog = FogOfWar.GetClientFog();
//            if (clientFog != null)
//            {
//                if (IsActorConsideredVisible(Caster))
//                    flag = true;
//                else if (clientFog.IsVisible(Board.smethod_0().method_7(TargetPos)))
//                    flag = true;
//            }
//            else
//                flag = true;
//
//            return flag;
//        }
//
//        internal bool IsCasterOrTargetOrTargetPosVisible()
//        {
//            bool flag = false;
//            FogOfWar clientFog = FogOfWar.GetClientFog();
//            if (clientFog != null)
//            {
//                if (IsActorConsideredVisible(Caster))
//                    flag = true;
//                else if (clientFog.IsVisible(Board.smethod_0().method_7(TargetPos)))
//                    flag = true;
//                else if (Targets != null)
//                {
//                    foreach (ActorData target in Targets)
//                    {
//                        if (IsActorConsideredVisible(target))
//                        {
//                            flag = true;
//                            break;
//                        }
//                    }
//                }
//            }
//            else
//                flag = true;
//
//            return flag;
//        }
//
//        protected void SetSequenceVisibility(bool visible)
//        {
//            m_lastSetVisibleValue = visible;
//            if (!(m_fxParent != null) || m_parentedFXs == null)
//                return;
//            if (m_sequenceHideType == SequenceHideType.MoveOffCamera_KeepEnabled)
//                m_fxParent.transform.localPosition = !visible ? -10000f * Vector3.one : Vector3.Zero;
//            else
//                m_fxParent.SetActive(visible);
//            for (int index = 0; index < m_parentedFXs.Count; ++index)
//            {
//                GameObject parentedFx = m_parentedFXs[index];
//                if ((bool) (parentedFx))
//                {
//                    if (m_sequenceHideType == SequenceHideType.MoveOffCamera_KeepEnabled)
//                    {
//                        parentedFx.transform.localPosition = !visible ? -10000f * Vector3.one : Vector3.Zero;
//                    }
//                    else
//                    {
//                        if (m_sequenceHideType == SequenceHideType.KillThenDisable && !visible)
//                        {
//                            foreach (PKFxFX componentsInChild in parentedFx.GetComponentsInChildren<PKFxFX>(true))
//                                componentsInChild.KillEffect();
//                        }
//
//                        parentedFx.SetActive(visible);
//                    }
//                }
//            }
//        }

        protected bool LastDesiredVisible()
        {
            return m_lastSetVisibleValue;
        }

//        protected bool IsActorConsideredVisible(ActorData actor)
//        {
//            if (!(actor != null) || !actor.method_48())
//                return false;
//            if (!(actor.method_4() == null))
//                return actor.method_4().IsVisibleToClient();
//            return true;
//        }
//
//        protected void ProcessSequenceVisibility()
//        {
//            if (m_initialized && !MarkedForRemoval)
//            {
//                if (m_phaseVisibilityType != PhaseBasedVisibilityType.Any && GameFlowData.Get() != null)
//                {
//                    GameState gameState = GameFlowData.Get().gameState;
//                    if (m_phaseVisibilityType == PhaseBasedVisibilityType.InDecisionOnly &&
//                        gameState != GameState.BothTeams_Decision ||
//                        m_phaseVisibilityType == PhaseBasedVisibilityType.InResolutionOnly &&
//                        gameState != GameState.BothTeams_Resolve)
//                    {
//                        SetSequenceVisibility(false);
//                        return;
//                    }
//                }
//
//                bool flag1;
//                if ((flag1 = m_turnOffVFXDuringCinematicCam && (bool) (CameraManager.Get()) &&
//                             CameraManager.Get().InCinematic()) && m_keepVFXInCinematicCamForCaster && Caster != null)
//                {
//                    ActorData cinematicTargetActor = CameraManager.Get().GetCinematicTargetActor();
//                    int cinematicActionAnimIndex = CameraManager.Get().GetCinematicActionAnimIndex();
//                    bool flag2 = m_keepCasterVFXForAnimIndex <= 0 ||
//                                 m_keepCasterVFXForAnimIndex == cinematicActionAnimIndex;
//                    bool flag3 = !m_keepCasterVFXForTurnOfSpawnOnly || AgeInTurns == 0;
//                    if (cinematicTargetActor.ActorIndex == Caster.ActorIndex && flag2 && flag3)
//                        flag1 = false;
//                }
//
//                ActorData activeOwnedActorData = GameFlowData.Get().activeOwnedActorData;
//                PlayerData localPlayerData = GameFlowData.Get().LocalPlayerData;
//                if (flag1)
//                    SetSequenceVisibility(false);
//                else if (activeOwnedActorData == null && localPlayerData == null)
//                    SetSequenceVisibility(true);
//                else if (m_forceAlwaysVisible)
//                    SetSequenceVisibility(true);
//                else if (m_visibilityType == VisibilityType.Caster)
//                {
//                    if (Caster != null)
//                        SetSequenceVisibility(IsActorConsideredVisible(Caster));
//                    else
//                        SetSequenceVisibility(true);
//                }
//                else if (m_visibilityType == VisibilityType.CasterOrTarget)
//                    SetSequenceVisibility(IsCasterOrTargetsVisible());
//                else if (m_visibilityType == VisibilityType.CasterOrTargetPos)
//                    SetSequenceVisibility(IsCasterOrTargetPosVisible());
//                else if (m_visibilityType == VisibilityType.CasterOrTargetOrTargetPos)
//                    SetSequenceVisibility(IsCasterOrTargetOrTargetPosVisible());
//                else if (m_visibilityType == VisibilityType.Target)
//                    SetSequenceVisibility(Targets != null && Targets.Length > 0 &&
//                                          IsActorConsideredVisible(Targets[0]));
//                else if (m_visibilityType == VisibilityType.TargetPos)
//                    SetSequenceVisibility(IsTargetPosVisible());
//                else if (m_visibilityType == VisibilityType.AlwaysOnlyIfCaster)
//                    SetSequenceVisibility(Caster == activeOwnedActorData);
//                else if (m_visibilityType == VisibilityType.AlwaysOnlyIfTarget)
//                    SetSequenceVisibility(Target == activeOwnedActorData);
//                else if (m_visibilityType == VisibilityType.AlwaysIfCastersTeam)
//                {
//                    if (Caster != null && activeOwnedActorData != null)
//                        SetSequenceVisibility(Caster.method_76() == activeOwnedActorData.method_76());
//                    else
//                        SetSequenceVisibility(true);
//                }
//                else if (m_visibilityType == VisibilityType.SequencePosition)
//                    SetSequenceVisibility(IsSequencePosVisible());
//                else if (m_visibilityType == VisibilityType.Always)
//                    SetSequenceVisibility(true);
//                else if (m_visibilityType == VisibilityType.CastersTeamOrSequencePosition)
//                {
//                    if (Caster != null && activeOwnedActorData != null)
//                        SetSequenceVisibility(Caster.method_76() == activeOwnedActorData.method_76() ||
//                                              IsSequencePosVisible());
//                    else
//                        SetSequenceVisibility(IsSequencePosVisible());
//                }
//                else if (m_visibilityType == VisibilityType.TargetPosAndCaster)
//                {
//                    SetSequenceVisibility(IsTargetPosVisible() && (Caster == null || IsActorConsideredVisible(Caster)));
//                }
//                else
//                {
//                    if (m_visibilityType != VisibilityType.TargetIfNotEvading &&
//                        m_visibilityType != VisibilityType.CasterIfNotEvading)
//                        return;
//                    bool visible = false;
//                    ActorData actor = m_visibilityType != VisibilityType.TargetIfNotEvading
//                        ? Caster
//                        : (Targets == null || Targets.Length <= 0 ? null : Targets[0]);
//                    if (actor != null && IsActorConsideredVisible(actor))
//                        visible = actor.method_9() == null || !actor.method_9().InChargeState();
//                    SetSequenceVisibility(visible);
//                }
//            }
//            else
//                SetSequenceVisibility(false);
//        }

        public bool HasReceivedAnimEventBeforeReady
        {
            get { return m_debugHasReceivedAnimEventBeforeReady; }
            set { m_debugHasReceivedAnimEventBeforeReady = value; }
        }

        internal virtual void OnTurnStart(int currentTurn)
        {
        }

        internal virtual void OnAbilityPhaseStart(AbilityPriority abilityPhase)
        {
        }

//  protected virtual void OnAnimationEvent(UnityEngine.Object parameter, GameObject sourceObject)
//  {
//  }

        internal virtual void SetTimerController(int value)
        {
        }

//  internal void AnimationEvent(UnityEngine.Object eventObject, GameObject sourceObject)
//  {
//    if (!Ready)
//      return;
//    OnAnimationEvent(eventObject, sourceObject);
//  }

//  protected void CallHitSequenceOnTargets(
//    Vector3 impactPos,
//    float defaultImpulseRadius = 1f,
//    List<ActorData> actorsToIgnore = null,
//    bool tryHitReactIfAlreadyHit = true)
//  {
//    float num = 0.0f;
//    if (Targets != null)
//    {
//      for (int index = 0; index < Targets.Length; ++index)
//      {
//        if (Targets[index] != null)
//        {
//          float magnitude = (Targets[index].transform.position - impactPos).magnitude;
//          if (magnitude > (double) num)
//            num = magnitude;
//        }
//      }
//    }
//    float explosionRadius = (double) num >= (double) Board.smethod_0().squareSize / 2.0 ? num : defaultImpulseRadius;
//    ActorModelData.ImpulseInfo impulseInfo = new ActorModelData.ImpulseInfo(explosionRadius, impactPos);
//    if (Targets != null)
//    {
//      for (int index = 0; index < Targets.Length; ++index)
//      {
//        if (Targets[index] != null && (actorsToIgnore == null || !actorsToIgnore.Contains(Targets[index])))
//          Source.OnSequenceHit(this, Targets[index], impulseInfo, ActorModelData.RagdollActivation.HealthBased, tryHitReactIfAlreadyHit);
//      }
//    }
//    Source.OnSequenceHit(this, TargetPos, impulseInfo);
//    List<ActorData> actorsInRadius = AreaEffectUtils.GetActorsInRadius(impactPos, explosionRadius * 3f, false, Caster, new List<Team>
//    {
//      Caster.method_77(),
//      Caster.method_76()
//    }, (List<NonActorTargetInfo>) null, false, new Vector3());
//    for (int index = 0; index < actorsInRadius.Count; ++index)
//    {
//      if (actorsInRadius[index] != null && actorsInRadius[index].method_4() != null)
//      {
//        Vector3 direction = actorsInRadius[index].transform.position - impactPos;
//        direction.y = 0.0f;
//        direction.Normalize();
//        actorsInRadius[index].method_4().ImpartWindImpulse(direction);
//      }
//    }
//  }

//        protected Vector3 GetTargetHitPosition(ActorData actorData, JointPopupProperty fxJoint)
//        {
//            Vector3 vector3 = Vector3.Zero;
//            for (int targetIndex = 0; targetIndex < Targets.Length; ++targetIndex)
//            {
//                if (Targets[targetIndex] == actorData)
//                {
//                    vector3 = GetTargetHitPosition(targetIndex, fxJoint);
//                    break;
//                }
//            }
//
//            return vector3;
//        }

//        protected Vector3 GetTargetHitPosition(int targetIndex, JointPopupProperty fxJoint)
//        {
//            bool flag = false;
//            Vector3 vector3 = Vector3.Zero;
//            if (Targets != null && Targets.Length > targetIndex &&
//                (Targets[targetIndex] != null && Targets[targetIndex].gameObject != null))
//            {
//                fxJoint.Initialize(Targets[targetIndex].gameObject);
//                if (fxJoint.m_jointObject != null)
//                {
//                    vector3 = fxJoint.m_jointObject.transform.position;
//                    flag = true;
//                }
//            }
//
//            if (!flag)
//                vector3 = GetTargetHitPosition(targetIndex);
//            return vector3;
//        }
//
//        protected Vector3 GetTargetHitPosition(int targetIndex)
//        {
//            return GetTargetPosition(targetIndex, true);
//        }
//
//        protected Vector3 GetTargetPosition(int targetIndex, bool secondaryActorHits = false)
//        {
//            if (secondaryActorHits && Targets != null &&
//                (Targets.Length > targetIndex && Targets[targetIndex] != null) &&
//                Targets[targetIndex].gameObject != null)
//            {
//                GameObject inChildren1 = Targets[targetIndex].gameObject.FindInChildren(s_defaultHitAttachJoint, 0);
//                if (inChildren1 != null)
//                    return inChildren1.transform.position + Vector3.up;
//                GameObject inChildren2 =
//                    Targets[targetIndex].gameObject.FindInChildren(s_defaultFallbackHitAttachJoint, 0);
//                if (inChildren2 != null)
//                    return inChildren2.transform.position + Vector3.up;
//                return Targets[targetIndex].gameObject.transform.position;
//            }
//
//            if (TargetSquare != null)
//                return TargetSquare.ToVector3() + Vector3.up;
//            return TargetPos;
//        }
//
//        protected bool IsHitFXVisible(ActorData hitTarget)
//        {
//            bool flag;
//            switch (m_hitVisibilityType)
//            {
//                case HitVisibilityType.Always:
//                    if (hitTarget != null && Caster != null)
//                    {
//                        ActorData activeOwnedActorData = GameFlowData.Get().activeOwnedActorData;
//                        if (activeOwnedActorData != null && hitTarget.method_76() == Caster.method_76() &&
//                            activeOwnedActorData.method_76() != hitTarget.method_76())
//                            return IsActorConsideredVisible(hitTarget);
//                        return true;
//                    }
//
//                    flag = true;
//                    break;
//                case HitVisibilityType.Target:
//                    flag = (bool) (hitTarget) && IsActorConsideredVisible(hitTarget);
//                    break;
//                default:
//                    flag = true;
//                    break;
//            }
//
//            return flag;
//        }
//
//        public bool IsHitFXVisibleWrtTeamFilter(ActorData hitTarget, HitVFXSpawnTeam teamFilter)
//        {
//            bool flag1;
//            if ((flag1 = IsHitFXVisible(hitTarget)) && Caster != null &&
//                (hitTarget != null && teamFilter != HitVFXSpawnTeam.AllTargets))
//            {
//                bool flag2 = Caster.method_76() == hitTarget.method_76();
//                flag1 = teamFilter == HitVFXSpawnTeam.AllTargets ||
//                        teamFilter == HitVFXSpawnTeam.AllExcludeCaster && Caster != hitTarget ||
//                        teamFilter == HitVFXSpawnTeam.AllyAndCaster && flag2 ||
//                        teamFilter == HitVFXSpawnTeam.EnemyOnly && !flag2;
//            }
//
//            return flag1;
//        }
//
//        public bool ShouldHideForActorIfAttached(ActorData actor)
//        {
//            if (actor != null)
//                return actor.method_43();
//            return false;
//        }
//
//        protected void InitializeFXStorage()
//        {
//            if (m_fxParent == null)
//            {
//                m_fxParent = new GameObject("fxParent_" + GetType());
//                m_fxParent.transform.parent = transform;
//            }
//
//            if (m_parentedFXs != null)
//                return;
//            m_parentedFXs = new List<GameObject>();
//        }
//
//        protected GameObject GetFxParentObject()
//        {
//            return m_fxParent;
//        }
//
//        internal GameObject InstantiateFX(GameObject prefab)
//        {
//            return InstantiateFX(prefab, Vector3.Zero, Quaternion.Identity, false);
//        }
//
//        internal GameObject InstantiateFX(
//            GameObject prefab,
//            Vector3 position,
//            Quaternion rotation,
//            bool tryApplyCameraOffset = true,
//            bool logErrorOnNullPrefab = true)
//        {
//            if (m_fxParent == null)
//                InitializeFXStorage();
//            if (tryApplyCameraOffset && prefab != null && (bool) (prefab.GetComponent<OffsetVFXTowardsCamera>()))
//                position = OffsetVFXTowardsCamera.ProcessOffset(position);
//            GameObject vfxInstanceRoot;
//            if (prefab != null)
//            {
//                vfxInstanceRoot = UnityEngine.Object.Instantiate<GameObject>(prefab, position, rotation);
//            }
//            else
//            {
//                vfxInstanceRoot = new GameObject("FallbackForNullFx");
//                if (MediaTypeNames.Application.isEditor && logErrorOnNullPrefab)
//                    Debug.LogError((gameObject.name + " Trying to instantiate null FX prefab"));
//            }
//
//            ReplaceVFXPrefabs(vfxInstanceRoot);
//            vfxInstanceRoot.transform.parent = m_fxParent.transform;
//            vfxInstanceRoot.gameObject.SetLayerRecursively((LayerMask) LayerMask.NameToLayer("DynamicLit"));
//            return vfxInstanceRoot;
//        }
//
//        private void ReplaceVFXPrefabs(GameObject vfxInstanceRoot)
//        {
//            GameEventManager gameEventManager = GameEventManager.Get();
//            if (gameEventManager == null || !(Caster != null))
//                return;
//            gameEventManager.FireEvent(GameEventManager.EventType.ReplaceVFXPrefab,
//                (GameEventManager.GameEventArgs) new GameEventManager.ReplaceVFXPrefab
//                {
//                    characterResourceLink = Caster.method_99(),
//                    characterVisualInfo = Caster.m_visualInfo,
//                    characterAbilityVfxSwapInfo = Caster.m_abilityVfxSwapInfo,
//                    vfxRoot = vfxInstanceRoot.transform
//                });
//        }
//
//        internal static void SetAttribute(GameObject fx, string name, int value)
//        {
//            if (!(fx != null))
//                return;
//            foreach (PKFxFX componentsInChild in fx.GetComponentsInChildren<PKFxFX>(true))
//            {
//                if (componentsInChild != null)
//                {
//                    PKFxManager.AttributeDesc desc = new PKFxManager.AttributeDesc(PKFxManager.BaseType.Int, name);
//                    if (componentsInChild.AttributeExists(desc))
//                        componentsInChild.SetAttribute(new PKFxManager.Attribute(desc)
//                        {
//                            m_Value0 = (float) value
//                        });
//                }
//            }
//        }
//
//        internal static void SetAttribute(GameObject fx, string name, float value)
//        {
//            if (fx == null)
//                return;
//            foreach (PKFxFX componentsInChild in fx.GetComponentsInChildren<PKFxFX>(true))
//            {
//                if (componentsInChild != null)
//                {
//                    PKFxManager.AttributeDesc desc = new PKFxManager.AttributeDesc(PKFxManager.BaseType.Float, name);
//                    if (componentsInChild.AttributeExists(desc))
//                        componentsInChild.SetAttribute(new PKFxManager.Attribute(desc)
//                        {
//                            m_Value0 = value
//                        });
//                }
//            }
//        }
//
//        internal static void SetAttribute(GameObject fx, string name, Vector3 value)
//        {
//            if (fx == null)
//                return;
//            foreach (PKFxFX componentsInChild in fx.GetComponentsInChildren<PKFxFX>(true))
//            {
//                if (componentsInChild != null)
//                {
//                    PKFxManager.AttributeDesc desc = new PKFxManager.AttributeDesc(PKFxManager.BaseType.Float3, name);
//                    if (componentsInChild.AttributeExists(desc))
//                        componentsInChild.SetAttribute(new PKFxManager.Attribute(desc)
//                        {
//                            m_Value0 = value.x,
//                            m_Value1 = value.y,
//                            m_Value2 = value.z
//                        });
//                }
//            }
//        }
//
//        internal bool AreFXFinished(GameObject fx)
//        {
//            if (!Source.RemoveAtEndOfTurn)
//                return false;
//            bool flag = false;
//            if (fx != null)
//            {
//                if (!fx.activeSelf)
//                    flag = true;
//                else if (fx.GetComponent<ParticleSystem>() != null && !fx.GetComponent<ParticleSystem>().IsAlive())
//                {
//                    flag = true;
//                }
//                else
//                {
//                    PKFxFX[] componentsInChildren = fx.GetComponentsInChildren<PKFxFX>(true);
//                    if (componentsInChildren.Length > 0)
//                    {
//                        flag = true;
//                        foreach (PKFxFX pkFxFx in componentsInChildren)
//                        {
//                            if (pkFxFx.Alive())
//                            {
//                                flag = false;
//                                break;
//                            }
//                        }
//                    }
//                }
//            }
//
//            return flag;
//        }
//
//        internal static float GetFXDuration(GameObject fxPrefab)
//        {
//            float num = 1f;
//            if (fxPrefab != null)
//            {
//                ParticleSystem component = fxPrefab.GetComponent<ParticleSystem>();
//                if (component != null)
//                {
//                    num = component.main.duration;
//                }
//                else
//                {
//                    PKFxManager.AttributeDesc desc =
//                        new PKFxManager.AttributeDesc(PKFxManager.BaseType.Float, "Duration");
//                    desc.DefaultValue0 = 1f;
//                    PKFxFX[] componentsInChildren = fxPrefab.GetComponentsInChildren<PKFxFX>(true);
//                    if (componentsInChildren.Length > 0)
//                    {
//                        for (int index = 0; index < componentsInChildren.Length; ++index)
//                        {
//                            if (componentsInChildren[index] != null &&
//                                componentsInChildren[index].AttributeExists(desc))
//                                num = Mathf.Max(componentsInChildren[index].GetAttributeFromDesc(desc).ValueFloat);
//                        }
//                    }
//                }
//            }
//
//            return num;
//        }
//
//        public static GameObject SpawnAndAttachFx(
//            Sequence sequence,
//            GameObject fxPrefab,
//            ActorData targetActor,
//            JointPopupProperty fxJoint,
//            bool attachToJoint,
//            bool aimAtCaster,
//            bool reverseDir)
//        {
//            GameObject fx = null;
//            if (targetActor != null)
//            {
//                if (!fxJoint.IsInitialized())
//                    fxJoint.Initialize(targetActor.gameObject);
//                if (fxPrefab != null)
//                {
//                    if (fxJoint.m_jointObject != null && fxJoint.m_jointObject.transform.localScale != Vector3.Zero &&
//                        attachToJoint)
//                    {
//                        fx = sequence.InstantiateFX(fxPrefab);
//                        sequence.AttachToBone(fx, fxJoint.m_jointObject);
//                        fx.transform.localPosition = Vector3.Zero;
//                        fx.transform.localRotation = Quaternion.Identity;
//                        Quaternion quaternion = new Quaternion();
//                        if (aimAtCaster)
//                        {
//                            Vector3 view = sequence.Caster.transform.position -
//                                           fxJoint.m_jointObject.transform.position;
//                            view.y = 0.0f;
//                            view.Normalize();
//                            if (reverseDir)
//                                view *= -1f;
//                            quaternion.SetLookRotation(view);
//                            fx.transform.rotation = quaternion;
//                        }
//                    }
//                    else
//                    {
//                        Vector3 position = fxJoint.m_jointObject.transform.position;
//                        Quaternion rotation = new Quaternion();
//                        if (aimAtCaster)
//                        {
//                            Vector3 view = sequence.Caster.transform.position - position;
//                            view.y = 0.0f;
//                            view.Normalize();
//                            if (reverseDir)
//                                view *= -1f;
//                            rotation.SetLookRotation(view);
//                        }
//                        else
//                            rotation = fxJoint.m_jointObject.transform.rotation;
//
//                        fx = sequence.InstantiateFX(fxPrefab, position, rotation);
//                        Sequence.SetAttribute(fx, "abilityAreaLength", (sequence.TargetPos - position).magnitude);
//                    }
//                }
//            }
//
//            return fx;
//        }
//
//        internal void AttachToBone(GameObject fx, GameObject parent)
//        {
//            if (m_parentedFXs == null)
//                InitializeFXStorage();
//            GameObject gameObject = new GameObject();
//            gameObject.transform.parent = parent.transform;
//            gameObject.transform.localPosition = Vector3.Zero;
//            gameObject.transform.localScale = Vector3.one;
//            gameObject.transform.localRotation = Quaternion.Identity;
//            fx.transform.parent = gameObject.transform;
//            m_parentedFXs.Add(gameObject);
//        }
//
//        internal static ActorModelData.ImpulseInfo CreateImpulseInfoWithObjectPose(
//            GameObject obj)
//        {
//            if (obj != null)
//                return new ActorModelData.ImpulseInfo(obj.transform.position, obj.transform.forward);
//            return null;
//        }
//
//        internal static ActorModelData.ImpulseInfo CreateImpulseInfoWithActorForward(
//            ActorData actor)
//        {
//            if (actor != null)
//                return new ActorModelData.ImpulseInfo(actor.transform.position, Vector3.up + actor.transform.forward);
//            return null;
//        }
//
//        internal static ActorModelData.ImpulseInfo CreateImpulseInfoBetweenActors(
//            ActorData fromActor,
//            ActorData targetActor)
//        {
//            if (fromActor == null || targetActor == null)
//                return null;
//            if (fromActor == targetActor)
//                return CreateImpulseInfoWithActorForward(fromActor);
//            return new ActorModelData.ImpulseInfo(targetActor.transform.position,
//                targetActor.transform.position - fromActor.transform.position);
//        }
//
//        public override string ToString()
//        {
//            return string.Format(
//                "[Sequence: {0}, Object: {1}, id: {2}, initialized: {3}, enabled: {4}, MarkedForRemoval: {5}, Caster: {6}]",
//                GetType().ToString(), gameObject.name, Id, m_initialized, enabled, MarkedForRemoval,
//                !(Caster == null) ? Caster.ToString() : "NULL");
//        }
//
//        public string GetTargetsString()
//        {
//            string empty = string.Empty;
//            if (Targets != null && Targets.Length > 0)
//            {
//                for (int index = 0; index < Targets.Length; ++index)
//                {
//                    ActorData target = Targets[index];
//                    if (target != null)
//                    {
//                        if (empty.Length > 0)
//                            empty += " | ";
//                        empty += target.ActorIndex.ToString();
//                    }
//                }
//            }
//
//            return empty;
//        }
//
//        public void OverridePhaseTimingParams(
//            PhaseTimingParameters timingParams,
//            IExtraSequenceParams iParams)
//        {
//            if (iParams == null || !(iParams is PhaseTimingExtraParams))
//                return;
//            PhaseTimingExtraParams timingExtraParams = iParams as PhaseTimingExtraParams;
//            if (timingExtraParams == null || timingParams == null || !timingParams.m_acceptOverrideFromParams)
//                return;
//            if (timingExtraParams.m_turnDelayStartOverride >= 0)
//                timingParams.m_turnDelayStart = timingExtraParams.m_turnDelayStartOverride;
//            if (timingExtraParams.m_turnDelayEndOverride >= 0)
//                timingParams.m_turnDelayEnd = timingExtraParams.m_turnDelayEndOverride;
//            if (timingExtraParams.m_abilityPhaseStartOverride >= 0)
//            {
//                timingParams.m_usePhaseStartTiming = true;
//                timingParams.m_abilityPhaseStart = (AbilityPriority) timingExtraParams.m_abilityPhaseStartOverride;
//            }
//
//            if (timingExtraParams.m_abilityPhaseEndOverride < 0)
//                return;
//            timingParams.m_usePhaseEndTiming = true;
//            timingParams.m_abilityPhaseEnd = (AbilityPriority) timingExtraParams.m_abilityPhaseEndOverride;
//        }
//
//        public string GetInEditorDescription()
//        {
//            string str1 = m_setupNotes.m_notes;
//            if (string.IsNullOrEmpty(str1))
//                str1 = "<empty>";
//            string str2 = "Setup Note: " + str1 + "\n----------\n";
//            if (m_targetHitAnimation)
//            {
//                str2 += "<[x] Target Hit Animation> Can trigger hit react anim\n\n";
//                if (m_canTriggerHitReactOnAllyHit)
//                    str2 += "Can trigger hit react on ally hit\n\n";
//            }
//
//            return str2 + GetVisibilityDescription() + "\n<color=white>--Sequence Specific--</color>\n" +
//                   GetSequenceSpecificDescription();
//        }
//
//        public virtual string GetVisibilityDescription()
//        {
//            string empty = string.Empty;
//            if (m_visibilityType == VisibilityType.Always)
//                empty +=
//                    "<color=yellow>WARNING: </color>VisibilityType is set to be always visible. Ignore if that is intended.\n";
//            return empty;
//        }

        public virtual string GetSequenceSpecificDescription()
        {
            return "NO SEQUENCE SPECIFIC DESCRIPTION IMPLEMENTED YET T_T";
        }

        public static string GetVisibilityTypeDescription(
            VisibilityType visType,
            out bool usesTargetPos,
            out bool usesSeqPos)
        {
            string str = "UNKNOWN";
            usesTargetPos = false;
            usesSeqPos = false;
            switch (visType)
            {
                case VisibilityType.Always:
                    str = "always visible";
                    break;
                case VisibilityType.Caster:
                    str = "visible if " + c_casterToken + " is visible";
                    break;
                case VisibilityType.CasterOrTarget:
                    str = "visible if either " + c_casterToken + " or any " + c_targetActorToken + " is visible";
                    break;
                case VisibilityType.CasterOrTargetPos:
                    str = "visible if either " + c_casterToken + " visible or " + c_targetPosToken + " square visible";
                    usesTargetPos = true;
                    break;
                case VisibilityType.CasterOrTargetOrTargetPos:
                    str = "visible if either " + c_casterToken + " or any " + c_targetActorToken + " visible, or " +
                          c_seqPosToken + " square is visible";
                    usesTargetPos = true;
                    break;
                case VisibilityType.Target:
                    str = "visible if any " + c_targetActorToken + " is visible";
                    break;
                case VisibilityType.TargetPos:
                    str = "visible if " + c_targetPosToken + " square is visible";
                    usesTargetPos = true;
                    break;
                case VisibilityType.AlwaysOnlyIfCaster:
                    str = "visible only if " + c_casterToken + " is current " + c_clientActorToken +
                          " that player controls";
                    break;
                case VisibilityType.AlwaysOnlyIfTarget:
                    str = "visible only if first " + c_targetActorToken + " is current " + c_clientActorToken +
                          " that player controls";
                    break;
                case VisibilityType.AlwaysIfCastersTeam:
                    str = "visible if " + c_casterToken + " is on same team as current " + c_clientActorToken +
                          " that player controls";
                    break;
                case VisibilityType.SequencePosition:
                    str = "visible if " + c_seqPosToken + " square is visible\n(ex. for most projectiles)";
                    usesSeqPos = true;
                    break;
                case VisibilityType.CastersTeamOrSequencePosition:
                    str = "visible if " + c_casterToken + " is on same team as current " + c_clientActorToken +
                          " that player controls, OR " + c_seqPosToken +
                          " square is visible\n(ex. for projectile that should always be visible for allies but only visible if projectile position is visible for enemies)";
                    usesSeqPos = true;
                    break;
                case VisibilityType.TargetPosAndCaster:
                    str = "visible if " + c_casterToken + " is visible AND " + c_targetPosToken +
                          " square is visible\n(ex. for Flash catalyst while stealthed)";
                    usesTargetPos = true;
                    break;
            }

            return str;
        }

        [Serializable]
        public class PhaseTimingParameters
        {
            public bool m_usePhaseStartTiming;
            public int m_turnDelayStart;
            public AbilityPriority m_abilityPhaseStart;
            public bool m_usePhaseEndTiming;
            public int m_turnDelayEnd;
            public AbilityPriority m_abilityPhaseEnd;

//            [Header("-- Whether this sequence component accept ability params to override phase timing params --")]
            public bool m_acceptOverrideFromParams;

            internal int m_startTurn;
            internal bool m_started;
            internal bool m_finished;

            internal void OnTurnStart(int currentTurn)
            {
                --m_turnDelayStart;
                --m_turnDelayEnd;
                if (!m_usePhaseEndTiming || m_finished ||
                    (m_turnDelayEnd >= 0 || m_abilityPhaseEnd == AbilityPriority.INVALID))
                    return;
                m_finished = true;
            }

            internal void OnAbilityPhaseStart(AbilityPriority abilityPhase)
            {
                if (m_turnDelayStart <= 0 && abilityPhase == m_abilityPhaseStart)
                    m_started = true;
                if (m_turnDelayEnd > 0 || abilityPhase != m_abilityPhaseEnd)
                    return;
                m_finished = true;
            }

            internal bool ShouldSequenceBeActive()
            {
                if (!m_started && m_usePhaseStartTiming)
                    return false;
                if (m_finished)
                    return !m_usePhaseEndTiming;
                return true;
            }

            internal bool ShouldSpawnSequence(AbilityPriority abilityPhase)
            {
                bool flag = false;
                if (m_turnDelayStart == 0 && abilityPhase == m_abilityPhaseStart && m_usePhaseStartTiming)
                    flag = true;
                return flag;
            }

            internal bool ShouldStopSequence(AbilityPriority abilityPhase)
            {
                bool flag = false;
                if (m_turnDelayEnd == 0 && abilityPhase == m_abilityPhaseEnd && m_usePhaseEndTiming)
                    flag = true;
                return flag;
            }
        }

        [Serializable]
        public class SequenceNotes
        {
//            [TextArea(1, 5)]
            public string m_notes;
        }

        public enum ReferenceModelType
        {
            Actor,
            TempSatellite,
            PersistentSatellite1
        }

        public enum VisibilityType
        {
            Always,
            Caster,
            CasterOrTarget,
            CasterOrTargetPos,
            CasterOrTargetOrTargetPos,
            Target,
            TargetPos,
            AlwaysOnlyIfCaster,
            AlwaysOnlyIfTarget,
            AlwaysIfCastersTeam,
            SequencePosition,
            CastersTeamOrSequencePosition,
            TargetPosAndCaster,
            CasterIfNotEvading,
            TargetIfNotEvading
        }

        public enum HitVisibilityType
        {
            Always,
            Target
        }

        public enum SequenceHideType
        {
            Default_DisableObject,
            MoveOffCamera_KeepEnabled,
            KillThenDisable
        }

        public enum PhaseBasedVisibilityType
        {
            Any,
            InDecisionOnly,
            InResolutionOnly
        }

        public enum HitVFXSpawnTeam
        {
            AllTargets,
            AllyAndCaster,
            EnemyOnly,
            AllExcludeCaster
        }

        public abstract class IExtraSequenceParams
        {
            public abstract void XSP_SerializeToStream(IBitStream stream);

            public abstract void XSP_DeserializeFromStream(IBitStream stream);

            public IExtraSequenceParams[] ToArray()
            {
                return new IExtraSequenceParams[1] {this};
            }
        }

        public class PhaseTimingExtraParams : IExtraSequenceParams
        {
            public sbyte m_turnDelayStartOverride = -1;
            public sbyte m_turnDelayEndOverride = -1;
            public sbyte m_abilityPhaseStartOverride = -1;
            public sbyte m_abilityPhaseEndOverride = -1;

            public override void XSP_SerializeToStream(IBitStream stream)
            {
                stream.Serialize(ref m_turnDelayStartOverride);
                stream.Serialize(ref m_turnDelayEndOverride);
                stream.Serialize(ref m_abilityPhaseStartOverride);
                stream.Serialize(ref m_abilityPhaseEndOverride);
            }

            public override void XSP_DeserializeFromStream(IBitStream stream)
            {
                stream.Serialize(ref m_turnDelayStartOverride);
                stream.Serialize(ref m_turnDelayEndOverride);
                stream.Serialize(ref m_abilityPhaseStartOverride);
                stream.Serialize(ref m_abilityPhaseEndOverride);
            }
        }

        public class FxAttributeParam : IExtraSequenceParams
        {
            public ParamNameCode m_paramNameCode;
            public ParamTarget m_paramTarget;
            public float m_paramValue;

            public override void XSP_SerializeToStream(IBitStream stream)
            {
                sbyte paramNameCode = (sbyte) m_paramNameCode;
                sbyte paramTarget = (sbyte) m_paramTarget;
                stream.Serialize(ref paramNameCode);
                stream.Serialize(ref paramTarget);
                stream.Serialize(ref m_paramValue);
            }

            public override void XSP_DeserializeFromStream(IBitStream stream)
            {
                sbyte num1 = 0;
                sbyte num2 = 0;
                stream.Serialize(ref num1);
                stream.Serialize(ref num2);
                stream.Serialize(ref m_paramValue);
                m_paramNameCode = (ParamNameCode) num1;
                m_paramTarget = (ParamTarget) num2;
            }

            public string GetAttributeName()
            {
                if (m_paramNameCode == ParamNameCode.ScaleControl)
                    return "scaleControl";
                if (m_paramNameCode == ParamNameCode.LengthInSquares)
                    return "lengthInSquares";
                if (m_paramNameCode == ParamNameCode.WidthInSquares)
                    return "widthInSquares";
                if (m_paramNameCode == ParamNameCode.AbilityAreaLength)
                    return "abilityAreaLength";
                return string.Empty;
            }

            public void SetValues(
                ParamTarget paramTarget,
                ParamNameCode nameCode,
                float value)
            {
                m_paramTarget = paramTarget;
                m_paramNameCode = nameCode;
                m_paramValue = value;
            }

            public enum ParamNameCode
            {
                None,
                ScaleControl,
                LengthInSquares,
                WidthInSquares,
                AbilityAreaLength
            }

            public enum ParamTarget
            {
                None,
                MainVfx,
                ImpactVfx
            }
        }

        public class ActorIndexExtraParam : IExtraSequenceParams
        {
            public short m_actorIndex = -1;

            public override void XSP_SerializeToStream(IBitStream stream)
            {
                stream.Serialize(ref m_actorIndex);
            }

            public override void XSP_DeserializeFromStream(IBitStream stream)
            {
                stream.Serialize(ref m_actorIndex);
            }
        }

        public class GenericIntParam : IExtraSequenceParams
        {
            public FieldIdentifier m_fieldIdentifier;
            public short m_value;

            public override void XSP_SerializeToStream(IBitStream stream)
            {
                sbyte fieldIdentifier = (sbyte) m_fieldIdentifier;
                stream.Serialize(ref fieldIdentifier);
                stream.Serialize(ref m_value);
            }

            public override void XSP_DeserializeFromStream(IBitStream stream)
            {
                sbyte num = 0;
                stream.Serialize(ref num);
                stream.Serialize(ref m_value);
                m_fieldIdentifier = (FieldIdentifier) num;
            }

            public enum FieldIdentifier
            {
                None,
                Index
            }
        }

//        public class GenericActorListParam : IExtraSequenceParams
//        {
//            public List<ActorData> m_actors;
//
//            public override void XSP_SerializeToStream(IBitStream stream)
//            {
//                List<ActorData> actors = m_actors;
//                sbyte num1 = actors == null ? (sbyte) 0 : (sbyte) actors.Count;
//                stream.Serialize(ref num1);
//                for (int index = 0; index < (int) num1; ++index)
//                {
//                    ActorData actorData = actors[index];
//                    sbyte num2 = !(actorData != null)
//                        ? (sbyte) ActorData.s_invalidActorIndex
//                        : (sbyte) actorData.ActorIndex;
//                    stream.Serialize(ref num2);
//                }
//            }
//
//            public override void XSP_DeserializeFromStream(IBitStream stream)
//            {
//                sbyte num = 0;
//                stream.Serialize(ref num);
//                m_actors = new List<ActorData>(num);
//                for (int index = 0; index < (int) num; ++index)
//                {
//                    sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
//                    stream.Serialize(ref invalidActorIndex);
//                    m_actors.Add(GameFlowData.Get().FindActorByActorIndex((int) invalidActorIndex));
//                }
//            }
//        }
    }
}
