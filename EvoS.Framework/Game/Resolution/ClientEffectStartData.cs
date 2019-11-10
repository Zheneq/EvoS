using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientEffectStartData
    {
        public int m_effectGUID;
        public List<ServerClientUtils.SequenceStartData> m_sequenceStartDataList;
        public ActorData m_caster;
        public ActorData m_effectTarget;
        public List<StatusType> m_statuses;
        public List<StatusType> m_statusesOnTurnStart;
        public int m_absorb;
        public int m_expectedHoT;
        public bool m_isBuff;
        public bool m_isDebuff;
        public bool m_hasMovementDebuff;

        public ClientEffectStartData(
            int effectGUID,
            List<ServerClientUtils.SequenceStartData> sequenceStartDataList,
            ActorData effectTarget,
            ActorData caster,
            List<StatusType> statuses,
            List<StatusType> statusesOnTurnStart,
            int absorb,
            int expectedHoT,
            bool isBuff,
            bool isDebuff,
            bool hasMovementDebuff)
        {
            m_effectGUID = effectGUID;
            m_sequenceStartDataList = sequenceStartDataList;
            m_effectTarget = effectTarget;
            m_caster = caster;
            m_statuses = statuses;
            m_statusesOnTurnStart = statusesOnTurnStart;
            m_absorb = absorb;
            m_expectedHoT = expectedHoT;
            m_isBuff = isBuff;
            m_isDebuff = isDebuff;
            m_hasMovementDebuff = hasMovementDebuff;
        }

        internal void OnClientEffectStartSequenceHitActor(ActorData target)
        {
        }

        internal void OnClientEffectStartSequenceHitPosition(Vector3 position)
        {
        }
    }
}
