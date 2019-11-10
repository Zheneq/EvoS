using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Game;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientEffectResults
    {
        private int m_effectGUID;
        private ActorData m_effectCaster;
        private AbilityData.ActionType m_sourceAbilityActionType;
        private List<ServerClientUtils.SequenceStartData> m_seqStartDataList;
        private Dictionary<ActorData, ClientActorHitResults> m_actorToHitResults;
        private Dictionary<Vector3, ClientPositionHitResults> m_posToHitResults;

        public ClientEffectResults(
            int effectGUID,
            ActorData effectCaster,
            AbilityData.ActionType sourceAbilityActionType,
            List<ServerClientUtils.SequenceStartData> seqStartDataList,
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> posToHitResults)
        {
            m_effectGUID = effectGUID;
            m_sourceAbilityActionType = sourceAbilityActionType;
            m_effectCaster = effectCaster;
            m_seqStartDataList = seqStartDataList;
            m_actorToHitResults = actorToHitResults;
            m_posToHitResults = posToHitResults;
        }

        public ActorData GetCaster()
        {
            return m_effectCaster;
        }

        public AbilityData.ActionType GetSourceActionType()
        {
            return m_sourceAbilityActionType;
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

        public void StartSequences(Component context)
        {
            if (HasSequencesToStart())
            {
                foreach (ServerClientUtils.SequenceStartData seqStartData in m_seqStartDataList)
                    seqStartData.CreateSequencesFromData(context, OnEffectHitActor, OnEffectHitPosition);
            }
            else
            {
                if (ClientAbilityResults.Boolean_0)
                    Log.Print(LogType.Warning,
                        $"{ClientAbilityResults.s_clientHitResultHeader}{GetDebugDescription()}: no Sequence to start, executing results directly");
                RunClientEffectHits();
            }
        }

        public void RunClientEffectHits()
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in m_actorToHitResults)
                OnEffectHitActor(actorToHitResult.Key);
            foreach (KeyValuePair<Vector3, ClientPositionHitResults> posToHitResult in m_posToHitResults)
                OnEffectHitPosition(posToHitResult.Key);
        }

        internal void OnEffectHitActor(ActorData target)
        {
            if (m_actorToHitResults.ContainsKey(target))
                m_actorToHitResults[target].ExecuteActorHit(target, m_effectCaster);
            else
                Log.Print(LogType.Error,
                    $"ClientEffectResults error-- Sequence hitting actor {target.method_95()}, but that actor isn't in our hit results.");
        }

        internal void OnEffectHitPosition(Vector3 position)
        {
            if (!m_posToHitResults.ContainsKey(position))
                return;
            m_posToHitResults[position].ExecutePositionHit();
        }

        internal bool DoneHitting()
        {
            return ClientResolutionAction.DoneHitting(m_actorToHitResults, m_posToHitResults);
        }

        internal bool HasUnexecutedHitOnActor(ActorData targetActor)
        {
            return ClientResolutionAction.HasUnexecutedHitOnActor(targetActor, m_actorToHitResults);
        }

        internal void ExecuteUnexecutedClientHits()
        {
            ClientResolutionAction.ExecuteUnexecutedHits(m_actorToHitResults, m_posToHitResults, m_effectCaster);
        }

        internal void ExecuteReactionHitsWithExtraFlagsOnActor(
            ActorData targetActor,
            ActorData caster,
            bool hasDamage,
            bool hasHealing)
        {
            ClientResolutionAction.ExecuteReactionHitsWithExtraFlagsOnActorAux(m_actorToHitResults, targetActor, caster,
                hasDamage, hasHealing);
        }

        public void AdjustKnockbackCounts_ClientEffectResults(
            ref Dictionary<ActorData, int> outgoingKnockbacks,
            ref Dictionary<ActorData, int> incomingKnockbacks)
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in m_actorToHitResults)
            {
                ActorData key = actorToHitResult.Key;
                ClientActorHitResults clientActorHitResults = actorToHitResult.Value;
                if (clientActorHitResults.HasKnockback)
                {
                    if (!incomingKnockbacks.ContainsKey(key))
                    {
                        incomingKnockbacks.Add(key, 1);
                    }
                    else
                    {
                        Dictionary<ActorData, int> dictionary;
                        ActorData index;
                        (dictionary = incomingKnockbacks)[index = key] = dictionary[index] + 1;
                    }

                    if (clientActorHitResults.KnockbackSourceActor != null)
                    {
                        if (!outgoingKnockbacks.ContainsKey(clientActorHitResults.KnockbackSourceActor))
                        {
                            outgoingKnockbacks.Add(clientActorHitResults.KnockbackSourceActor, 1);
                        }
                        else
                        {
                            Dictionary<ActorData, int> dictionary;
                            ActorData knockbackSourceActor;
                            (dictionary = outgoingKnockbacks)[
                                    knockbackSourceActor = clientActorHitResults.KnockbackSourceActor] =
                                dictionary[knockbackSourceActor] + 1;
                        }
                    }
                }
            }
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

        public bool HasReactionByCaster(ActorData caster)
        {
            return ClientResolutionAction.HasReactionHitByCaster(caster, m_actorToHitResults);
        }

        public void GetReactionHitResultsByCaster(
            ActorData caster,
            out Dictionary<ActorData, ClientActorHitResults> reactionActorHits,
            out Dictionary<Vector3, ClientPositionHitResults> reactionPosHits)
        {
            ClientResolutionAction.GetReactionHitResultsByCaster(caster, m_actorToHitResults, out reactionActorHits,
                out reactionPosHits);
        }

        public Dictionary<ActorData, ClientActorHitResults> GetActorHitResults()
        {
            return m_actorToHitResults;
        }

        public Dictionary<Vector3, ClientPositionHitResults> GetPosHitResults()
        {
            return m_posToHitResults;
        }

        public void MarkActorHitsAsMovementHits()
        {
            foreach (var clientActorHitResults in m_actorToHitResults.Values)
                clientActorHitResults.IsMovementHit = true;
        }

        public string GetDebugDescription()
        {
            return m_effectCaster.method_95() + "'s effect, guid = " + m_effectGUID;
        }

        internal string UnexecutedHitsDebugStr()
        {
            return ClientResolutionAction.AssembleUnexecutedHitsDebugStr(m_actorToHitResults, m_posToHitResults);
        }
    }
}
