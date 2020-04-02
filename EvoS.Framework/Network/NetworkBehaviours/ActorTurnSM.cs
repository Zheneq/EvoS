using System;
using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("ActorTurnSM")]
    public class ActorTurnSM : NetworkBehaviour
    {
        private static int kRpcRpcTurnMessage = -107921272;
        private static int kRpcRpcStoreAutoQueuedAbilityRequest = 675585254;
        public static int kCmdCmdSetSquare = -1156253069;
        public static int kCmdCmdGUITurnMessage = -122150213;
        public TurnStateEnum CurrentState { get; private set; }
        public TurnStateEnum PreviousState { get; private set; }

        private List<AbilityTarget> m_targets;

        static ActorTurnSM()
        {
            RegisterRpcDelegate(typeof(ActorTurnSM), kRpcRpcTurnMessage, InvokeRpcRpcTurnMessage);
            RegisterRpcDelegate(typeof(ActorTurnSM), kRpcRpcStoreAutoQueuedAbilityRequest,
                InvokeRpcRpcStoreAutoQueuedAbilityRequest);
        }

        public ActorTurnSM()
        {
        }

        public ActorTurnSM(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public override void Awake()
        {
            base.Awake();
            this.m_targets = new List<AbilityTarget>();
        }

        protected static void InvokeRpcRpcTurnMessage(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcTurnMessage called on server.");
            else
                ((ActorTurnSM) obj).RpcTurnMessage((int) reader.ReadPackedUInt32(), (int) reader.ReadPackedUInt32());
        }

        protected static void InvokeRpcRpcStoreAutoQueuedAbilityRequest(
            NetworkBehaviour obj,
            NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "RPC RpcStoreAutoQueuedAbilityRequest called on server.");
            else
                ((ActorTurnSM) obj).RpcStoreAutoQueuedAbilityRequest((int) reader.ReadPackedUInt32());
        }

        public void CallRpcTurnMessage(TurnMessage msg, int extraData)
        {
            CallRpcTurnMessage((int) msg, extraData);
        }

        public void CallRpcTurnMessage(int msgEnum, int extraData)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcTurnMessage called on client.");
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcTurnMessage);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) msgEnum);
                writer.WritePackedUInt32((uint) extraData);
                SendRPCInternal(writer, 0, "RpcTurnMessage");
            }
        }

        public void CallRpcStoreAutoQueuedAbilityRequest(int actionTypeInt)
        {
            if (!EvoSGameConfig.NetworkIsServer)
            {
                Log.Print(LogType.Error, "RPC Function RpcStoreAutoQueuedAbilityRequest called on client.");
            }
            else
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write((short) 0);
                writer.Write((short) 2);
                writer.WritePackedUInt32((uint) kRpcRpcStoreAutoQueuedAbilityRequest);
                writer.Write(GetComponent<NetworkIdentity>().netId);
                writer.WritePackedUInt32((uint) actionTypeInt);
                SendRPCInternal(writer, 0, "RpcStoreAutoQueuedAbilityRequest");
            }
        }

//        [ClientRpc]
        private void RpcTurnMessage(int msgEnum, int extraData)
        {
            Log.Print(LogType.Game, $"ActorTurnSM.RpcTurnMessage: {(TurnMessage)msgEnum}, {extraData}");
//            TurnMessage msg = (TurnMessage) msgEnum;
//            if (!this.m_actorData.HasBotController && this.m_actorData == GameFlowData.Get().activeOwnedActorData &&
//                !this.m_actorData.method_38())
//            {
//                if (msg == TurnMessage.BEGIN_RESOLVE && this.GetState() != this.m_turnStates[0] &&
//                    (this.GetState() != this.m_turnStates[2] && this.GetState() != this.m_turnStates[5]) &&
//                    this.GetState() != this.m_turnStates[7])
//                {
//                    if (this.m_requestStackForUndo.IsNullOrEmpty<ActorTurnSM.ActionRequestForUndo>() &&
//                        this.m_autoQueuedRequestActionTypes.IsNullOrEmpty<AbilityData.ActionType>())
//                    {
//                        int lastTargetIndex = -1;
//                        string str = "(none)";
//                        ActorController actorController = this.m_actorData.method_12();
//                        if (actorController != null)
//                        {
//                            Ability lastTargetedAbility = actorController.GetLastTargetedAbility(ref lastTargetIndex);
//                            if (lastTargetedAbility != null)
//                                str = lastTargetedAbility.m_abilityName;
//                        }
//
//                        Log.Print(LogType.Error,
//                            ("Player " + this.m_actorData.DisplayName +
//                             " skipped turn (could be AFK) in client ActorTurnSM state " +
//                             this.GetState().GetType().ToString() + ". LastTargetedAbility: " + str +
//                             ", targeterIndex: " + lastTargetIndex + ". GuiUtility.hotControl: " +
//                             GUIUtility.hotControl.ToString()));
//                    }
//                }
//                else if (msg == TurnMessage.TURN_START && this.GetState() != this.m_turnStates[7] &&
//                         (this.GetState() != this.m_turnStates[0] && this.GetState() != this.m_turnStates[8]) &&
//                         (this.GetState() != this.m_turnStates[5] && this.GetState() != this.m_turnStates[6]))
//                    Log.Print(LogType.Error,
//                        ("Player " + this.m_actorData.DisplayName +
//                         " received TURN_START in client ActorTurnSM state " + this.GetState().GetType().ToString() +
//                         " which doesn't handle that transition."));
//            }
//
//            this.GetState().OnMsg(msg, extraData);
//            this.UpdateStates();
        }

//        [ClientRpc]
        private void RpcStoreAutoQueuedAbilityRequest(int actionTypeInt)
        {
            if (EvoSGameConfig.NetworkIsServer)
                return;
//            this.StoreAutoQueuedAbilityRequest((AbilityData.ActionType) actionTypeInt);
        }

        public void ClearAbilityTargets()
        {
            this.m_targets.Clear();
        }

        public void AddAbilityTarget(AbilityTarget newTarget)
        {
            this.m_targets.Add(newTarget);
        }

        public List<AbilityTarget> GetAbilityTargets()
        {
            return this.m_targets;
        }
        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            return false;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
        }

        public override string ToString()
        {
            return $"{nameof(ActorTurnSM)}>(" +
                   ")";
        }
    }
}
