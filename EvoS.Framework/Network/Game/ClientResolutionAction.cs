using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Game.Resolution;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game
{
    public class ClientResolutionAction : IComparable
    {
        private ResolutionActionType m_type;
        private ClientAbilityResults m_abilityResults;
        private ClientEffectResults m_effectResults;
        private ClientMovementResults m_moveResults;

        public ClientResolutionAction()
        {
            m_type = ResolutionActionType.Invalid;
            m_abilityResults = null;
            m_effectResults = null;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            ClientResolutionAction resolutionAction = obj as ClientResolutionAction;
            if (resolutionAction == null)
                throw new ArgumentException("Object is not a ClientResolutionAction");
            if (ReactsToMovement() != resolutionAction.ReactsToMovement())
                return ReactsToMovement().CompareTo(resolutionAction.ReactsToMovement());
            if (!ReactsToMovement() && !resolutionAction.ReactsToMovement())
                return 0;
            float moveCost1 = m_moveResults.m_triggeringPath.moveCost;
            float moveCost2 = resolutionAction.m_moveResults.m_triggeringPath.moveCost;
            if (moveCost1 != (double) moveCost2)
                return moveCost1.CompareTo(moveCost2);
            bool flag1 = m_moveResults.HasBarrierHitResults();
            bool flag2 = resolutionAction.m_moveResults.HasBarrierHitResults();
            if (flag1 && !flag2)
                return -1;
            if (!flag1 && flag2)
                return 1;
            bool flag3 = m_moveResults.HasGameModeHitResults();
            bool flag4 = resolutionAction.m_moveResults.HasGameModeHitResults();
            if (flag3 && !flag4)
                return -1;
            return !flag3 && flag4 ? 1 : 0;
        }

        public static ClientResolutionAction ClientResolutionAction_DeSerializeFromStream(Component context, ref IBitStream stream)
        {
            ClientResolutionAction resolutionAction = new ClientResolutionAction();
            sbyte num = -1;
            stream.Serialize(ref num);
            ResolutionActionType resolutionActionType = (ResolutionActionType) num;
            resolutionAction.m_type = (ResolutionActionType) num;
            switch (resolutionActionType)
            {
                case ResolutionActionType.AbilityCast:
                    resolutionAction.m_abilityResults =
                        AbilityResultsUtils.DeSerializeClientAbilityResultsFromStream(context, ref stream);
                    break;
                case ResolutionActionType.EffectAnimation:
                case ResolutionActionType.EffectPulse:
                    resolutionAction.m_effectResults =
                        AbilityResultsUtils.DeSerializeClientEffectResultsFromStream(context, ref stream);
                    break;
                case ResolutionActionType.EffectOnMove:
                case ResolutionActionType.BarrierOnMove:
                case ResolutionActionType.PowerupOnMove:
                case ResolutionActionType.GameModeOnMove:
                    resolutionAction.m_moveResults =
                        AbilityResultsUtils.DeSerializeClientMovementResultsFromStream(context, ref stream);
                    break;
            }

            return resolutionAction;
        }

        public ActorData GetCaster()
        {
            if (m_abilityResults != null)
                return m_abilityResults.GetCaster();
            if (m_effectResults != null)
                return m_effectResults.GetCaster();
            return null;
        }

        public AbilityData.ActionType GetSourceAbilityActionType()
        {
            if (m_abilityResults != null)
                return m_abilityResults.GetSourceActionType();
            if (m_effectResults != null)
                return m_effectResults.GetSourceActionType();
            return AbilityData.ActionType.INVALID_ACTION;
        }

        public bool IsResolutionActionType(ResolutionActionType testType)
        {
            return m_type == testType;
        }

        public bool HasReactionHitByCaster(ActorData caster)
        {
            if (m_abilityResults != null)
                return m_abilityResults.HasReactionByCaster(caster);
            if (m_effectResults != null)
                return m_effectResults.HasReactionByCaster(caster);
            return false;
        }

        public void GetHitResults(
            out Dictionary<ActorData, ClientActorHitResults> actorHitResList,
            out Dictionary<Vector3, ClientPositionHitResults> posHitResList)
        {
            actorHitResList = null;
            posHitResList = null;
            if (m_abilityResults != null)
            {
                actorHitResList = m_abilityResults.GetActorHitResults();
                posHitResList = m_abilityResults.GetPosHitResults();
            }
            else
            {
                if (m_effectResults == null)
                    return;
                actorHitResList = m_effectResults.GetActorHitResults();
                posHitResList = m_effectResults.GetPosHitResults();
            }
        }

        public void GetReactionHitResultsByCaster(
            ActorData caster,
            out Dictionary<ActorData, ClientActorHitResults> actorHitResList,
            out Dictionary<Vector3, ClientPositionHitResults> posHitResList)
        {
            actorHitResList = null;
            posHitResList = null;
            if (m_abilityResults != null)
            {
                m_abilityResults.GetReactionHitResultsByCaster(caster, out actorHitResList, out posHitResList);
            }
            else
            {
                if (m_effectResults == null)
                    return;
                m_effectResults.GetReactionHitResultsByCaster(caster, out actorHitResList, out posHitResList);
            }
        }

        public void RunStartSequences(Component context)
        {
            m_abilityResults?.StartSequences();
            m_effectResults?.StartSequences(context);
        }

        public void Run_OutsideResolution(Component context)
        {
            m_abilityResults?.StartSequences();
            m_effectResults?.StartSequences(context);
            m_moveResults?.ReactToMovement(context);
        }

        public bool CompletedAction()
        {
            bool flag;
            if (m_type == ResolutionActionType.AbilityCast)
                flag = m_abilityResults.DoneHitting();
            else if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
            {
                if (m_type != ResolutionActionType.EffectOnMove && m_type != ResolutionActionType.BarrierOnMove &&
                    (m_type != ResolutionActionType.PowerupOnMove && m_type != ResolutionActionType.GameModeOnMove))
                {
                    Log.Print(LogType.Error,
                        $"ClientResolutionAction has unknown type: {m_type}.  Assuming it's complete...");
                    flag = true;
                }
                else
                    flag = m_moveResults.DoneHitting();
            }
            else
                flag = m_effectResults.DoneHitting();

            return flag;
        }

        public void ExecuteUnexecutedClientHitsInAction()
        {
            if (m_type == ResolutionActionType.AbilityCast)
                m_abilityResults.ExecuteUnexecutedClientHits();
            else if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
            {
                if (m_type != ResolutionActionType.EffectOnMove && m_type != ResolutionActionType.BarrierOnMove &&
                    (m_type != ResolutionActionType.PowerupOnMove && m_type != ResolutionActionType.GameModeOnMove))
                    return;
                m_moveResults.ExecuteUnexecutedClientHits();
            }
            else
                m_effectResults.ExecuteUnexecutedClientHits();
        }

        public bool HasUnexecutedHitOnActor(ActorData actor)
        {
            bool flag = false;
            if (m_type == ResolutionActionType.AbilityCast)
                flag = m_abilityResults.HasUnexecutedHitOnActor(actor);
            else if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
            {
                if (m_type == ResolutionActionType.EffectOnMove || m_type == ResolutionActionType.BarrierOnMove ||
                    (m_type == ResolutionActionType.PowerupOnMove || m_type == ResolutionActionType.GameModeOnMove))
                    flag = m_moveResults.HasUnexecutedHitOnActor(actor);
            }
            else
                flag = m_effectResults.HasUnexecutedHitOnActor(actor);

            return flag;
        }

        public void ExecuteReactionHitsWithExtraFlagsOnActor(
            ActorData targetActor,
            ActorData caster,
            bool hasDamage,
            bool hasHealing)
        {
            if (m_type == ResolutionActionType.AbilityCast)
                m_abilityResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            else if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
            {
                if (m_type != ResolutionActionType.EffectOnMove && m_type != ResolutionActionType.BarrierOnMove &&
                    (m_type != ResolutionActionType.PowerupOnMove && m_type != ResolutionActionType.GameModeOnMove))
                    return;
                m_moveResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
            }
            else
                m_effectResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage, hasHealing);
        }

        public static bool DoneHitting(
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> positionHitResults)
        {
            bool flag1 = true;
            bool flag2 = true;
            foreach (ClientActorHitResults clientActorHitResults in actorToHitResults.Values)
            {
                if (!clientActorHitResults.ExecutedHit || clientActorHitResults.HasUnexecutedReactionHits())
                {
                    flag1 = false;
                    break;
                }
            }

            foreach (ClientPositionHitResults positionHitResults1 in positionHitResults.Values)
            {
                if (!positionHitResults1.ExecutedHit)
                {
                    flag2 = false;
                    break;
                }
            }

            if (flag1)
                return flag2;
            return false;
        }

        public static bool HasUnexecutedHitOnActor(
            ActorData targetActor,
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults)
        {
            bool flag = false;
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in actorToHitResults)
            {
                ClientActorHitResults clientActorHitResults = actorToHitResult.Value;
                if (!clientActorHitResults.ExecutedHit && actorToHitResult.Key.ActorIndex == targetActor.ActorIndex ||
                    clientActorHitResults.HasUnexecutedReactionOnActor(targetActor))
                {
                    flag = true;
                    break;
                }
            }

            return flag;
        }

        public static void ExecuteUnexecutedHits(
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> positionHitResults,
            ActorData caster)
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in actorToHitResults)
            {
                if (!actorToHitResult.Value.ExecutedHit)
                {
                    ActorData key = actorToHitResult.Key;
                    if (ClientAbilityResults.Boolean_0)
                        Log.Print(LogType.Warning,
                            $"{ClientAbilityResults.s_clientHitResultHeader}Executing unexecuted actor hit on {key.method_95()} from {caster.method_95()}");
                    actorToHitResult.Value.ExecuteActorHit(actorToHitResult.Key, caster);
                }
            }

            foreach (KeyValuePair<Vector3, ClientPositionHitResults> positionHitResult in positionHitResults)
            {
                if (!positionHitResult.Value.ExecutedHit)
                {
                    if (ClientAbilityResults.Boolean_0)
                        Log.Print(LogType.Warning,
                            $"{ClientAbilityResults.s_clientHitResultHeader}Executing unexecuted position hit on {positionHitResult.Key} from {caster.method_95()}");
                    positionHitResult.Value.ExecutePositionHit();
                }
            }
        }

        public static void ExecuteReactionHitsWithExtraFlagsOnActorAux(
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            ActorData targetActor,
            ActorData caster,
            bool hasDamage,
            bool hasHealing)
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in actorToHitResults)
            {
                ActorData key = actorToHitResult.Key;
                ClientActorHitResults clientActorHitResults = actorToHitResult.Value;
                if (!clientActorHitResults.ExecutedHit && key == targetActor)
                    clientActorHitResults.ExecuteReactionHitsWithExtraFlagsOnActor(targetActor, caster, hasDamage,
                        hasHealing);
            }
        }

        public static bool HasReactionHitByCaster(
            ActorData caster,
            Dictionary<ActorData, ClientActorHitResults> actorHitResults)
        {
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorHitResult in actorHitResults)
            {
                if (actorHitResult.Value.HasReactionHitByCaster(caster))
                    return true;
            }

            return false;
        }

        public static void GetReactionHitResultsByCaster(
            ActorData caster,
            Dictionary<ActorData, ClientActorHitResults> actorHitResults,
            out Dictionary<ActorData, ClientActorHitResults> reactionActorHits,
            out Dictionary<Vector3, ClientPositionHitResults> reactionPosHits)
        {
            reactionActorHits = null;
            reactionPosHits = null;
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorHitResult in actorHitResults)
            {
                if (actorHitResult.Value.HasReactionHitByCaster(caster))
                {
                    actorHitResult.Value.GetReactionHitResultsByCaster(caster, out reactionActorHits,
                        out reactionPosHits);
                    break;
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
            bool flag;
            if (m_type == ResolutionActionType.AbilityCast)
                flag = m_abilityResults.ContainsSequenceSourceID(id);
            else if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
            {
                if (m_type != ResolutionActionType.EffectOnMove && m_type != ResolutionActionType.BarrierOnMove &&
                    (m_type != ResolutionActionType.PowerupOnMove && m_type != ResolutionActionType.GameModeOnMove))
                {
                    Log.Print(LogType.Error,
                        $"ClientResolutionAction has unknown type: {(int) m_type}.  Assuming it does not have a given SequenceSource...");
                    flag = false;
                }
                else
                    flag = m_moveResults.ContainsSequenceSourceID(id);
            }
            else
                flag = m_effectResults.ContainsSequenceSourceID(id);

            return flag;
        }

        public bool ReactsToMovement()
        {
            if (m_type != ResolutionActionType.EffectOnMove && m_type != ResolutionActionType.BarrierOnMove &&
                m_type != ResolutionActionType.PowerupOnMove)
                return m_type == ResolutionActionType.GameModeOnMove;
            return true;
        }

        public ActorData GetTriggeringMovementActor()
        {
            return m_moveResults != null ? m_moveResults.m_triggeringMover : null;
        }

        public void OnActorMoved_ClientResolutionAction(ActorData mover, BoardSquarePathInfo curPath)
        {
            if (!m_moveResults.TriggerMatchesMovement(mover, curPath))
                return;
            m_moveResults.ReactToMovement(mover);
        }

        public void AdjustKnockbackCounts_ClientResolutionAction(
            ref Dictionary<ActorData, int> outgoingKnockbacks,
            ref Dictionary<ActorData, int> incomingKnockbacks)
        {
            if (m_type == ResolutionActionType.AbilityCast)
            {
                m_abilityResults.AdjustKnockbackCounts_ClientAbilityResults(ref outgoingKnockbacks,
                    ref incomingKnockbacks);
            }
            else
            {
                if (m_type != ResolutionActionType.EffectAnimation && m_type != ResolutionActionType.EffectPulse)
                    return;
                m_effectResults.AdjustKnockbackCounts_ClientEffectResults(ref outgoingKnockbacks,
                    ref incomingKnockbacks);
            }
        }

        public string GetDebugDescription()
        {
            string str = m_type + ": ";
            return m_type != ResolutionActionType.AbilityCast
                ? (m_type == ResolutionActionType.EffectAnimation || m_type == ResolutionActionType.EffectPulse
                    ? str + m_effectResults.GetDebugDescription()
                    : (m_type == ResolutionActionType.EffectOnMove || m_type == ResolutionActionType.BarrierOnMove ||
                       (m_type == ResolutionActionType.PowerupOnMove || m_type == ResolutionActionType.GameModeOnMove)
                        ? str + m_moveResults.GetDebugDescription()
                        : str + "??? (invalid results)"))
                : str + m_abilityResults.GetDebugDescription();
        }

        public string GetUnexecutedHitsDebugStr(bool logSequenceDataActors = false)
        {
            string str;
            if (m_type == ResolutionActionType.AbilityCast)
            {
                str = m_abilityResults.UnexecutedHitsDebugStr();
                if (logSequenceDataActors)
                    str = str + "\n" + m_abilityResults.GetSequenceStartDataDebugStr() + "\n";
            }
            else
                str = m_type == ResolutionActionType.EffectAnimation || m_type == ResolutionActionType.EffectPulse
                    ? m_effectResults.UnexecutedHitsDebugStr()
                    : (m_type == ResolutionActionType.EffectOnMove || m_type == ResolutionActionType.BarrierOnMove ||
                       (m_type == ResolutionActionType.PowerupOnMove || m_type == ResolutionActionType.GameModeOnMove)
                        ? m_moveResults.UnexecutedHitsDebugStr()
                        : string.Empty);

            return str;
        }

        public static string AssembleUnexecutedHitsDebugStr(
            Dictionary<ActorData, ClientActorHitResults> actorToHitResults,
            Dictionary<Vector3, ClientPositionHitResults> positionToHitResults)
        {
            int num1 = 0;
            string str1 = string.Empty;
            int num2 = 0;
            string str2 = string.Empty;
            foreach (KeyValuePair<ActorData, ClientActorHitResults> actorToHitResult in actorToHitResults)
            {
                ActorData key = actorToHitResult.Key;
                if (!actorToHitResult.Value.ExecutedHit)
                {
                    ++num1;
                    str1 = str1 + "\n\t\t" + num1 + ". ActorHit on " + key.method_95();
                }
                else
                {
                    ++num2;
                    str2 = str2 + "\n\t\t" + num2 + ". ActorHit on " + key.method_95();
                }
            }

            foreach (KeyValuePair<Vector3, ClientPositionHitResults> positionToHitResult in positionToHitResults)
            {
                Vector3 key = positionToHitResult.Key;
                if (!positionToHitResult.Value.ExecutedHit)
                {
                    ++num1;
                    str1 = str1 + "\n\t\t" + num1 + ". PositionHit on " + key;
                }
                else
                {
                    ++num2;
                    str2 = str2 + "\n\t\t" + num2 + ". PositionHit on " + key;
                }
            }

            string str3 = "\n\tUnexecuted hits: " + num1 + str1;
            if (num2 > 0)
                str3 = str3 + "\n\tExecuted hits: " + num2 + str2;
            return str3 + "\n";
        }
    }
}
