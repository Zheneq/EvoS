using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public class ClientActorHitResults
    {
        private bool m_hasDamage;
        private bool m_hasHealing;
        private bool m_hasTechPointGain;
        private bool m_hasTechPointLoss;
        private bool m_hasTechPointGainOnCaster;
        private bool m_hasKnockback;
        private ActorData m_knockbackSourceActor;
        private int m_finalDamage;
        private int m_finalHealing;
        private int m_finalTechPointsGain;
        private int m_finalTechPointsLoss;
        private int m_finalTechPointGainOnCaster;
        private bool m_damageBoosted;
        private bool m_damageReduced;
        private bool m_isPartOfHealOverTime;
        private bool m_updateCasterLastKnownPos;
        private bool m_updateTargetLastKnownPos;
        private bool m_triggerCasterVisOnHitVisualOnly;
        private bool m_updateEffectHolderLastKnownPos;
        private ActorData m_effectHolderActor;
        private bool m_updateOtherLastKnownPos;
        private List<ActorData> m_otherActorsToUpdateVisibility;
        private bool m_targetInCoverWrtDamage;
        private Vector3 m_damageHitOrigin;
        private bool m_canBeReactedTo;
        private bool m_isCharacterSpecificAbility;
        private List<ClientEffectStartData> m_effectsToStart;
        private List<int> m_effectsToRemove;
        private List<ClientBarrierStartData> m_barriersToAdd;
        private List<int> m_barriersToRemove;
        private List<ServerClientUtils.SequenceEndData> m_sequencesToEnd;
        private List<ClientReactionResults> m_reactions;
        private List<int> m_powerupsToRemove;
        private List<ClientPowerupStealData> m_powerupsToSteal;
        private List<ClientMovementResults> m_directPowerupHits;
        private List<ClientGameModeEvent> m_gameModeEvents;
        private List<int> m_overconIds;

        public ClientActorHitResults(Component context, ref IBitStream stream)
        {
            byte bitField1 = 0;
            stream.Serialize(ref bitField1);
            ServerClientUtils.GetBoolsFromBitfield(bitField1, out m_hasDamage, out m_hasHealing, out m_hasTechPointGain,
                out m_hasTechPointLoss, out m_hasTechPointGainOnCaster, out m_hasKnockback,
                out m_targetInCoverWrtDamage, out m_canBeReactedTo);
            byte bitField2 = 0;
            stream.Serialize(ref bitField2);
            ServerClientUtils.GetBoolsFromBitfield(bitField2, out m_damageBoosted, out m_damageReduced,
                out m_updateCasterLastKnownPos, out m_updateTargetLastKnownPos, out m_updateEffectHolderLastKnownPos,
                out m_updateOtherLastKnownPos, out m_isPartOfHealOverTime, out m_triggerCasterVisOnHitVisualOnly);
            short num1 = 0;
            short num2 = 0;
            short num3 = 0;
            short num4 = 0;
            short num5 = 0;
            if (m_hasDamage)
            {
                stream.Serialize(ref num1);
                m_finalDamage = num1;
            }

            if (m_hasHealing)
            {
                stream.Serialize(ref num2);
                m_finalHealing = num2;
            }

            if (m_hasTechPointGain)
            {
                stream.Serialize(ref num3);
                m_finalTechPointsGain = num3;
            }

            if (m_hasTechPointLoss)
            {
                stream.Serialize(ref num4);
                m_finalTechPointsLoss = num4;
            }

            if (m_hasTechPointGainOnCaster)
            {
                stream.Serialize(ref num5);
                m_finalTechPointGainOnCaster = num5;
            }

            if (m_hasKnockback)
            {
                short invalidActorIndex = (short) ActorData.s_invalidActorIndex;
                stream.Serialize(ref invalidActorIndex);
                m_knockbackSourceActor = (int) invalidActorIndex == ActorData.s_invalidActorIndex
                    ? null
                    : context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
            }

            if ((!m_hasDamage || !m_targetInCoverWrtDamage ? (m_hasKnockback ? 1 : 0) : 1) != 0)
            {
                float num6 = 0.0f;
                float num7 = 0.0f;
                stream.Serialize(ref num6);
                stream.Serialize(ref num7);
                m_damageHitOrigin.X = num6;
                m_damageHitOrigin.Y = 0.0f;
                m_damageHitOrigin.Z = num7;
            }

            if (m_updateEffectHolderLastKnownPos)
            {
                short invalidActorIndex = (short) ActorData.s_invalidActorIndex;
                stream.Serialize(ref invalidActorIndex);
                m_effectHolderActor = (int) invalidActorIndex == ActorData.s_invalidActorIndex
                    ? null
                    : context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
            }

            if (m_updateOtherLastKnownPos)
            {
                byte num6 = 0;
                stream.Serialize(ref num6);
                m_otherActorsToUpdateVisibility = new List<ActorData>(num6);
                for (int index = 0; index < (int) num6; ++index)
                {
                    short invalidActorIndex = (short) ActorData.s_invalidActorIndex;
                    stream.Serialize(ref invalidActorIndex);
                    if (invalidActorIndex != ActorData.s_invalidActorIndex)
                    {
                        ActorData actorByActorIndex = context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
                        if (actorByActorIndex != null)
                            m_otherActorsToUpdateVisibility.Add(actorByActorIndex);
                    }
                }
            }

            bool out0_1 = false;
            bool out1_1 = false;
            bool out2_1 = false;
            bool out2_2 = false;
            bool out3_1 = false;
            bool out4 = false;
            bool out5 = false;
            bool out6 = false;
            bool out7 = false;
            bool out0_2 = false;
            bool out1_2 = false;
            bool out3_2 = false;
            byte bitField3 = 0;
            byte bitField4 = 0;
            stream.Serialize(ref bitField3);
            stream.Serialize(ref bitField4);
            ServerClientUtils.GetBoolsFromBitfield(bitField3, out out0_1, out out1_1, out out2_2, out out3_1, out out4,
                out out5, out out6, out out7);
            ServerClientUtils.GetBoolsFromBitfield(bitField4, out out0_2, out out1_2, out out2_1, out out3_2);
            m_effectsToStart = !out0_1
                ? new List<ClientEffectStartData>()
                : AbilityResultsUtils.DeSerializeEffectsToStartFromStream(context, ref stream);
            m_effectsToRemove = !out1_1
                ? new List<int>()
                : AbilityResultsUtils.DeSerializeEffectsForRemovalFromStream(ref stream);
            m_barriersToAdd = !out2_1
                ? new List<ClientBarrierStartData>()
                : AbilityResultsUtils.DeSerializeBarriersToStartFromStream(context, ref stream);
            m_barriersToRemove = !out2_2
                ? new List<int>()
                : AbilityResultsUtils.DeSerializeBarriersForRemovalFromStream(ref stream);
            m_sequencesToEnd = !out3_1
                ? new List<ServerClientUtils.SequenceEndData>()
                : AbilityResultsUtils.DeSerializeSequenceEndDataListFromStream(ref stream);
            m_reactions = !out4
                ? new List<ClientReactionResults>()
                : AbilityResultsUtils.DeSerializeClientReactionResultsFromStream(context, ref stream);
            m_powerupsToRemove =
                !out5 ? new List<int>() : AbilityResultsUtils.DeSerializePowerupsToRemoveFromStream(ref stream);
            m_powerupsToSteal =
                !out6
                    ? new List<ClientPowerupStealData>()
                    : AbilityResultsUtils.DeSerializePowerupsToStealFromStream(context, ref stream);
            m_directPowerupHits =
                !out7
                    ? new List<ClientMovementResults>()
                    : AbilityResultsUtils.DeSerializeClientMovementResultsListFromStream(context, ref stream);
            m_gameModeEvents = !out0_2
                ? new List<ClientGameModeEvent>()
                : AbilityResultsUtils.DeSerializeClientGameModeEventListFromStream(context, ref stream);
            m_overconIds = !out3_2
                ? new List<int>()
                : AbilityResultsUtils.DeSerializeClientOverconListFromStream(ref stream);
            m_isCharacterSpecificAbility = out1_2;
            IsMovementHit = false;
            ExecutedHit = false;
        }

        public bool ExecutedHit { get; private set; }

        public bool IsMovementHit { get; set; }

        public bool HasKnockback
        {
            get { return m_hasKnockback; }
            private set { }
        }

        public ActorData KnockbackSourceActor
        {
            get { return m_knockbackSourceActor; }
            private set { }
        }

        public bool HasUnexecutedReactionOnActor(ActorData actor)
        {
            bool flag = false;
            for (int index = 0; index < m_reactions.Count && !flag; ++index)
                flag = m_reactions[index].HasUnexecutedReactionOnActor(actor);
            return flag;
        }

        public bool HasUnexecutedReactionHits()
        {
            bool flag = false;
            for (int index = 0; index < m_reactions.Count && !flag; ++index)
                flag = !m_reactions[index].ReactionHitsDone();
            return flag;
        }

        public bool HasReactionHitByCaster(ActorData caster)
        {
            for (int index = 0; index < m_reactions.Count; ++index)
            {
                if (m_reactions[index].GetCaster() == caster)
                    return true;
            }

            return false;
        }

        public void GetReactionHitResultsByCaster(
            ActorData caster,
            out Dictionary<ActorData, ClientActorHitResults> actorHits,
            out Dictionary<Vector3, ClientPositionHitResults> posHits)
        {
            actorHits = null;
            posHits = null;
            for (int index = 0; index < m_reactions.Count; ++index)
            {
                if (m_reactions[index].GetCaster() == caster)
                {
                    actorHits = m_reactions[index].GetActorHitResults();
                    posHits = m_reactions[index].GetPosHitResults();
                    break;
                }
            }
        }

        public void ExecuteReactionHitsWithExtraFlagsOnActor(
            ActorData targetActor,
            ActorData caster,
            bool hasDamage,
            bool hasHealing)
        {
            for (int index = 0; index < m_reactions.Count; ++index)
            {
                var reaction = m_reactions[index];
                byte extraFlags = reaction.GetExtraFlags();
                if (!reaction.PlayedReaction() &&
                    (((((int) extraFlags & 1) == 0 || !hasDamage
                          ? 0
                          : reaction.HasUnexecutedReactionOnActor(targetActor)
                              ? 1
                              : 0) != 0
                         ? 1
                         : (((int) extraFlags & 2) == 0 || !hasDamage
                             ? 0
                             : reaction.HasUnexecutedReactionOnActor(caster)
                                 ? 1
                                 : 0)) != 0
                        ? 1
                        : (((int) extraFlags & 4) == 0 || !hasDamage
                            ? 0
                            : reaction.GetCaster() == targetActor
                                ? 1
                                : 0)) != 0)
                {
                    if (ClientAbilityResults.Boolean_0)
                        Log.Print(LogType.Warning,
                            $"{ClientAbilityResults.s_clientHitResultHeader}{reaction.GetDebugDescription()} executing reaction hit on first damaging hit");
                    reaction.PlayReaction(targetActor ?? caster);
                }
            }
        }

        public void ExecuteActorHit(ActorData target, ActorData caster)
        {
            throw new NotImplementedException();
//            if (ExecutedHit)
//                return;
//            if (ClientAbilityResults.Boolean_0)
//                Log.Print(LogType.Warning,
//                    $"{ClientAbilityResults.s_executeActorHitHeader} Target: {target.method_95()} Caster: {caster.method_95()}");
//            bool flag = ClientResolutionManager.Get().IsInResolutionState();
//            if (m_triggerCasterVisOnHitVisualOnly && !m_updateCasterLastKnownPos)
//                caster.TriggerVisibilityForHit(IsMovementHit, false);
//            if (m_updateCasterLastKnownPos)
//                caster.TriggerVisibilityForHit(IsMovementHit);
//            if (m_updateTargetLastKnownPos)
//                target.TriggerVisibilityForHit(IsMovementHit);
//            if (m_updateEffectHolderLastKnownPos && m_effectHolderActor != null)
//                m_effectHolderActor.TriggerVisibilityForHit(IsMovementHit);
//            if (m_updateOtherLastKnownPos && m_otherActorsToUpdateVisibility != null)
//            {
//                for (int index = 0; index < m_otherActorsToUpdateVisibility.Count; ++index)
//                    m_otherActorsToUpdateVisibility[index].TriggerVisibilityForHit(IsMovementHit);
//            }
//
//            if (m_hasDamage)
//            {
//                if (flag)
//                {
//                    target.ClientUnresolvedDamage += m_finalDamage;
//                    CaptureTheFlag.OnActorDamaged_Client(target, m_finalDamage);
//                }
//
//                string str = !m_targetInCoverWrtDamage ? "|N" : "|C";
//                BuffIconToDisplay icon = BuffIconToDisplay.None;
//                if (m_damageBoosted)
//                    icon = BuffIconToDisplay.BoostedDamage;
//                else if (m_damageReduced)
//                    icon = BuffIconToDisplay.ReducedDamage;
//                target.AddCombatText(m_finalDamage + str, string.Empty, CombatTextCategory.Damage, icon);
//                if (m_targetInCoverWrtDamage)
//                    target.OnHitWhileInCover(m_damageHitOrigin, caster);
//                if (target.method_3() != null)
//                    target.method_3().Client_RecordDamageFromActor(caster);
//                GameEventManager.Get().FireEvent(GameEventManager.EventType.ActorDamaged_Client,
//                    (GameEventManager.GameEventArgs) new GameEventManager.ActorHitHealthChangeArgs(
//                        GameEventManager.ActorHitHealthChangeArgs.ChangeType.Damage, m_finalDamage, target, caster,
//                        m_isCharacterSpecificAbility));
//            }
//
//            if (m_hasHealing)
//            {
//                if (flag)
//                {
//                    target.ClientUnresolvedHealing += m_finalHealing;
//                    if (m_isPartOfHealOverTime)
//                        target.ClientAppliedHoTThisTurn += m_finalHealing;
//                }
//
//                target.AddCombatText(m_finalHealing.ToString(), string.Empty, CombatTextCategory.Healing,
//                    BuffIconToDisplay.None);
//                if (target.method_3() != null)
//                    target.method_3().Client_RecordHealingFromActor(caster);
//                GameEventManager.Get().FireEvent(GameEventManager.EventType.CharacterHealedOrBuffed,
//                    (GameEventManager.GameEventArgs) new GameEventManager.CharacterHealBuffArgs
//                    {
//                        targetCharacter = target,
//                        casterActor = caster,
//                        healed = true
//                    });
//                GameEventManager.Get().FireEvent(GameEventManager.EventType.ActorHealed_Client,
//                    (GameEventManager.GameEventArgs) new GameEventManager.ActorHitHealthChangeArgs(
//                        GameEventManager.ActorHitHealthChangeArgs.ChangeType.Healing, m_finalHealing, target, caster,
//                        m_isCharacterSpecificAbility));
//            }
//
//            if (m_hasTechPointGain)
//            {
//                if (flag)
//                    target.ClientUnresolvedTechPointGain += m_finalTechPointsGain;
//                target.AddCombatText(m_finalTechPointsGain.ToString(), string.Empty, CombatTextCategory.TP_Recovery,
//                    BuffIconToDisplay.None);
//            }
//
//            if (m_hasTechPointLoss)
//            {
//                if (flag)
//                    target.ClientUnresolvedTechPointLoss += m_finalTechPointsLoss;
//                target.AddCombatText(m_finalTechPointsLoss.ToString(), string.Empty, CombatTextCategory.TP_Damage,
//                    BuffIconToDisplay.None);
//            }
//
//            if (m_hasTechPointGainOnCaster)
//            {
//                if (flag)
//                    caster.ClientUnresolvedTechPointGain += m_finalTechPointGainOnCaster;
//                caster.AddCombatText(m_finalTechPointGainOnCaster.ToString(), string.Empty,
//                    CombatTextCategory.TP_Recovery, BuffIconToDisplay.None);
//            }
//
//            if (m_hasKnockback)
//            {
//                ClientKnockbackManager.Get().OnKnockbackHit(m_knockbackSourceActor, target);
//                if (caster != target && target.method_11() != null && target.method_11().IsKnockbackImmune(true))
//                    target.OnKnockbackWhileUnstoppable(m_damageHitOrigin, caster);
//            }
//
//            int amount = 0;
//            foreach (ClientEffectStartData effectData in m_effectsToStart)
//            {
//                amount += effectData.m_absorb;
//                ClientEffectBarrierManager.Get().ExecuteEffectStart(effectData);
//            }
//
//            if (amount > 0)
//            {
//                target.AddCombatText(amount.ToString(), string.Empty, CombatTextCategory.Absorb,
//                    BuffIconToDisplay.None);
//                GameEventManager.Get().FireEvent(GameEventManager.EventType.ActorGainedAbsorb_Client,
//                    (GameEventManager.GameEventArgs) new GameEventManager.ActorHitHealthChangeArgs(
//                        GameEventManager.ActorHitHealthChangeArgs.ChangeType.Absorb, amount, target, caster,
//                        m_isCharacterSpecificAbility));
//            }
//
//            foreach (int effectGuid in m_effectsToRemove)
//                ClientEffectBarrierManager.Get().EndEffect(effectGuid);
//            foreach (ClientBarrierStartData barrierData in m_barriersToAdd)
//                ClientEffectBarrierManager.Get().ExecuteBarrierStart(barrierData);
//            foreach (int barrierGuid in m_barriersToRemove)
//                ClientEffectBarrierManager.Get().EndBarrier(barrierGuid);
//            foreach (ServerClientUtils.SequenceEndData sequenceEndData in m_sequencesToEnd)
//                sequenceEndData.EndClientSequences();
//            foreach (ClientReactionResults reaction in m_reactions)
//                reaction.PlayReaction();
//            foreach (int guid in m_powerupsToRemove)
//            {
//                PowerUp powerUpOfGuid = PowerUpManager.Get().GetPowerUpOfGuid(guid);
//                if (powerUpOfGuid != null)
//                    powerUpOfGuid.Client_OnPickedUp(target.ActorIndex);
//            }
//
//            foreach (ClientPowerupStealData powerupStealData in m_powerupsToSteal)
//            {
//                powerupStealData.m_powerupResults.RunResults();
//                PowerUp powerUpOfGuid = PowerUpManager.Get().GetPowerUpOfGuid(powerupStealData.m_powerupGuid);
//                if (powerUpOfGuid != null)
//                    powerUpOfGuid.Client_OnSteal(target.ActorIndex);
//            }
//
//            foreach (ClientMovementResults directPowerupHit in m_directPowerupHits)
//                directPowerupHit.ReactToMovement();
//            foreach (ClientGameModeEvent gameModeEvent in m_gameModeEvents)
//                gameModeEvent.ExecuteClientGameModeEvent();
//            foreach (int overconId in m_overconIds)
//            {
//                if (UIOverconData.Get() != null)
//                    UIOverconData.Get().UseOvercon(overconId, caster.ActorIndex, true);
//            }
//
//            ExecutedHit = true;
//            ClientResolutionManager.Get().UpdateLastEventTime();
//            ClientResolutionManager.Get()
//                .OnHitExecutedOnActor(target, caster, m_hasDamage, m_hasHealing, m_canBeReactedTo);
        }

        public void ShowDamage(ActorData target)
        {
            string empty = string.Empty;
            target.ShowDamage(empty);
        }

        public int GetNumEffectsToStart()
        {
            if (m_effectsToStart != null)
                return m_effectsToStart.Count;
            return 0;
        }

        public void SwapEffectsToStart(ClientActorHitResults other)
        {
            List<ClientEffectStartData> effectsToStart = m_effectsToStart;
            m_effectsToStart = other.m_effectsToStart;
            other.m_effectsToStart = effectsToStart;
        }
    }
}
