using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Game;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientAbilityResults
    {
        public static string s_storeActorHitHeader = "<color=cyan>Storing ClientActorHitResult: </color>";
        public static string s_storePositionHitHeader = "<color=cyan>Storing ClientPositionHitResult: </color>";
        public static string s_executeActorHitHeader = "<color=green>Executing ClientActorHitResult: </color>";
        public static string s_executePositionHitHeader = "<color=green>Executing ClientPositionHitResults: </color>";
        public static string s_clientResolutionNetMsgHeader = "<color=white>ClientResolution NetworkMessage: </color>";
        public static string s_clientHitResultHeader = "<color=yellow>ClientHitResults: </color>";
        private ActorData m_casterActor;
        private Ability m_castedAbility;
        private AbilityData.ActionType m_actionType;
        private List<ServerClientUtils.SequenceStartData> m_seqStartDataList;
        private Dictionary<ActorData, ClientActorHitResults> m_actorToHitResults;
        private Dictionary<Vector3, ClientPositionHitResults> m_posToHitResults;

        public ClientAbilityResults(
            Component context,
            int casterActorIndex,
            int abilityAction,
            List<ServerClientUtils.SequenceStartData> seqStartDataList,
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> posToHitResults)
        {
            m_casterActor = context.GameFlowData.FindActorByActorIndex(casterActorIndex);
            if (m_casterActor == null)
            {
                Log.Print(LogType.Error, $"ClientAbilityResults error: Actor with index {casterActorIndex} is null.");
                m_castedAbility = null;
                m_actionType = AbilityData.ActionType.INVALID_ACTION;
            }
            else
            {
                var type = (AbilityData.ActionType) abilityAction;
                m_castedAbility = m_casterActor.method_8().GetAbilityOfActionType(type);
                m_actionType = type;
            }

            m_seqStartDataList = seqStartDataList;
            m_actorToHitResults = actorToHitResults;
            m_posToHitResults = posToHitResults;
        }

        public ActorData GetCaster()
        {
            return m_casterActor;
        }

        public AbilityData.ActionType GetSourceActionType()
        {
            return m_actionType;
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

        public void StartSequences()
        {
            if (HasSequencesToStart())
            {
                foreach (var seqStartData in m_seqStartDataList)
                    seqStartData.CreateSequencesFromData(m_casterActor, OnAbilityHitActor, OnAbilityHitPosition);
            }
            else
            {
                if (Boolean_0)
                    Log.Print(LogType.Warning,
                        s_clientHitResultHeader + GetDebugDescription() +
                        ": no Sequence to start, executing results directly");
                RunClientAbilityHits();
            }
        }

        public void RunClientAbilityHits()
        {
            foreach (var actorToHitResult in m_actorToHitResults)
                OnAbilityHitActor(actorToHitResult.Key);
            foreach (var posToHitResult in m_posToHitResults)
                OnAbilityHitPosition(posToHitResult.Key);
        }

        internal void OnAbilityHitActor(ActorData target)
        {
            if (m_actorToHitResults.ContainsKey(target))
                m_actorToHitResults[target].ExecuteActorHit(target, m_casterActor);
            else
                Log.Print(LogType.Error,
                    $"ClientAbilityResults error-- Sequence hitting actor {target.method_95()}, but that actor isn't in our hit results.");
        }

        internal void OnAbilityHitPosition(Vector3 position)
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
            ClientResolutionAction.ExecuteUnexecutedHits(m_actorToHitResults, m_posToHitResults, m_casterActor);
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

        public void MarkActorHitsAsMovementHits()
        {
            foreach (var clientActorHitResults in m_actorToHitResults.Values)
                clientActorHitResults.IsMovementHit = true;
        }

        internal string UnexecutedHitsDebugStr()
        {
            return ClientResolutionAction.AssembleUnexecutedHitsDebugStr(m_actorToHitResults, m_posToHitResults);
        }

        internal string GetSequenceStartDataDebugStr()
        {
            string str = string.Empty;
            if (m_seqStartDataList != null)
            {
                foreach (var seqStartData in m_seqStartDataList)
                {
                    if (seqStartData != null)
                        str =
                            $"{str}SeqStartData Actors with prefab ID {seqStartData.GetSequencePrefabId()}: {seqStartData.GetTargetActorsString(m_casterActor)}\n";
                }
            }

            return str;
        }

        public void AdjustKnockbackCounts_ClientAbilityResults(
            ref Dictionary<ActorData, int> outgoingKnockbacks,
            ref Dictionary<ActorData, int> incomingKnockbacks)
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in m_actorToHitResults)
            {
                var key = actorToHitResult.Key;
                var clientActorHitResults = actorToHitResult.Value;
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

        public string GetDebugDescription()
        {
            return
                $"{(m_casterActor == null ? "(null actor)" : m_casterActor.method_95())}'s {(m_castedAbility == null ? "(null ability)" : m_castedAbility.m_abilityName)}";
        }

        public static bool Boolean_0 => false;

        public static bool Boolean_1 => false;
    }
}
