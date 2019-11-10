using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientMovementResults
    {
        public ActorData m_triggeringMover;
        public BoardSquarePathInfo m_triggeringPath;
        public List<ServerClientUtils.SequenceStartData> m_seqStartDataList;
        private bool m_alreadyReacted;
        private ClientEffectResults m_effectResults;
        private ClientBarrierResults m_barrierResults;
        private ClientAbilityResults m_powerupResults;
        private ClientAbilityResults m_gameModeResults;

        public ClientMovementResults(
            ActorData triggeringMover,
            BoardSquarePathInfo triggeringPath,
            List<ServerClientUtils.SequenceStartData> seqStartDataList,
            ClientEffectResults effectResults,
            ClientBarrierResults barrierResults,
            ClientAbilityResults powerupResults,
            ClientAbilityResults gameModeResults)
        {
            m_triggeringMover = triggeringMover;
            m_triggeringPath = triggeringPath;
            m_seqStartDataList = seqStartDataList;
            m_effectResults = effectResults;
            m_barrierResults = barrierResults;
            m_powerupResults = powerupResults;
            m_gameModeResults = gameModeResults;
            if (m_effectResults != null)
                m_effectResults.MarkActorHitsAsMovementHits();
            if (m_barrierResults != null)
                m_barrierResults.MarkActorHitsAsMovementHits();
            if (m_powerupResults != null)
                m_powerupResults.MarkActorHitsAsMovementHits();
            if (m_gameModeResults != null)
                m_gameModeResults.MarkActorHitsAsMovementHits();
            m_alreadyReacted = false;
        }

        public bool TriggerMatchesMovement(ActorData mover, BoardSquarePathInfo curPath)
        {
            if (m_alreadyReacted || mover != m_triggeringMover)
                return false;
            return MovementUtils.ArePathSegmentsEquivalent_FromBeginning(m_triggeringPath, curPath);
        }

        public bool HasSequencesToStart()
        {
            if (m_seqStartDataList == null || m_seqStartDataList.Count == 0)
                return false;
            foreach (ServerClientUtils.SequenceStartData seqStartData in m_seqStartDataList)
            {
                if (seqStartData != null && seqStartData.HasSequencePrefab())
                    return true;
            }

            return false;
        }

        public void ReactToMovement(Component context)
        {
            if (HasSequencesToStart())
            {
                foreach (ServerClientUtils.SequenceStartData seqStartData in m_seqStartDataList)
                    seqStartData.CreateSequencesFromData(context, OnMoveResultsHitActor, OnMoveResultsHitPosition);
            }
            else
            {
                if (ClientAbilityResults.Boolean_0)
                    Log.Print(LogType.Warning,
                        ClientAbilityResults.s_clientHitResultHeader + GetDebugDescription() +
                        ": no Sequence to start, executing results directly");
                if (m_effectResults != null)
                    m_effectResults.RunClientEffectHits();
                else if (m_barrierResults != null)
                    m_barrierResults.RunClientBarrierHits();
                else if (m_powerupResults != null)
                    m_powerupResults.RunClientAbilityHits();
                else if (m_gameModeResults != null)
                    m_gameModeResults.RunClientAbilityHits();
            }

            m_alreadyReacted = true;
        }

        internal void OnMoveResultsHitActor(ActorData target)
        {
            if (m_effectResults != null)
                m_effectResults.OnEffectHitActor(target);
            else if (m_barrierResults != null)
                m_barrierResults.OnBarrierHitActor(target);
            else if (m_powerupResults != null)
            {
                m_powerupResults.OnAbilityHitActor(target);
            }
            else
            {
                if (m_gameModeResults == null)
                    return;
                m_gameModeResults.OnAbilityHitActor(target);
            }
        }

        internal void OnMoveResultsHitPosition(Vector3 position)
        {
            if (m_effectResults != null)
                m_effectResults.OnEffectHitPosition(position);
            else if (m_barrierResults != null)
                m_barrierResults.OnBarrierHitPosition(position);
            else if (m_powerupResults != null)
            {
                m_powerupResults.OnAbilityHitPosition(position);
            }
            else
            {
                if (m_gameModeResults == null)
                    return;
                m_gameModeResults.OnAbilityHitPosition(position);
            }
        }

        internal bool DoneHitting()
        {
            bool flag;
            if (m_effectResults != null)
                flag = m_effectResults.DoneHitting();
            else if (m_barrierResults != null)
                flag = m_barrierResults.DoneHitting();
            else if (m_powerupResults != null)
                flag = m_powerupResults.DoneHitting();
            else if (m_gameModeResults != null)
            {
                flag = m_gameModeResults.DoneHitting();
            }
            else
            {
                Log.Print(LogType.Error,
                    "ClientMovementResults has neither effect results nor barrier results nor powerup results.  Assuming it's done hitting...");
                flag = true;
            }

            return flag;
        }

        public bool HasUnexecutedHitOnActor(ActorData actor)
        {
            bool flag = false;
            if (m_effectResults != null)
                flag = m_effectResults.HasUnexecutedHitOnActor(actor);
            else if (m_barrierResults != null)
                flag = m_barrierResults.HasUnexecutedHitOnActor(actor);
            else if (m_powerupResults != null)
                flag = m_powerupResults.HasUnexecutedHitOnActor(actor);
            else if (m_gameModeResults != null)
                flag = m_gameModeResults.HasUnexecutedHitOnActor(actor);
            return flag;
        }

        public bool HasEffectHitResults()
        {
            return m_effectResults != null;
        }

        public bool HasBarrierHitResults()
        {
            return m_barrierResults != null;
        }

        public bool HasPowerupHitResults()
        {
            return m_powerupResults != null;
        }

        public bool HasGameModeHitResults()
        {
            return m_gameModeResults != null;
        }

        public bool ContainsSequenceSource(SequenceSource sequenceSource)
        {
            if (sequenceSource != null)
                return ContainsSequenceSourceID(sequenceSource.RootID);
            return false;
        }

        public bool ContainsSequenceSourceID(uint id)
        {
            bool flag = false;
            if (m_seqStartDataList != null)
            {
                for (int index = 0; index < m_seqStartDataList.Count; ++index)
                {
                    if (m_seqStartDataList[index].ContainsSequenceSourceID(id))
                    {
                        flag = true;
                        break;
                    }
                }
            }

            return flag;
        }

        public string GetDebugDescription()
        {
            string str = string.Empty;
            if (m_effectResults != null)
                str = m_effectResults.GetDebugDescription();
            else if (m_barrierResults != null)
                str = m_barrierResults.GetDebugDescription();
            else if (m_powerupResults != null)
                str = m_powerupResults.GetDebugDescription();
            else if (m_gameModeResults != null)
                str = m_gameModeResults.GetDebugDescription();
            return str + " triggering on " + m_triggeringMover.method_95();
        }

        internal void ExecuteUnexecutedClientHits()
        {
            if (m_effectResults != null)
                m_effectResults.ExecuteUnexecutedClientHits();
            else if (m_barrierResults != null)
                m_barrierResults.ExecuteUnexecutedClientHits();
            else if (m_powerupResults != null)
            {
                m_powerupResults.ExecuteUnexecutedClientHits();
            }
            else
            {
                if (m_gameModeResults == null)
                    return;
                m_gameModeResults.ExecuteUnexecutedClientHits();
            }
        }

        internal void ExecuteReactionHitsWithExtraFlagsOnActor(
            ActorData targetActor,
            ActorData caster,
            bool hasDamage,
            bool hasHealing)
        {
            if (m_effectResults != null)
                m_effectResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            else if (m_barrierResults != null)
                m_barrierResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            else if (m_powerupResults != null)
            {
                m_powerupResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            }
            else
            {
                if (m_gameModeResults == null)
                    return;
                m_gameModeResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            }
        }

        internal string UnexecutedHitsDebugStr()
        {
            string str = $"\n\tUnexecuted hits:\n\t\tMovement hit on {m_triggeringMover.method_95()}\n";
            if (m_effectResults != null)
                str += m_effectResults.UnexecutedHitsDebugStr();
            else if (m_barrierResults != null)
                str += m_barrierResults.UnexecutedHitsDebugStr();
            else if (m_powerupResults != null)
                str += m_powerupResults.UnexecutedHitsDebugStr();
            else if (m_gameModeResults != null)
                str += m_gameModeResults.UnexecutedHitsDebugStr();
            return str;
        }
    }
}
