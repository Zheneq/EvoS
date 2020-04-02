using System;
using System.Collections.Generic;
using EvoS.Framework.Assets;
using EvoS.Framework.Assets.Serialized.Behaviours;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.NetworkBehaviours
{
    [Serializable]
    [SerializedMonoBehaviour("ActorStatus")]
    public class ActorStatus : NetworkBehaviour
    {
        private static int kListm_statusCounts = -7231791;
        private static int kListm_statusDurations = 625641650;
        private SyncListUInt _statusCounts = new SyncListUInt();
        private SyncListUInt _statusDurations = new SyncListUInt();
        private List<Ability> _passivePendingStatusSources = new List<Ability>();
        private int[] _statusCountsPrevious;
        private int[] _clientStatusCountAdjustments;
        private ActorData _actorData;

        public SyncListUInt StatusCounts => _statusCounts;
        public SyncListUInt StatusDurations => _statusDurations;
        public int[] StatusCountsPrevious => _statusCountsPrevious;
        public int[] ClientStatusCountAdjustments => _clientStatusCountAdjustments;

        static ActorStatus()
        {
            RegisterSyncListDelegate(typeof(ActorStatus), kListm_statusCounts, InvokeSyncListm_statusCounts);
            RegisterSyncListDelegate(typeof(ActorStatus), kListm_statusDurations, InvokeSyncListm_statusDurations);
        }

        public ActorStatus()
        {
        }

        public ActorStatus(AssetFile assetFile, StreamReader stream)
        {
            DeserializeAsset(assetFile, stream);
        }

        public bool HasStatus(StatusType status, bool includePending = true)
        {
            var index1 = (int) status;
            var num = index1 >= _statusCounts.Count ? 0 : (int) _statusCounts[index1];
            bool flag;
            if (!(flag = num + _clientStatusCountAdjustments[index1] > 0) && includePending &&
                _passivePendingStatusSources.Count > 0)
            {
                for (var index2 = 0; index2 < _passivePendingStatusSources.Count && !flag; ++index2)
                {
                    var pendingStatusSource = _passivePendingStatusSources[index2];
                    flag = ((flag ? 1 : 0) | (pendingStatusSource == null
                                ? 0
                                : (pendingStatusSource.HasPassivePendingStatus(status, _actorData) ? 1 : 0))) != 0;
                }
            }

            if (GameplayMutators != null)
            {
                int currentTurn = GameFlowData.CurrentTurn;
                if (!flag)
                    flag |= GameplayMutators.IsStatusActive(status, currentTurn);
                if (flag)
                    flag = !GameplayMutators.IsStatusSuppressed(status, currentTurn);
            }

            return flag;
        }


        public override void Awake()
        {
            _statusCountsPrevious = new int[58];
            _clientStatusCountAdjustments = new int[58];
            _actorData = GetComponent<ActorData>();
            _statusCounts.InitializeBehaviour(this, kListm_statusCounts);
            _statusDurations.InitializeBehaviour(this, kListm_statusDurations);
        }

        protected static void InvokeSyncListm_statusCounts(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_statusCounts called on server.");
            else
                ((ActorStatus) obj)._statusCounts.HandleMsg(reader);
        }

        protected static void InvokeSyncListm_statusDurations(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!EvoSGameConfig.NetworkIsClient)
                Log.Print(LogType.Error, "SyncList m_statusDurations called on server.");
            else
                ((ActorStatus) obj)._statusDurations.HandleMsg(reader);
        }

        public bool IsMovementDebuffImmune(bool checkPending = true)
        {
            bool result;
            if (!(result = (this.HasStatus(StatusType.MovementDebuffImmunity, true) || this.HasStatus(StatusType.Unstoppable, true))) && this._actorData.GetAbilityData() != null && checkPending)
            {
                result = (this._actorData.GetAbilityData().HasPendingStatusFromQueuedAbilities(StatusType.Unstoppable) ||
                    this._actorData.GetAbilityData().HasPendingStatusFromQueuedAbilities(StatusType.MovementDebuffImmunity));
            }
            return result;
        }

        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                SyncListUInt.WriteInstance(writer, _statusCounts);
                SyncListUInt.WriteInstance(writer, _statusDurations);
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

                SyncListUInt.WriteInstance(writer, _statusCounts);
            }

            if (((int) syncVarDirtyBits & 2) != 0)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }

                SyncListUInt.WriteInstance(writer, _statusDurations);
            }

            if (!flag)
                writer.WritePackedUInt32(syncVarDirtyBits);
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                SyncListUInt.ReadReference(reader, _statusCounts);
                SyncListUInt.ReadReference(reader, _statusDurations);
            }
            else
            {
                int num = (int) reader.ReadPackedUInt32();
                if ((num & 1) != 0)
                    SyncListUInt.ReadReference(reader, _statusCounts);
                if ((num & 2) == 0)
                    return;
                SyncListUInt.ReadReference(reader, _statusDurations);
            }
        }

        public override void DeserializeAsset(AssetFile assetFile, StreamReader stream)
        {
        }

        public override string ToString()
        {
            return $"{nameof(ActorStatus)}>(" +
                   ")";
        }
    }
}
