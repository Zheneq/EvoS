using System;
using System.Collections.Generic;
using System.Diagnostics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using EvoS.Framework.Misc;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("BarrierManager")]
    public class BarrierManager : NetworkBehaviour
    {
        private static int kRpcRpcUpdateBarriers = 73930193;
        private List<Barrier> m_barriers = new List<Barrier>();
        private Dictionary<Team, int> m_movementStates = new Dictionary<Team, int>();
        private Dictionary<Team, int> m_visionStates = new Dictionary<Team, int>();
        private List<BarrierSerializeInfo> m_clientBarrierInfo = new List<BarrierSerializeInfo>();
        private SyncListInt m_barrierIdSync = new SyncListInt();
        private SyncListInt m_movementStatesSync = new SyncListInt();
        private SyncListInt m_visionStatesSync = new SyncListInt();
        private bool m_clientNeedMovementUpdate;
        private bool m_suppressingAbilityBlocks;
        private bool m_hasAbilityBlockingBarriers;
        private static int kListm_barrierIdSync = 1647649475;
        private static int kListm_movementStatesSync = -1285657162;
        private static int kListm_visionStatesSync = -1477195729;

        static BarrierManager()
        {
            RegisterRpcDelegate(typeof(BarrierManager), kRpcRpcUpdateBarriers, InvokeRpcRpcUpdateBarriers);
            RegisterSyncListDelegate(typeof(BarrierManager), kListm_barrierIdSync, InvokeSyncListm_barrierIdSync);
            RegisterSyncListDelegate(typeof(BarrierManager), kListm_movementStatesSync,
                InvokeSyncListm_movementStatesSync);
            RegisterSyncListDelegate(typeof(BarrierManager), kListm_visionStatesSync, InvokeSyncListm_visionStatesSync);
        }

        public BarrierManager()
        {
        }

        public override void Awake()
        {
            m_movementStates.Add(Team.TeamA, 0);
            m_movementStates.Add(Team.TeamB, 0);
            m_movementStates.Add(Team.Objects, 0);
            m_visionStates.Add(Team.TeamA, 0);
            m_visionStates.Add(Team.TeamB, 0);
            m_visionStates.Add(Team.Objects, 0);
            m_barrierIdSync.InitializeBehaviour(this, kListm_barrierIdSync);
            m_movementStatesSync.InitializeBehaviour(this, kListm_movementStatesSync);
            m_visionStatesSync.InitializeBehaviour(this, kListm_visionStatesSync);
        }

        public override void OnStartServer()
        {
            for (int index = 0; index < 3; ++index)
            {
                m_movementStatesSync.Add(0);
                m_visionStatesSync.Add(0);
            }
        }

        public BarrierManager(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public bool IsTeamSupported(Team team)
        {
            return team == Team.TeamA || team == Team.TeamB || team == Team.Objects;
        }

        protected static void InvokeSyncListm_barrierIdSync(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_barrierIdSync called on server.");
            else
                ((BarrierManager) obj).m_barrierIdSync.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_movementStatesSync(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_movementStatesSync called on server.");
            else
                ((BarrierManager) obj).m_movementStatesSync.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_visionStatesSync(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_visionStatesSync called on server.");
            else
                ((BarrierManager) obj).m_visionStatesSync.HandleMsg(reader);
        }

        private Team GetTeamFromSyncIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return Team.TeamA;
                case 1:
                    return Team.TeamB;
                case 2:
                    return Team.Objects;
                default:
                    return Team.Invalid;
            }
        }

        private int GetSyncIndexFromTeam(Team team)
        {
            switch (team)
            {
                case Team.TeamA:
                    return 0;
                case Team.TeamB:
                    return 1;
                case Team.Objects:
                    return 2;
                default:
                    Log.Print(LogType.Error, "Invalid team passed to GetSyncIndexFromTeam()");
                    return 0;
            }
        }

//        [ClientRpc]
        private void RpcUpdateBarriers()
        {
//            if (EvoSGameConfig.NetworkIsServer)
//                return;
//            bool flag = false;
//            for (int index = 0; index < m_barriers.Count; ++index)
//            {
//                if (m_barriers[index].ConsiderAsCover)
//                {
//                    flag = true;
//                    break;
//                }
//            }
//
//            m_barriers.Clear();
//            if (m_barrierIdSync.Count > 50)
//                Log.Print(LogType.Error, "More than 50 barriers active?");
//            for (int index = 0; index < m_barrierIdSync.Count; ++index)
//            {
//                foreach (BarrierSerializeInfo info in m_clientBarrierInfo)
//                {
//                    if (info.m_guid == m_barrierIdSync[index])
//                    {
//                        Barrier fromSerializeInfo = Barrier.CreateBarrierFromSerializeInfo(info);
//                        if (fromSerializeInfo.ConsiderAsCover)
//                            flag = true;
//                        List<ActorData> visionUpdaters;
//                        this.AddBarrier(fromSerializeInfo, false, out visionUpdaters);
//                        break;
//                    }
//                }
//            }
//
//            this.ClientUpdateMovementAndVision();
//            this.UpdateHasAbilityBlockingBarriers();
//            if (!flag)
//                return;
//            GameFlowData.UpdateCoverFromBarriersForAllActors();
        }

        protected static void InvokeRpcRpcUpdateBarriers(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcUpdateBarriers called on server.");
            else
                ((BarrierManager) obj).RpcUpdateBarriers();
        }

        public void CallRpcUpdateBarriers()
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcUpdateBarriers called on client.");
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcUpdateBarriers);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                SendRPCInternal(writer, 0, "RpcUpdateBarriers");
            }
        }

        // [Server] Unused!
        public void UpdateMovementStateForTeam(Team team)
        {
            //if (!NetworkServer.active)
            //{
            //    Debug.LogWarning("[Server] function 'System.Void BarrierManager::UpdateMovementStateForTeam(Team)' called on client");
            //    return;
            //}
            if (!this.IsTeamSupported(team))
            {
                throw new Exception("BarrierManager does not support this team");
            }
            int syncIndexFromTeam = this.GetSyncIndexFromTeam(team);
            int num = this.m_movementStatesSync[syncIndexFromTeam];
            int value = num + 1;
            this.m_movementStates[team] = value;
            this.m_movementStatesSync[syncIndexFromTeam] = value;
        }

        public bool IsMovementBlocked(ActorData mover, BoardSquare source, BoardSquare dest)
        {
            bool result = false;
            for (int i = 0; i < this.m_barriers.Count; i++)
            {
                Barrier barrier = this.m_barriers[i];
                if (!barrier.CanBeMovedThroughBy(mover) && barrier.CrossingBarrier(source.ToVector3(), dest.ToVector3()))
                {
                    result = true;
                    return result;
                }
            }
            return result;
        }

        public int GetMovementStateChangesFor(ActorData mover)
        {
            Team team = mover.method_76();
            if (!this.IsTeamSupported(team))
            {
                return -1;
            }
            return this.m_movementStates[team];
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                SyncListInt.WriteInstance(writer, m_barrierIdSync);
                SyncListInt.WriteInstance(writer, m_movementStatesSync);
                SyncListInt.WriteInstance(writer, m_visionStatesSync);
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

                SyncListInt.WriteInstance(writer, m_barrierIdSync);
            }

            if (((int) syncVarDirtyBits & 2) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, m_movementStatesSync);
            }

            if (((int) syncVarDirtyBits & 4) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListInt.WriteInstance(writer, m_visionStatesSync);
            }

            if (!flag)
                writer.WritePackedUInt32(syncVarDirtyBits);
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                SyncListInt.ReadReference(reader, m_barrierIdSync);
                SyncListInt.ReadReference(reader, m_movementStatesSync);
                SyncListInt.ReadReference(reader, m_visionStatesSync);
            }
            else
            {
                int num = (int) reader.ReadPackedUInt32();
                if ((num & 1) != 0)
                    SyncListInt.ReadReference(reader, m_barrierIdSync);
                if ((num & 2) != 0)
                    SyncListInt.ReadReference(reader, m_movementStatesSync);
                if ((num & 4) == 0)
                    return;
                SyncListInt.ReadReference(reader, m_visionStatesSync);
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
        }

        public override string ToString()
        {
            return $"{nameof(BarrierManager)}(" +
                   ")";
        }

        private class TeamStatusEntry
        {
            public string m_text;
        }
    }
}
