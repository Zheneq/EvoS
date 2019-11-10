using System;
using System.Numerics;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("ActorController")]
    public class ActorController : NetworkBehaviour
    {
        private static int kCmdCmdDebugTeleportRequest = -1583259838;
        private static int kCmdCmdPickedRespawnRequest;
        private static int kCmdCmdSendMinimapPing;
        private static int kCmdCmdSendAbilityPing;
        private static int kCmdCmdSelectAbilityRequest;
        private static int kCmdCmdQueueSimpleActionRequest;
        private static int kCmdCmdCustomGamePause;
        private static int kRpcRpcUpdateRemainingMovement;

        private ActorData m_actor;

        static ActorController()
        {
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdDebugTeleportRequest,
                InvokeCmdCmdDebugTeleportRequest);
            kCmdCmdPickedRespawnRequest = 1763304984;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdPickedRespawnRequest,
                InvokeCmdCmdPickedRespawnRequest);
            kCmdCmdSendMinimapPing = -810618818;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdSendMinimapPing, InvokeCmdCmdSendMinimapPing);
            kCmdCmdSendAbilityPing = -963392189;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdSendAbilityPing, InvokeCmdCmdSendAbilityPing);
            kCmdCmdSelectAbilityRequest = -1183646894;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdSelectAbilityRequest,
                InvokeCmdCmdSelectAbilityRequest);
            kCmdCmdQueueSimpleActionRequest = -797856057;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdQueueSimpleActionRequest,
                InvokeCmdCmdQueueSimpleActionRequest);
            kCmdCmdCustomGamePause = 983951586;
            RegisterCommandDelegate(typeof(ActorController), kCmdCmdCustomGamePause, InvokeCmdCmdCustomGamePause);
            kRpcRpcUpdateRemainingMovement = 64425877;
            RegisterRpcDelegate(typeof(ActorController), kRpcRpcUpdateRemainingMovement,
                InvokeRpcRpcUpdateRemainingMovement);
        }

        public ActorController()
        {
        }

        public ActorController(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void Awake()
        {
            m_actor = GetComponent<ActorData>();
        }

//        [Command]
        private void CmdDebugTeleportRequest(int x, int y)
        {
        }

//        [Command]
        private void CmdPickedRespawnRequest(int x, int y)
        {
        }

//        [Command]
        internal void CmdSendMinimapPing(
            int teamIndex,
            Vector3 worldPosition,
            ActorController.PingType pingType)
        {
        }

//        [Command]
        internal void CmdSendAbilityPing(int teamIndex, LocalizationArg_AbilityPing localizedPing)
        {
        }

//  [Command]
        protected void CmdSelectAbilityRequest(int actionTypeInt)
        {
        }

//  [Command]
        protected void CmdQueueSimpleActionRequest(int actionTypeInt)
        {
        }

        public void RequestCustomGamePause(bool desiredPause, int requestActorIndex)
        {
            if (EvoSGameConfig.NetworkIsServer)
                HandleCustomGamePauseOnServer(desiredPause, requestActorIndex);
            else
                CallCmdCustomGamePause(desiredPause, requestActorIndex);
        }

        private void HandleCustomGamePauseOnServer(bool desiredPause, int requestActorIndex)
        {
        }

//  [Command]
        private void CmdCustomGamePause(bool desiredPause, int requestActorIndex)
        {
            HandleCustomGamePauseOnServer(desiredPause, requestActorIndex);
        }

//        [ClientRpc]
        internal void RpcUpdateRemainingMovement(
            float remainingMovement,
            float remainingMovementWithQueuedAbility)
        {
            if (m_actor == null || GameFlowData == null || GameFlowData.activeOwnedActorData != m_actor)
                return;
            bool flag = false;
            if ((double) m_actor.RemainingHorizontalMovement != remainingMovement)
            {
                m_actor.RemainingHorizontalMovement = remainingMovement;
                flag = true;
            }

            if ((double) m_actor.RemainingMovementWithQueuedAbility != remainingMovementWithQueuedAbility)
            {
                m_actor.RemainingMovementWithQueuedAbility = remainingMovementWithQueuedAbility;
                flag = true;
            }

            if (!flag)
                return;
            m_actor.method_9().UpdateSquaresCanMoveTo();
        }

        protected static void InvokeCmdCmdDebugTeleportRequest(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdDebugTeleportRequest called on client.");
            else
                ((ActorController) obj).CmdDebugTeleportRequest((int) reader.ReadPackedUInt32(),
                    (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdPickedRespawnRequest(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdPickedRespawnRequest called on client.");
            else
                ((ActorController) obj).CmdPickedRespawnRequest((int) reader.ReadPackedUInt32(),
                    (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdSendMinimapPing(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSendMinimapPing called on client.");
            else
                ((ActorController) obj).CmdSendMinimapPing((int) reader.ReadPackedUInt32(), reader.ReadVector3(),
                    (PingType) reader.ReadInt32());
        }

        protected static void InvokeCmdCmdSendAbilityPing(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSendAbilityPing called on client.");
            else
                ((ActorController) obj).CmdSendAbilityPing((int) reader.ReadPackedUInt32(),
                    GeneratedNetworkCode._ReadLocalizationArg_AbilityPing_None(reader));
        }

        protected static void InvokeCmdCmdSelectAbilityRequest(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdSelectAbilityRequest called on client.");
            else
                ((ActorController) obj).CmdSelectAbilityRequest((int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdQueueSimpleActionRequest(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdQueueSimpleActionRequest called on client.");
            else
                ((ActorController) obj).CmdQueueSimpleActionRequest((int) reader.ReadPackedUInt32());
        }

        protected static void InvokeCmdCmdCustomGamePause(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsServer)
                Log.Print(LogType.Error, "Command CmdCustomGamePause called on client.");
            else
                ((ActorController) obj).CmdCustomGamePause(reader.ReadBoolean(), (int) reader.ReadPackedUInt32());
        }

        public void CallCmdDebugTeleportRequest(int x, int y)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdDebugTeleportRequest called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                this.CmdDebugTeleportRequest(x, y);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdDebugTeleportRequest);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) x);
                writer.WritePackedUInt32((uint) y);
                // this.SendCommandInternal(writer, 0, "CmdDebugTeleportRequest");
                throw new NotImplementedException();
            }
        }

        public void CallCmdPickedRespawnRequest(int x, int y)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdPickedRespawnRequest called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                this.CmdPickedRespawnRequest(x, y);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdPickedRespawnRequest);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) x);
                writer.WritePackedUInt32((uint) y);
                // this.SendCommandInternal(writer, 0, "CmdPickedRespawnRequest");
                throw new NotImplementedException();
            }
        }

        public void CallCmdSendMinimapPing(
            int teamIndex,
            Vector3 worldPosition,
            PingType pingType)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSendMinimapPing called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                this.CmdSendMinimapPing(teamIndex, worldPosition, pingType);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSendMinimapPing);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) teamIndex);
                writer.Write(worldPosition);
                writer.Write((int) pingType);
                // this.SendCommandInternal(writer, 0, "CmdSendMinimapPing");
                throw new NotImplementedException();
            }
        }

        public void CallCmdSendAbilityPing(int teamIndex, LocalizationArg_AbilityPing localizedPing)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSendAbilityPing called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                this.CmdSendAbilityPing(teamIndex, localizedPing);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSendAbilityPing);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) teamIndex);
                GeneratedNetworkCode._WriteLocalizationArg_AbilityPing_None(writer, localizedPing);
                // this.SendCommandInternal(writer, 0, "CmdSendAbilityPing");
                throw new NotImplementedException();
            }
        }

        public void CallCmdSelectAbilityRequest(int actionTypeInt)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdSelectAbilityRequest called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdSelectAbilityRequest(actionTypeInt);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdSelectAbilityRequest);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) actionTypeInt);
                // this.SendCommandInternal(writer, 0, "CmdSelectAbilityRequest");
                throw new NotImplementedException();
            }
        }

        public void CallCmdQueueSimpleActionRequest(int actionTypeInt)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdQueueSimpleActionRequest called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdQueueSimpleActionRequest(actionTypeInt);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdQueueSimpleActionRequest);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) actionTypeInt);
                // this.SendCommandInternal(writer, 0, "CmdQueueSimpleActionRequest");
                throw new NotImplementedException();
            }
        }

        public void CallCmdCustomGamePause(bool desiredPause, int requestActorIndex)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "Command function CmdCustomGamePause called on server.");
            else if (EvoSGameConfig.NetworkIsServer)
            {
                CmdCustomGamePause(desiredPause, requestActorIndex);
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 5);
                writer.WritePackedUInt32((uint) kCmdCmdCustomGamePause);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(desiredPause);
                writer.WritePackedUInt32((uint) requestActorIndex);
                // this.SendCommandInternal(writer, 0, "CmdCustomGamePause");
                throw new NotImplementedException();
            }
        }

        protected static void InvokeRpcRpcUpdateRemainingMovement(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcUpdateRemainingMovement called on server.");
            else
                ((ActorController) obj).RpcUpdateRemainingMovement(reader.ReadSingle(), reader.ReadSingle());
        }

        public void CallRpcUpdateRemainingMovement(
            float remainingMovement,
            float remainingMovementWithQueuedAbility)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcUpdateRemainingMovement called on client.");
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcUpdateRemainingMovement);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.Write(remainingMovement);
                writer.Write(remainingMovementWithQueuedAbility);
                SendRPCInternal(writer, 0, "RpcUpdateRemainingMovement");
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            bool flag = false;
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
        }

        public override string ToString()
        {
            return $"{nameof(ActorController)}>(" +
                   ")";
        }

        public enum PingType
        {
            Default,
            Assist,
            Defend,
            Enemy,
            Move
        }
    }
}
