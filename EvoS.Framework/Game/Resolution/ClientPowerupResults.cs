using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientPowerupResults
    {
        private List<ServerClientUtils.SequenceStartData> m_seqStartDataList;
        private ClientAbilityResults m_powerupAbilityResults;

        public ClientPowerupResults(
            List<ServerClientUtils.SequenceStartData> seqStartDataList,
            ClientAbilityResults clientAbilityResults)
        {
            m_seqStartDataList = seqStartDataList;
            m_powerupAbilityResults = clientAbilityResults;
        }

        public bool HasSequencesToStart()
        {
            if (m_seqStartDataList == null || m_seqStartDataList.Count == 0)
                return false;
            foreach (var seqStartData in m_seqStartDataList)
            {
                if (seqStartData != null && seqStartData.HasSequencePrefab())
                    return true;
            }

            return false;
        }

        public void RunResults(Component context)
        {
            if (HasSequencesToStart())
            {
                foreach (var seqStartData in m_seqStartDataList)
                    seqStartData.CreateSequencesFromData(context, OnPowerupHitActor, OnPowerupHitPosition);
            }
            else
            {
                if (ClientAbilityResults.Boolean_0)
                    Log.Print(LogType.Warning,
                        $"{ClientAbilityResults.s_clientHitResultHeader}{GetDebugDescription()}: no Sequence to start, executing results directly");
                m_powerupAbilityResults.RunClientAbilityHits();
            }
        }

        internal void OnPowerupHitActor(ActorData target)
        {
            m_powerupAbilityResults.OnAbilityHitActor(target);
        }

        internal void OnPowerupHitPosition(Vector3 position)
        {
            m_powerupAbilityResults.OnAbilityHitPosition(position);
        }

        internal string GetDebugDescription()
        {
            if (m_powerupAbilityResults != null)
                return m_powerupAbilityResults.GetDebugDescription();
            return "Powerup UNKNWON";
        }
    }
}
