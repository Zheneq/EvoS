using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Game;
using EvoS.Framework.Network.NetworkBehaviours;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientBarrierResults
    {
        private int m_barrierGUID;
        private ActorData m_barrierCaster;
        private Dictionary<ActorData, ClientActorHitResults> m_actorToHitResults;
        private Dictionary<Vector3, ClientPositionHitResults> m_posToHitResults;

        public ClientBarrierResults(
            int barrierGUID,
            ActorData barrierCaster,
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> posToHitResults)
        {
            m_barrierGUID = barrierGUID;
            m_barrierCaster = barrierCaster;
            m_actorToHitResults = actorToHitResults;
            m_posToHitResults = posToHitResults;
        }

        public void RunClientBarrierHits()
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in m_actorToHitResults)
                OnBarrierHitActor(actorToHitResult.Key);
            foreach (KeyValuePair<Vector3, ClientPositionHitResults> posToHitResult in m_posToHitResults)
                OnBarrierHitPosition(posToHitResult.Key);
        }

        internal void OnBarrierHitActor(ActorData target)
        {
            if (m_actorToHitResults.ContainsKey(target))
                m_actorToHitResults[target].ExecuteActorHit(target, m_barrierCaster);
            else
                Log.Print(LogType.Error,
                    $"ClientBarrierResults error-- Sequence hitting actor {target.method_95()}, but that actor isn't in our hit results.");
        }

        internal void OnBarrierHitPosition(Vector3 position)
        {
            if (!m_posToHitResults.ContainsKey(position))
                return;
            m_posToHitResults[position].ExecutePositionHit();
        }

        internal bool DoneHitting()
        {
            return ClientResolutionAction.DoneHitting(m_actorToHitResults, m_posToHitResults);
        }

        internal bool HasUnexecutedHitOnActor(ActorData actor)
        {
            return ClientResolutionAction.HasUnexecutedHitOnActor(actor, m_actorToHitResults);
        }

        internal void ExecuteUnexecutedClientHits()
        {
            ClientResolutionAction.ExecuteUnexecutedHits(m_actorToHitResults, m_posToHitResults, m_barrierCaster);
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
            foreach (ClientActorHitResults clientActorHitResults in m_actorToHitResults.Values)
                clientActorHitResults.IsMovementHit = true;
        }

        public string GetDebugDescription()
        {
            return $"{m_barrierCaster.method_95()}'s barrier, guid = {m_barrierGUID}";
        }

        internal string UnexecutedHitsDebugStr()
        {
            return ClientResolutionAction.AssembleUnexecutedHitsDebugStr(m_actorToHitResults, m_posToHitResults);
        }
    }
}
