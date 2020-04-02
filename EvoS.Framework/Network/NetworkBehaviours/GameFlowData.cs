using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("GameFlowData")]
    public class GameFlowData : NetworkBehaviour
    {
        public static float s_loadingScreenTime = 4f;
        private static int kRpcRpcUpdateTimeRemaining = 939569152;

        public SerializedVector<SerializedComponent> SerializedOownedActorDatas;
        public List<ActorData> m_ownedActorDatas = new List<ActorData>();
        public bool m_oneClassOnTeam = true;

        public SerializedArray<SerializedComponent> m_availableCharacterResourceLinkPrefabs;

//        [SyncVar(hook = "HookSetStartTime")]
        public float m_startTime = 5f;

//        [SyncVar(hook = "HookSetDeploymentTime")]
        public float m_deploymentTime = 7f;

//        [SyncVar(hook = "HookSetTurnTime")]
        public float m_turnTime = 21f;

//        [SyncVar(hook = "HookSetMaxTurnTime")]
        public float m_maxTurnTime = 21f;
        public float m_resolveTimeoutLimit = 112f;
        private List<ActorData> m_teamAPlayerAndBots = new List<ActorData>();
        private List<ActorData> m_teamBPlayerAndBots = new List<ActorData>();
        private List<ActorData> m_teamObjectsPlayerAndBots = new List<ActorData>();
        private List<ActorData> m_teamA = new List<ActorData>();
        private List<ActorData> m_teamB = new List<ActorData>();
        private List<ActorData> m_teamObjects = new List<ActorData>();
        private List<ActorData> m_actors = new List<ActorData>();
        private List<GameObject> m_players = new List<GameObject>();

        private float m_timeRemainingInDecision = 20f;

//        [SyncVar]
        private bool m_pause;

//        [SyncVar]
        private bool m_pausedForDebugging;

//        [SyncVar]
        private bool m_pausedByPlayerRequest;

        private bool m_pausedForDialog;

//        [SyncVar]
        private bool m_pausedForSinglePlayer;

//        [SyncVar]
        private ResolutionPauseState m_resolutionPauseState;
        public PlayerData m_localPlayerData;
        private GameObject m_actorRoot;
        private GameObject m_thinCoverRoot;
        private GameObject m_brushRegionBorderRoot;
        private Team m_selectedTeam;
        private ActorData m_activeOwnedActorData;
        private float m_matchStartTime;

        private float m_deploymentStartTime;

//        [SyncVar]
        private float m_timeRemainingInDecisionOverflow;

//        [SyncVar]
        private bool m_willEnterTimebankMode;
        private const float c_timeRemainingUpdateInterval = 1f;
        private const float c_latencyCorrectionTime = 1f;
        private float m_timeInState;
        private float m_timeInStateUnscaled;

        private float m_timeInDecision;

//        [SyncVar(hook = "HookSetCurrentTurn")]
        private int m_currentTurn;

//        [SyncVar(hook = "HookSetGameState")]
        private GameState m_gameState;

        private float turnStartTime = 0;

        private float TimeInDecision
        {
            get
            {
                return Math.Max(0, Time.realtimeSinceStartup - turnStartTime);
            }
        }

        [JsonIgnore]
        public bool Networkm_pause
        {
            get => m_pause;
            [param: In] set => SetSyncVar(value, ref m_pause, 1U);
        }

        [JsonIgnore]
        public bool Networkm_pausedForDebugging
        {
            get => m_pausedForDebugging;
            [param: In] set => SetSyncVar(value, ref m_pausedForDebugging, 2U);
        }

        [JsonIgnore]
        public bool Networkm_pausedByPlayerRequest
        {
            get => m_pausedByPlayerRequest;
            [param: In] set => SetSyncVar(value, ref m_pausedByPlayerRequest, 4U);
        }

        [JsonIgnore]
        public bool Networkm_pausedForSinglePlayer
        {
            get => m_pausedForSinglePlayer;
            [param: In] set => SetSyncVar(value, ref m_pausedForSinglePlayer, 8U);
        }

        [JsonIgnore]
        public ResolutionPauseState Networkm_resolutionPauseState
        {
            get => m_resolutionPauseState;
            [param: In] set => SetSyncVar(value, ref m_resolutionPauseState, 16U);
        }

        [JsonIgnore]
        public float Networkm_startTime
        {
            get => m_startTime;
            [param: In]
            set
            {
                double num = value;
                ref float local = ref m_startTime;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetStartTime(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((float) num, ref local, 32U);
            }
        }

        [JsonIgnore]
        public float Networkm_deploymentTime
        {
            get => m_deploymentTime;
            [param: In]
            set
            {
                double num = value;
                ref float local = ref m_deploymentTime;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetDeploymentTime(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((float) num, ref local, 64U);
            }
        }

        [JsonIgnore]
        public float Networkm_turnTime
        {
            get => m_turnTime;
            [param: In]
            set
            {
                double num = value;
                ref float local = ref m_turnTime;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetTurnTime(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((float) num, ref local, 128U);
            }
        }

        [JsonIgnore]
        public float Networkm_maxTurnTime
        {
            get => m_maxTurnTime;
            [param: In]
            set
            {
                double num = value;
                ref float local = ref m_maxTurnTime;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetMaxTurnTime(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((float) num, ref local, 256U);
            }
        }

        [JsonIgnore]
        public float Networkm_timeRemainingInDecisionOverflow
        {
            get => m_timeRemainingInDecisionOverflow;
            [param: In] set => SetSyncVar(value, ref m_timeRemainingInDecisionOverflow, 512U);
        }

        [JsonIgnore]
        public bool Networkm_willEnterTimebankMode
        {
            get => m_willEnterTimebankMode;
            [param: In] set => SetSyncVar(value, ref m_willEnterTimebankMode, 1024U);
        }

        [JsonIgnore]
        public int Networkm_currentTurn
        {
            get => m_currentTurn;
            [param: In]
            set
            {
                int num = value;
                ref int local = ref m_currentTurn;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetCurrentTurn(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar(num, ref local, 2048U);
            }
        }

        [JsonIgnore]
        public GameState Networkm_gameState
        {
            get => m_gameState;
            [param: In]
            set
            {
                int num = (int) value;
                ref GameState local = ref m_gameState;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetGameState(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((GameState) num, ref local, 4096U);
            }
        }

        public int CurrentTurn => m_currentTurn;

        public GameState gameState
        {
            get => m_gameState;
            set
            {
                if (m_gameState == value)
                    return;
                SetGameState(value);
            }
        }


        public ActorData firstOwnedFriendlyActorData
        {
            get
            {
                ActorData actorData = null;
                if (m_ownedActorDatas.Count <= 0 || activeOwnedActorData == null) return actorData;

                foreach (var ownedActorData in m_ownedActorDatas)
                {
                    if (ownedActorData.Team != activeOwnedActorData.Team) continue;
                    actorData = ownedActorData;
                    break;
                }

                return actorData;
            }
        }

        public ActorData firstOwnedEnemyActorData
        {
            get
            {
                ActorData actorData = null;
                if (activeOwnedActorData == null) return actorData;

                foreach (var ownedActorData in m_ownedActorDatas)
                {
                    if (ownedActorData.Team == activeOwnedActorData.Team) continue;
                    actorData = ownedActorData;
                    break;
                }

                return actorData;
            }
        }

        public ActorData POVActorData => activeOwnedActorData;

        public ActorData activeOwnedActorData
        {
            get => m_activeOwnedActorData;
            set
            {
                var flag1 = m_activeOwnedActorData != value;
                var flag2 = false;
                if (m_activeOwnedActorData != null)
                {
                    // TODO
//                    m_activeOwnedActorData.OnDeselect();
//                    flag2 = value != null && value.\u0012() == m_activeOwnedActorData.\u000E();
                }

                m_activeOwnedActorData = value;
                // TODO
//                if (m_activeOwnedActorData != null)
//                    m_activeOwnedActorData.OnSelect();
                if (flag1)
                    s_onActiveOwnedActorChange?.Invoke(value);
                if (!flag2)
                    return;
                // TODO
//                GameEventManager.Get().FireEvent(GameEventManager.EventType.ActiveControlChangedToEnemyTeam,
//                    (GameEventManager.GameEventArgs) null);
            }
        }

        public event Action<ActorData> s_onAddActor = delegate { };
        public event Action<ActorData> s_onRemoveActor = delegate { };
        public event Action<ActorData> s_onActiveOwnedActorChange = delegate { };
        public event Action<GameState> s_onGameStateChanged = delegate { };

        static GameFlowData()
        {
            RegisterRpcDelegate(typeof(GameFlowData), kRpcRpcUpdateTimeRemaining, InvokeRpcRpcUpdateTimeRemaining);
        }

        public GameFlowData()
        {
        }

        public GameFlowData(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public bool IsInDecisionState()
        {
            return m_gameState == GameState.BothTeams_Decision;
        }

        private void SetGameState(GameState value)
        {
            Networkm_gameState = value;
            m_timeInState = 0.0f;
            m_timeInStateUnscaled = 0.0f;
            Log.Print(LogType.Game, $"Game state: {value}");
            switch (m_gameState)
            {
                case GameState.StartingGame:
                    break;
                case GameState.Deployment:
                    m_deploymentStartTime = Time.realtimeSinceStartup;
                    break;
                case GameState.BothTeams_Decision:
                    if (EvoSGameConfig.NetworkIsServer)
                        IncrementTurn();
                    if (CurrentTurn == 1)
                        m_matchStartTime = Time.realtimeSinceStartup;
                    ResetOwnedActorDataToFirst();
                    m_timeInDecision = 0.0f;
                    break;
            }

            if (s_onGameStateChanged == null)
                return;
            s_onGameStateChanged(m_gameState);
        }

        public void RemoveReferencesToDestroyedActor(ActorData actor)
        {
            if (actor != null)
            {
                if (m_teamAPlayerAndBots.Contains(actor))
                    m_teamAPlayerAndBots.Remove(actor);
                if (m_teamBPlayerAndBots.Contains(actor))
                    m_teamBPlayerAndBots.Remove(actor);
                if (m_teamObjectsPlayerAndBots.Contains(actor))
                    m_teamObjectsPlayerAndBots.Remove(actor);
                if (m_teamA.Contains(actor))
                    m_teamA.Remove(actor);
                if (m_teamB.Contains(actor))
                    m_teamB.Remove(actor);
                if (m_teamObjects.Contains(actor))
                    m_teamObjects.Remove(actor);
                if (m_players.Contains(actor.gameObject))
                    m_players.Remove(actor.gameObject);
                if (m_actors.Contains(actor))
                    m_actors.Remove(actor);
                SetLocalPlayerData();
                if (GameFlowData.s_onRemoveActor == null)
                    return;
                GameFlowData.s_onRemoveActor(actor);
            }
            else
                Log.Print(LogType.Error, "Trying to destroy a null actor.");
        }

        public void SetLocalPlayerData()
        {
//            m_localPlayerData = null;
//            if (GameFlow == null)
//                return;
//            foreach (var player in m_players)
//            {
//                var component = player?.GetComponent<PlayerData>();
//                if (component == null) continue;
//                if (!GameFlow.playerDetails.TryGetValue(component.GetPlayer(), out PlayerDetails playerDetails) ||
//                    !playerDetails.IsLocal()) continue;
//                m_localPlayerData = component;
//                break;
//            }
        }

        private void IncrementTurn()
        {
            m_timeInDecision = 0.0f;
            turnStartTime = Time.realtimeSinceStartup;
            Networkm_currentTurn = m_currentTurn + 1;
            NotifyOnTurnTick();
            Log.Print(LogType.Game, $"Turn: {CurrentTurn}");
            // TODO
//            if (!(Board.\u000E() != null))
//                return;
//            Board.\u000E().MarkForUpdateValidSquares(true);
        }

        private void NotifyOnTurnTick()
        {
            // TODO
            
            UpdateTimeRemaining();
//            if (TeamSensitiveDataMatchmaker.Get() != null)
//                TeamSensitiveDataMatchmaker.Get().SetTeamSensitiveDataForUnhandledActors();
            GameEventManager
                .FireEvent(GameEventManager.EventType.TurnTick, (GameEventManager.GameEventArgs)null);
//            this.ShowIntervanStatusNotifications();
//            if (ClientResolutionManager.Get() != null)
//                ClientResolutionManager.Get().OnTurnStart();
//            if (ClientClashManager.Get() != null)
//                ClientClashManager.Get().OnTurnStart();
//            if (SequenceManager.Get() != null)
//                SequenceManager.Get().OnTurnStart(this.m_currentTurn);
//            if (InterfaceManager.Get() != null)
//                InterfaceManager.Get().OnTurnTick();
//            foreach (PowerUp.IPowerUpListener powerUpListener in PowerUpManager.Get().powerUpListeners)
//                powerUpListener.OnTurnTick();
//            if (TriggerCoordinator.Get() != null)
//                TriggerCoordinator.Get().OnTurnTick();
//            if (SinglePlayerManager.Get() != null)
//                SinglePlayerManager.Get().OnTurnTick();
//            if (TheatricsManager.Get() != null)
//                TheatricsManager.Get().OnTurnTick();
//            if (SequenceManager.Get() != null)
//                SequenceManager.Get().ClientOnTurnResolveEnd();
//            if (CameraManager.Get() != null)
//                CameraManager.Get().OnTurnTick();
//            if (FirstTurnMovement.Get() != null)
//                FirstTurnMovement.Get().OnTurnTick();
//            if (CollectTheCoins.Get() != null)
//                CollectTheCoins.Get().OnTurnTick();
            this.m_timeRemainingInDecision = GameFlowData.m_turnTime;
            foreach (ActorData actor in this.GetActors())
                actor.OnTurnTick();
//            if (ObjectivePoints.Get() != null)
//                ObjectivePoints.Get().OnTurnTick();
//            if (ClientAbilityResults.\u001D)
//                Log.Warning("Turn Start: <color=magenta>" + (object) GameFlowData.Get().CurrentTurn + "</color>");
//            if (!(ControlpadGameplay.Get() != null))
//                return;
//            ControlpadGameplay.Get().OnTurnTick();
        }

        public void ResetOwnedActorDataToFirst()
        {
            if (m_ownedActorDatas.Count <= 0)
                return;
            // TODO
//            if (SpawnPointManager.Get() == null || !SpawnPointManager.Get().m_playersSelectRespawn)
//            {
//                foreach (ActorData ownedActorData in m_ownedActorDatas)
//                {
//                    if (ownedActorData != null && !ownedActorData.\u000E())
//                    {
//                        activeOwnedActorData = ownedActorData;
//                        return;
//                    }
//                }
//            }

            activeOwnedActorData = m_ownedActorDatas[0];
        }

        private void HookSetGameState(GameState state)
        {
            if (m_gameState == state || EvoSGameConfig.NetworkIsServer)
                return;
            gameState = state;
        }

        private void HookSetStartTime(float startTime)
        {
            if (m_startTime == (double) startTime)
                return;
            Networkm_startTime = startTime;
        }

        private void HookSetDeploymentTime(float deploymentTime)
        {
            if (m_deploymentTime == (double) deploymentTime)
                return;
            Networkm_deploymentTime = deploymentTime;
        }

        private void HookSetTurnTime(float turnTime)
        {
            if (m_turnTime == (double) turnTime)
                return;
            Networkm_turnTime = turnTime;
        }

        private void HookSetMaxTurnTime(float maxTurnTime)
        {
            if (m_maxTurnTime == (double) maxTurnTime)
                return;
            Networkm_maxTurnTime = maxTurnTime;
        }

        public GameObject GetThinCoverRoot()
        {
            if (m_thinCoverRoot == null)
            {
                m_thinCoverRoot = new GameObject("ThinCoverRoot");
//                UnityEngine.Object.DontDestroyOnLoad(m_thinCoverRoot);
                GameManager.RegisterObject(m_thinCoverRoot);
            }

            return m_thinCoverRoot;
        }

        private void HookSetCurrentTurn(int turn)
        {
            if (EvoSGameConfig.NetworkIsServer)
                return;
            while (m_currentTurn < turn)
                IncrementTurn();
            Networkm_currentTurn = turn;
        }

        public void AddPlayer(GameObject player)
        {
            m_players.Add(player);
            SetLocalPlayerData();
        }

        public void RemoveExistingPlayer(GameObject player)
        {
            if (!m_players.Contains(player))
                return;
            m_players.Remove(player);
        }

        public List<ActorData> GetActors()
        {
            return m_actors;
        }

        public List<ActorData> GetActorsVisibleToActor(
            ActorData observer,
            bool targetableOnly = true)
        {
            var actorDataList = new List<ActorData>();
            if (observer == null) return actorDataList;

            foreach (var actor in m_actors)
            {
                if (!actor.IsDead() && actor.IsActorVisibleToActor(observer) &&
                    (!targetableOnly || !actor.IgnoreForAbilityHits))
                    actorDataList.Add(actor);
            }

            return actorDataList;
        }

        public List<ActorData> GetAllActorsForPlayer(int playerIndex)
        {
            var actorDataList = new List<ActorData>();
            foreach (var actor in m_actors)
            {
                if (actor.PlayerIndex == playerIndex)
                    actorDataList.Add(actor);
            }

            return actorDataList;
        }

        public void AddActor(ActorData actor)
        {
            Log.Print(LogType.Game, $"Registering actor {actor}");
            m_actors.Add(actor);
            if (!EvoSGameConfig.NetworkIsServer || s_onAddActor == null)
                return;
            s_onAddActor(actor);
        }

        public ActorData FindActorByActorIndex(int actorIndex)
        {
            ActorData actorData = null;
            foreach (var actor in m_actors)
            {
                if (actor.ActorIndex == actorIndex)
                {
                    actorData = actor;
                    break;
                }
            }

            if (actorData == null && actorIndex > 0 &&
                (CurrentTurn > 0 && GameManager != null) &&
                (GameManager.GameConfig != null && GameManager.GameConfig.GameType != GameType.Tutorial))
                Log.Print(LogType.Warning, $"Failed to find actor index {actorIndex}");
            return actorData;
        }

        public ActorData FindActorByPlayerIndex(int playerIndex)
        {
            foreach (var player in m_players)
            {
                var component = player.GetComponent<ActorData>();
                if (component != null && component.PlayerIndex == playerIndex)
                    return component;
            }

            Log.Print(LogType.Warning, $"Failed to find player index {playerIndex}");
            return null;
        }

        public ActorData FindActorByPlayer(Player player)
        {
            foreach (var actor in m_actors)
            {
                var playerData = actor.PlayerData;
                if (playerData != null && playerData.GetPlayer() == player)
                    return actor;
            }

            return null;
        }

        public List<ActorData> GetAllTeamMembers(Team team)
        {
            return GetAllActorsOnTeam(team);
        }

        public void RemoveFromTeam(ActorData actorData)
        {
            m_teamA.Remove(actorData);
            m_teamB.Remove(actorData);
            m_teamObjects.Remove(actorData);
            m_teamAPlayerAndBots.Remove(actorData);
            m_teamBPlayerAndBots.Remove(actorData);
            m_teamObjectsPlayerAndBots.Remove(actorData);
        }

        public void AddToTeam(ActorData actorData)
        {
            if (GameplayUtils.IsPlayerControlled(actorData))
            {
                if (actorData.Team == Team.TeamA && !m_teamAPlayerAndBots.Contains(actorData))
                {
                    m_teamBPlayerAndBots.Remove(actorData);
                    m_teamObjectsPlayerAndBots.Remove(actorData);
                    m_teamAPlayerAndBots.Add(actorData);
                }
                else if (actorData.Team == Team.TeamB && !m_teamBPlayerAndBots.Contains(actorData))
                {
                    m_teamAPlayerAndBots.Remove(actorData);
                    m_teamObjectsPlayerAndBots.Remove(actorData);
                    m_teamBPlayerAndBots.Add(actorData);
                }
                else if (actorData.Team == Team.Objects && !m_teamObjectsPlayerAndBots.Contains(actorData))
                {
                    m_teamAPlayerAndBots.Remove(actorData);
                    m_teamBPlayerAndBots.Remove(actorData);
                    m_teamObjectsPlayerAndBots.Add(actorData);
                }
            }

            if (actorData.Team == Team.TeamA && !m_teamA.Contains(actorData))
            {
                m_teamB.Remove(actorData);
                m_teamObjects.Remove(actorData);
                m_teamA.Add(actorData);
            }
            else if (actorData.Team == Team.TeamB && !m_teamB.Contains(actorData))
            {
                m_teamA.Remove(actorData);
                m_teamObjects.Remove(actorData);
                m_teamB.Add(actorData);
            }
            else
            {
                if (actorData.Team != Team.Objects || m_teamObjects.Contains(actorData))
                    return;
                m_teamA.Remove(actorData);
                m_teamB.Remove(actorData);
                m_teamObjects.Add(actorData);
            }
        }

        private List<ActorData> GetAllActorsOnTeam(Team team)
        {
            switch (team)
            {
                case Team.TeamA:
                    return m_teamA;
                case Team.TeamB:
                    return m_teamB;
                case Team.Objects:
                    return m_teamObjects;
                default:
                    return new List<ActorData>();
            }
        }

//        [ClientRpc]
        private void RpcUpdateTimeRemaining(float timeRemaining)
        {
            if (EvoSGameConfig.NetworkIsServer)
                return;
            m_timeRemainingInDecision = timeRemaining - 1f;
        }

        protected static void InvokeRpcRpcUpdateTimeRemaining(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcUpdateTimeRemaining called on server.");
            else
                ((GameFlowData) obj).RpcUpdateTimeRemaining(reader.ReadSingle());
        }

        public void UpdateTimeRemaining()
        {
            m_timeRemainingInDecision = Math.Max(0, m_maxTurnTime - TimeInDecision);
            if (IsInDecisionState())
            {
                //Log.Print(LogType.Warning, $"UpdateTimeRemaining {m_timeRemainingInDecision}");
                CallRpcUpdateTimeRemaining(m_timeRemainingInDecision);
                if (!this.m_pause && m_timeRemainingInDecision <= 0 && CurrentTurn > 0)
                {
                    SetGameState(GameState.BothTeams_Resolve);  // TODO timebanks
                    GameManager.ResolveMovement();
                }
            }
        }

        public void CallRpcUpdateTimeRemaining(float timeRemaining)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcUpdateTimeRemaining called on client.");
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcUpdateTimeRemaining);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(timeRemaining);
                SendRPCInternal(writer, 0, "RpcUpdateTimeRemaining");
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                writer.Write(m_pause);
                writer.Write(m_pausedForDebugging);
                writer.Write(m_pausedByPlayerRequest);
                writer.Write(m_pausedForSinglePlayer);
                writer.Write((int) m_resolutionPauseState);
                writer.Write(m_startTime);
                writer.Write(m_deploymentTime);
                writer.Write(m_turnTime);
                writer.Write(m_maxTurnTime);
                writer.Write(m_timeRemainingInDecisionOverflow);
                writer.Write(m_willEnterTimebankMode);
                writer.WritePackedUInt32((uint) m_currentTurn);
                writer.Write((int) m_gameState);
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

                writer.Write(m_pause);
            }

            if (((int) syncVarDirtyBits & 2) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_pausedForDebugging);
            }

            if (((int) syncVarDirtyBits & 4) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_pausedByPlayerRequest);
            }

            if (((int) syncVarDirtyBits & 8) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_pausedForSinglePlayer);
            }

            if (((int) syncVarDirtyBits & 16) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write((int) m_resolutionPauseState);
            }

            if (((int) syncVarDirtyBits & 32) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_startTime);
            }

            if (((int) syncVarDirtyBits & 64) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_deploymentTime);
            }

            if (((int) syncVarDirtyBits & 128) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_turnTime);
            }

            if (((int) syncVarDirtyBits & 256) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_maxTurnTime);
            }

            if (((int) syncVarDirtyBits & 512) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_timeRemainingInDecisionOverflow);
            }

            if (((int) syncVarDirtyBits & 1024) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_willEnterTimebankMode);
            }

            if (((int) syncVarDirtyBits & 2048) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.WritePackedUInt32((uint) m_currentTurn);
            }

            if (((int) syncVarDirtyBits & 4096) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write((int) m_gameState);
            }

            if (!flag)
                writer.WritePackedUInt32(syncVarDirtyBits);
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                m_pause = reader.ReadBoolean();
                m_pausedForDebugging = reader.ReadBoolean();
                m_pausedByPlayerRequest = reader.ReadBoolean();
                m_pausedForSinglePlayer = reader.ReadBoolean();
                m_resolutionPauseState = (ResolutionPauseState) reader.ReadInt32();
                m_startTime = reader.ReadSingle();
                m_deploymentTime = reader.ReadSingle();
                m_turnTime = reader.ReadSingle();
                m_maxTurnTime = reader.ReadSingle();
                m_timeRemainingInDecisionOverflow = reader.ReadSingle();
                m_willEnterTimebankMode = reader.ReadBoolean();
                m_currentTurn = (int) reader.ReadPackedUInt32();
                m_gameState = (GameState) reader.ReadInt32();
            }
            else
            {
                int num = (int) reader.ReadPackedUInt32();
                if ((num & 1) != 0)
                    m_pause = reader.ReadBoolean();
                if ((num & 2) != 0)
                    m_pausedForDebugging = reader.ReadBoolean();
                if ((num & 4) != 0)
                    m_pausedByPlayerRequest = reader.ReadBoolean();
                if ((num & 8) != 0)
                    m_pausedForSinglePlayer = reader.ReadBoolean();
                if ((num & 16) != 0)
                    m_resolutionPauseState = (ResolutionPauseState) reader.ReadInt32();
                if ((num & 32) != 0)
                    HookSetStartTime(reader.ReadSingle());
                if ((num & 64) != 0)
                    HookSetDeploymentTime(reader.ReadSingle());
                if ((num & 128) != 0)
                    HookSetTurnTime(reader.ReadSingle());
                if ((num & 256) != 0)
                    HookSetMaxTurnTime(reader.ReadSingle());
                if ((num & 512) != 0)
                    m_timeRemainingInDecisionOverflow = reader.ReadSingle();
                if ((num & 1024) != 0)
                    m_willEnterTimebankMode = reader.ReadBoolean();
                if ((num & 2048) != 0)
                    HookSetCurrentTurn((int) reader.ReadPackedUInt32());
                if ((num & 4096) == 0)
                    return;
                HookSetGameState((GameState) reader.ReadInt32());
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            SerializedOownedActorDatas = new SerializedVector<SerializedComponent>(assetFile, stream);
            m_oneClassOnTeam = stream.ReadBoolean();
            stream.AlignTo();
            m_availableCharacterResourceLinkPrefabs = new SerializedArray<SerializedComponent>(assetFile, stream);
            m_startTime = stream.ReadSingle();
            m_deploymentTime = stream.ReadSingle();
            m_turnTime = stream.ReadSingle();
            m_maxTurnTime = stream.ReadSingle();
            m_resolveTimeoutLimit = stream.ReadSingle();
        }

        public override string ToString()
        {
            return $"{nameof(GameFlowData)}>(" +
                   $"{nameof(m_ownedActorDatas)}: {m_ownedActorDatas}, " +
                   $"{nameof(m_oneClassOnTeam)}: {m_oneClassOnTeam}, " +
                   $"{nameof(m_availableCharacterResourceLinkPrefabs)}: {m_availableCharacterResourceLinkPrefabs}, " +
                   $"{nameof(m_startTime)}: {m_startTime}, " +
                   $"{nameof(m_deploymentTime)}: {m_deploymentTime}, " +
                   $"{nameof(m_turnTime)}: {m_turnTime}, " +
                   $"{nameof(m_maxTurnTime)}: {m_maxTurnTime}, " +
                   $"{nameof(m_resolveTimeoutLimit)}: {m_resolveTimeoutLimit}, " +
                   ")";
        }
    }
}
