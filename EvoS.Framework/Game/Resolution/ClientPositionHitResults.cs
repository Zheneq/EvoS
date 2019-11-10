using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientPositionHitResults
    {
        private List<ClientEffectStartData> m_effectsToStart;
        private List<ClientBarrierStartData> m_barriersToStart;
        private List<int> m_effectsToRemove;
        private List<int> m_barriersToRemove;
        private List<ServerClientUtils.SequenceEndData> m_sequencesToEnd;
        private List<ClientMovementResults> m_reactionsOnPosHit;

        public ClientPositionHitResults(Component context, ref IBitStream stream)
        {
            m_effectsToStart = AbilityResultsUtils.DeSerializeEffectsToStartFromStream(context, ref stream);
            m_barriersToStart = AbilityResultsUtils.DeSerializeBarriersToStartFromStream(context, ref stream);
            m_effectsToRemove = AbilityResultsUtils.DeSerializeEffectsForRemovalFromStream(ref stream);
            m_barriersToRemove = AbilityResultsUtils.DeSerializeBarriersForRemovalFromStream(ref stream);
            m_sequencesToEnd = AbilityResultsUtils.DeSerializeSequenceEndDataListFromStream(ref stream);
            m_reactionsOnPosHit =
                AbilityResultsUtils.DeSerializeClientMovementResultsListFromStream(context, ref stream);
            ExecutedHit = false;
        }

        public bool ExecutedHit { get; private set; }

        public void ExecutePositionHit()
        {
            throw new NotImplementedException();
//            if (ExecutedHit)
//                return;
//            if (ClientAbilityResults.Boolean_0)
//                Debug.LogWarning((object) (ClientAbilityResults.s_executePositionHitHeader +
//                                           " Executing Position Hit"));
//            foreach (ClientEffectStartData effectData in m_effectsToStart)
//                ClientEffectBarrierManager.Get().ExecuteEffectStart(effectData);
//            foreach (ClientBarrierStartData barrierData in m_barriersToStart)
//                ClientEffectBarrierManager.Get().ExecuteBarrierStart(barrierData);
//            foreach (int effectGuid in m_effectsToRemove)
//                ClientEffectBarrierManager.Get().EndEffect(effectGuid);
//            foreach (int barrierGuid in m_barriersToRemove)
//                ClientEffectBarrierManager.Get().EndBarrier(barrierGuid);
//            foreach (ServerClientUtils.SequenceEndData sequenceEndData in m_sequencesToEnd)
//                sequenceEndData.EndClientSequences();
//            foreach (ClientMovementResults clientMovementResults in m_reactionsOnPosHit)
//                clientMovementResults.ReactToMovement();
//            ExecutedHit = true;
//            ClientResolutionManager.Get().UpdateLastEventTime();
        }
    }
}
