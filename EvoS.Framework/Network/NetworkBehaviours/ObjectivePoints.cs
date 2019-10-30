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
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("ObjectivePoints")]
    public class ObjectivePoints : NetworkBehaviour
    {
        private static int kListm_points = 2045097107;
        public bool m_skipEndOfGameCheck;
        public int m_startingPointsTeamA;
        public int m_startingPointsTeamB;
        public int m_timeLimitTurns;
        public bool m_disablePowerupsAfterTimeLimit = true;
        public string m_victoryCondition;
        public string m_victoryConditionOneTurnLeft;
        public VictoryCondition m_teamAVictoryCondition;
        public VictoryCondition m_teamBVictoryCondition;
        public bool m_allowTies = true;
        public SerializedVector<PointsForCharacter> m_passivePointsForTeamWithCharacter;
        public SerializedComponent m_gameModePanelPrefab;
        public MatchState m_matchState;

        private SyncListInt m_points = new SyncListInt();
        private int[] m_displayedPoints = new int[2];
        private float m_sendGameSummaryToLobbyServer = -1f;
        private float m_gameResultFraction = 0.5f;
        private List<int> m_clientNumDeathInTurn = new List<int>();
        private bool m_inSuddenDeath;

        private HashSet<ActorData> m_respawningPlayers;

//        [SyncVar]
        private GameResult m_gameResult;

//        [SyncVar]
        private float m_minutesInMatchOnGameEnd;

        private List<MatchObjective> m_objectives;

//        [SyncVar(hook = "HookSetMatchState")]
        private float m_gameShutdownTime;

        static ObjectivePoints()
        {
            RegisterSyncListDelegate(typeof(ObjectivePoints), kListm_points, InvokeSyncListm_points);
        }

        public ObjectivePoints()
        {
        }

        public ObjectivePoints(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void Awake()
        {
            m_skipEndOfGameCheck = false;
            m_points.InitializeBehaviour(this, kListm_points);
        }

        public override void OnStartServer() // Was Start()
        {
            if (EvoSGameConfig.NetworkIsServer)
            {
                m_points.Add(m_startingPointsTeamA);
                m_points.Add(m_startingPointsTeamB);
            }

            for (int index = 0; index <= 1; ++index)
                m_clientNumDeathInTurn.Add(0);
//            m_displayedPoints[0] = m_points[0];
//            m_displayedPoints[1] = m_points[1];
            m_respawningPlayers = new HashSet<ActorData>();
            m_inSuddenDeath = false;
            if (EvoSGameConfig.NetworkIsServer)
                Networkm_matchState = MatchState.InMatch;
            // TODO
//            m_objectives =
//                new List<MatchObjective>(
//                    (IEnumerable<MatchObjective>) gameObject.GetComponentsInChildren<MatchObjective>());
        }

        public GameResult Networkm_gameResult
        {
            get => m_gameResult;
            [param: In] set => SetSyncVar(value, ref m_gameResult, 2U);
        }

        public float Networkm_minutesInMatchOnGameEnd
        {
            get => m_minutesInMatchOnGameEnd;
            [param: In] set => SetSyncVar(value, ref m_minutesInMatchOnGameEnd, 4U);
        }

        public MatchState Networkm_matchState
        {
            get => m_matchState;
            [param: In]
            set
            {
                var num = (int) value;
                ref var local = ref m_matchState;
                if (EvoSGameConfig.NetworkIsClient && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    HookSetMatchState(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar((MatchState) num, ref local, 8U);
            }
        }

        private void HookSetMatchState(MatchState state)
        {
            Networkm_matchState = state;
            if (m_matchState != MatchState.MatchEnd)
                return;
            // TODO
//            m_gameShutdownTime = Time.time + GameManager.Get().GameConfig.GameServerShutdownTime;
//            var team1 = Team.TeamA;
//            var team2 = Team.TeamB;
//            ActorData activeOwnedActorData = GameFlowData.Get().activeOwnedActorData;
//            if (activeOwnedActorData != null && activeOwnedActorData.Team == Team.TeamB)
//            {
//                team1 = Team.TeamB;
//                team2 = Team.TeamA;
//            }
//            GameEventManager.Get().FireEvent(GameEventManager.EventType.MatchEnded, (GameEventManager.GameEventArgs) new GameEventManager.MatchEndedArgs
//            {
//                result = m_gameResult
//            });
//            UIGameOverScreen.Get().Setup(GameManager.Get().GameConfig.GameType, m_gameResult, m_points[(int) team1], m_points[(int) team2]);
        }


        protected static void InvokeSyncListm_points(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_points called on server.");
            else
                ((ObjectivePoints) obj).m_points.HandleMsg(reader);
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                SyncListInt.WriteInstance(writer, m_points);
                writer.Write((int) m_gameResult);
                writer.Write(m_minutesInMatchOnGameEnd);
                writer.Write((int) m_matchState);
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

                SyncListInt.WriteInstance(writer, m_points);
            }

            if (((int) syncVarDirtyBits & 2) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write((int) m_gameResult);
            }

            if (((int) syncVarDirtyBits & 4) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write(m_minutesInMatchOnGameEnd);
            }

            if (((int) syncVarDirtyBits & 8) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                writer.Write((int) m_matchState);
            }

            if (!flag)
                writer.WritePackedUInt32(syncVarDirtyBits);
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                SyncListInt.ReadReference(reader, m_points);
                m_gameResult = (GameResult) reader.ReadInt32();
                m_minutesInMatchOnGameEnd = reader.ReadSingle();
                m_matchState = (MatchState) reader.ReadInt32();
            }
            else
            {
                int num = (int) reader.ReadPackedUInt32();
                if ((num & 1) != 0)
                    SyncListInt.ReadReference(reader, m_points);
                if ((num & 2) != 0)
                    m_gameResult = (GameResult) reader.ReadInt32();
                if ((num & 4) != 0)
                    m_minutesInMatchOnGameEnd = reader.ReadSingle();
                if ((num & 8) == 0)
                    return;
                HookSetMatchState((MatchState) reader.ReadInt32());
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
            m_skipEndOfGameCheck = stream.ReadBoolean();
            stream.AlignTo();
            m_startingPointsTeamA = stream.ReadInt32();
            m_startingPointsTeamB = stream.ReadInt32();
            m_timeLimitTurns = stream.ReadInt32();
            m_disablePowerupsAfterTimeLimit = stream.ReadBoolean();
            stream.AlignTo();
            m_victoryCondition = stream.ReadString32();
            m_victoryConditionOneTurnLeft = stream.ReadString32();
            m_teamAVictoryCondition = new VictoryCondition(assetFile, stream);
            m_teamBVictoryCondition = new VictoryCondition(assetFile, stream);
            m_allowTies = stream.ReadBoolean();
            stream.AlignTo();
            m_passivePointsForTeamWithCharacter = new SerializedVector<PointsForCharacter>(assetFile, stream);
            m_gameModePanelPrefab = new SerializedComponent(assetFile, stream);
            m_matchState = (MatchState) stream.ReadInt32();
        }

        public override string ToString()
        {
            return $"{nameof(ObjectivePoints)}>(" +
                   $"{nameof(m_skipEndOfGameCheck)}: {m_skipEndOfGameCheck}, " +
                   $"{nameof(m_startingPointsTeamA)}: {m_startingPointsTeamA}, " +
                   $"{nameof(m_startingPointsTeamB)}: {m_startingPointsTeamB}, " +
                   $"{nameof(m_timeLimitTurns)}: {m_timeLimitTurns}, " +
                   $"{nameof(m_disablePowerupsAfterTimeLimit)}: {m_disablePowerupsAfterTimeLimit}, " +
                   $"{nameof(m_victoryCondition)}: {m_victoryCondition}, " +
                   $"{nameof(m_victoryConditionOneTurnLeft)}: {m_victoryConditionOneTurnLeft}, " +
                   $"{nameof(m_teamAVictoryCondition)}: {m_teamAVictoryCondition}, " +
                   $"{nameof(m_teamBVictoryCondition)}: {m_teamBVictoryCondition}, " +
                   $"{nameof(m_allowTies)}: {m_allowTies}, " +
                   $"{nameof(m_passivePointsForTeamWithCharacter)}: {m_passivePointsForTeamWithCharacter}, " +
                   $"{nameof(m_gameModePanelPrefab)}: {m_gameModePanelPrefab}, " +
                   $"{nameof(m_matchState)}: {m_matchState}, " +
                   ")";
        }

        public enum MatchState
        {
            InMatch,
            MatchEnd
        }
    }
}
