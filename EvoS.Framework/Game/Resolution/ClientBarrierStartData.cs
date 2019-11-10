using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientBarrierStartData
    {
        public int m_barrierGUID;
        public List<ServerClientUtils.SequenceStartData> m_sequenceStartDataList;
        public BarrierSerializeInfo m_barrierGameplayInfo;

        public ClientBarrierStartData(
            int barrierGUID,
            List<ServerClientUtils.SequenceStartData> sequenceStartDataList,
            BarrierSerializeInfo gameplayInfo)
        {
            m_barrierGUID = barrierGUID;
            m_sequenceStartDataList = sequenceStartDataList;
            m_barrierGameplayInfo = gameplayInfo;
        }

        public void ExecuteBarrierStart(Component context)
        {
            foreach (var sequenceStartData in m_sequenceStartDataList)
                sequenceStartData.CreateSequencesFromData(context, OnClientBarrierStartSequenceHitActor,
                    OnClientBarrierStartSequenceHitPosition);
        }

        internal void OnClientBarrierStartSequenceHitActor(ActorData target)
        {
        }

        internal void OnClientBarrierStartSequenceHitPosition(Vector3 position)
        {
        }
    }
}
