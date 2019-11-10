using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Game.Resolution
{
    public static class AbilityResultsUtils
    {
        public static Dictionary<ActorData, ClientActorHitResults> DeSerializeActorHitResultsDictionaryFromStream(
            Component context, ref IBitStream stream)
        {
            var dictionary = new Dictionary<ActorData, ClientActorHitResults>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
                stream.Serialize(ref invalidActorIndex);
                ActorData actorByActorIndex = context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
                var clientActorHitResults = new ClientActorHitResults(context, ref stream);
                if (actorByActorIndex != null)
                    dictionary.Add(actorByActorIndex, clientActorHitResults);
            }

            return dictionary;
        }

        public static Dictionary<Vector3, ClientPositionHitResults> DeSerializePositionHitResultsDictionaryFromStream(
            Component context, ref IBitStream stream)
        {
            sbyte num = 0;
            stream.Serialize(ref num);
            var dictionary = new Dictionary<Vector3, ClientPositionHitResults>(num);
            for (var index = 0; index < (int) num; ++index)
            {
                var zero = Vector3.Zero;
                stream.Serialize(ref zero);
                var positionHitResults = new ClientPositionHitResults(context, ref stream);
                dictionary.Add(zero, positionHitResults);
            }

            return dictionary;
        }

        public static List<ClientEffectStartData> DeSerializeEffectsToStartFromStream(Component context,
            ref IBitStream stream)
        {
            sbyte num1 = 0;
            stream.Serialize(ref num1);
            var clientEffectStartDataList = new List<ClientEffectStartData>(num1);
            for (var index1 = 0; index1 < (int) num1; ++index1)
            {
                uint num2 = 0;
                stream.Serialize(ref num2);
                sbyte num3 = 0;
                stream.Serialize(ref num3);
                var sequenceStartDataList = new List<ServerClientUtils.SequenceStartData>(num3);
                for (var index2 = 0; index2 < (int) num3; ++index2)
                {
                    var sequenceStartData =
                        ServerClientUtils.SequenceStartData.SequenceStartData_DeserializeFromStream(ref stream);
                    sequenceStartDataList.Add(sequenceStartData);
                }

                sbyte invalidActorIndex1 = (sbyte) ActorData.s_invalidActorIndex;
                stream.Serialize(ref invalidActorIndex1);
                ActorData actorByActorIndex1 = context.GameFlowData.FindActorByActorIndex(invalidActorIndex1);
                sbyte invalidActorIndex2 = (sbyte) ActorData.s_invalidActorIndex;
                stream.Serialize(ref invalidActorIndex2);
                ActorData actorByActorIndex2 = context.GameFlowData.FindActorByActorIndex(invalidActorIndex2);
                var statuses = new List<StatusType>();
                var statusesOnTurnStart = new List<StatusType>();
                if (invalidActorIndex2 != ActorData.s_invalidActorIndex)
                {
                    sbyte num4 = 0;
                    stream.Serialize(ref num4);
                    for (var index2 = 0; index2 < (int) num4; ++index2)
                    {
                        byte num5 = 0;
                        stream.Serialize(ref num5);
                        statuses.Add((StatusType) num5);
                    }
                }

                if (invalidActorIndex2 != ActorData.s_invalidActorIndex)
                {
                    sbyte num4 = 0;
                    stream.Serialize(ref num4);
                    for (var index2 = 0; index2 < (int) num4; ++index2)
                    {
                        byte num5 = 0;
                        stream.Serialize(ref num5);
                        statusesOnTurnStart.Add((StatusType) num5);
                    }
                }

                bool out0 = false;
                bool out1 = false;
                bool out2 = false;
                bool out3 = false;
                bool out4 = false;
                byte bitField = 0;
                stream.Serialize(ref bitField);
                ServerClientUtils.GetBoolsFromBitfield(bitField, out out0, out out1, out out2, out out3, out out4);
                short num6 = 0;
                if (out3)
                    stream.Serialize(ref num6);
                short num7 = 0;
                if (out4)
                    stream.Serialize(ref num7);
                var clientEffectStartData = new ClientEffectStartData((int) num2, sequenceStartDataList,
                    actorByActorIndex2, actorByActorIndex1, statuses, statusesOnTurnStart, num6, num7, out0, out1,
                    out2);
                clientEffectStartDataList.Add(clientEffectStartData);
            }

            return clientEffectStartDataList;
        }

        public static List<ClientBarrierStartData> DeSerializeBarriersToStartFromStream(Component context, ref IBitStream stream)
        {
            throw new NotImplementedException();
//            sbyte num1 = 0;
//            stream.Serialize(ref num1);
//            var barrierStartDataList = new List<ClientBarrierStartData>(num1);
//            for (var index1 = 0; index1 < (int) num1; ++index1)
//            {
//                var info = new BarrierSerializeInfo();
//                BarrierSerializeInfo.SerializeBarrierInfo(stream, ref info);
//                var guid = info.m_guid;
//                sbyte num2 = 0;
//                stream.Serialize(ref num2);
//                var sequenceStartDataList = new List<ServerClientUtils.SequenceStartData>(num2);
//                for (var index2 = 0; index2 < (int) num2; ++index2)
//                {
//                    var sequenceStartData =
//                        ServerClientUtils.SequenceStartData.SequenceStartData_DeserializeFromStream(ref stream);
//                    sequenceStartData.SetTargetPos(info.m_center);
//                    sequenceStartDataList.Add(sequenceStartData);
//                }
//
//                var barrierStartData = new ClientBarrierStartData(guid, sequenceStartDataList, info);
//                barrierStartDataList.Add(barrierStartData);
//                if (context.BarrierManager != null)
//                    context.BarrierManager.AddClientBarrierInfo(barrierStartData.m_barrierGameplayInfo);
//            }
//
//            return barrierStartDataList;
        }

        public static List<int> DeSerializeEffectsForRemovalFromStream(ref IBitStream stream)
        {
            sbyte num1 = 0;
            stream.Serialize(ref num1);
            var intList = new List<int>(num1);
            for (var index = 0; index < (int) num1; ++index)
            {
                var num2 = -1;
                stream.Serialize(ref num2);
                intList.Add(num2);
            }

            return intList;
        }

        public static List<int> DeSerializeBarriersForRemovalFromStream(ref IBitStream stream)
        {
            sbyte num1 = 0;
            stream.Serialize(ref num1);
            var intList = new List<int>(num1);
            for (var index = 0; index < (int) num1; ++index)
            {
                var num2 = -1;
                stream.Serialize(ref num2);
                intList.Add(num2);
            }

            return intList;
        }

        public static List<ServerClientUtils.SequenceStartData> DeSerializeSequenceStartDataListFromStream(
            ref IBitStream stream)
        {
            var sequenceStartDataList = new List<ServerClientUtils.SequenceStartData>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                var sequenceStartData =
                    ServerClientUtils.SequenceStartData.SequenceStartData_DeserializeFromStream(ref stream);
                sequenceStartDataList.Add(sequenceStartData);
            }

            return sequenceStartDataList;
        }

        public static List<ServerClientUtils.SequenceEndData> DeSerializeSequenceEndDataListFromStream(
            ref IBitStream stream)
        {
            var sequenceEndDataList = new List<ServerClientUtils.SequenceEndData>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                ServerClientUtils.SequenceEndData sequenceEndData =
                    ServerClientUtils.SequenceEndData.SequenceEndData_DeserializeFromStream(ref stream);
                sequenceEndDataList.Add(sequenceEndData);
            }

            return sequenceEndDataList;
        }

        public static ClientAbilityResults DeSerializeClientAbilityResultsFromStream(Component context,
            ref IBitStream stream)
        {
            sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
            sbyte num = -1;
            stream.Serialize(ref invalidActorIndex);
            stream.Serialize(ref num);
            var seqStartDataList = DeSerializeSequenceStartDataListFromStream(ref stream);
            var actorToHitResults = DeSerializeActorHitResultsDictionaryFromStream(context, ref stream);
            var posToHitResults = DeSerializePositionHitResultsDictionaryFromStream(context, ref stream);
            return new ClientAbilityResults(context, invalidActorIndex, num, seqStartDataList, actorToHitResults,
                posToHitResults);
        }

        public static ClientEffectResults DeSerializeClientEffectResultsFromStream(Component context,
            ref IBitStream stream)
        {
            uint num1 = 0;
            sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
            sbyte num2 = -1;
            stream.Serialize(ref num1);
            stream.Serialize(ref invalidActorIndex);
            stream.Serialize(ref num2);
            var seqStartDataList = DeSerializeSequenceStartDataListFromStream(ref stream);
            var actorToHitResults = DeSerializeActorHitResultsDictionaryFromStream(context, ref stream);
            var posToHitResults = DeSerializePositionHitResultsDictionaryFromStream(context, ref stream);
            ActorData actorByActorIndex = context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
            var sourceAbilityActionType = (AbilityData.ActionType) num2;
            return new ClientEffectResults((int) num1, actorByActorIndex, sourceAbilityActionType, seqStartDataList,
                actorToHitResults, posToHitResults);
        }

        public static ClientBarrierResults DeSerializeClientBarrierResultsFromStream(Component context,
            ref IBitStream stream)
        {
            var barrierGUID = -1;
            sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
            stream.Serialize(ref barrierGUID);
            stream.Serialize(ref invalidActorIndex);
            var actorToHitResults = DeSerializeActorHitResultsDictionaryFromStream(context, ref stream);
            var posToHitResults = DeSerializePositionHitResultsDictionaryFromStream(context, ref stream);
            ActorData actorByActorIndex = context.GameFlowData.FindActorByActorIndex(invalidActorIndex);
            return new ClientBarrierResults(barrierGUID, actorByActorIndex, actorToHitResults, posToHitResults);
        }

        public static ClientMovementResults DeSerializeClientMovementResultsFromStream(Component context,
            ref IBitStream stream)
        {
            sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
            stream.Serialize(ref invalidActorIndex);
            var triggeringPath = MovementUtils.DeSerializeLightweightPath(context, stream);
            var seqStartDataList = DeSerializeSequenceStartDataListFromStream(ref stream);
            sbyte num = 0;
            stream.Serialize(ref num);
            var gameplayResponseType = (MovementResults_GameplayResponseType) num;
            ClientEffectResults effectResults = null;
            ClientBarrierResults barrierResults = null;
            ClientAbilityResults powerupResults = null;
            ClientAbilityResults gameModeResults = null;
            switch (gameplayResponseType)
            {
                case MovementResults_GameplayResponseType.Effect:
                    effectResults = DeSerializeClientEffectResultsFromStream(context, ref stream);
                    break;
                case MovementResults_GameplayResponseType.Barrier:
                    barrierResults = DeSerializeClientBarrierResultsFromStream(context, ref stream);
                    break;
                case MovementResults_GameplayResponseType.Powerup:
                    powerupResults = DeSerializeClientAbilityResultsFromStream(context, ref stream);
                    break;
                case MovementResults_GameplayResponseType.GameMode:
                    gameModeResults = DeSerializeClientAbilityResultsFromStream(context, ref stream);
                    break;
            }

            return new ClientMovementResults(context.GameFlowData.FindActorByActorIndex(invalidActorIndex),
                triggeringPath, seqStartDataList, effectResults, barrierResults, powerupResults, gameModeResults);
        }

        public static List<ClientMovementResults> DeSerializeClientMovementResultsListFromStream(Component context,
            ref IBitStream stream)
        {
            var clientMovementResultsList = new List<ClientMovementResults>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                var clientMovementResults = DeSerializeClientMovementResultsFromStream(context, ref stream);
                clientMovementResultsList.Add(clientMovementResults);
            }

            return clientMovementResultsList;
        }

        public static List<ClientReactionResults> DeSerializeClientReactionResultsFromStream(Component context,
            ref IBitStream stream)
        {
            var clientReactionResultsList = new List<ClientReactionResults>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                var seqStartDataList = DeSerializeSequenceStartDataListFromStream(ref stream);
                var effectResults = DeSerializeClientEffectResultsFromStream(context, ref stream);
                byte extraFlags = 0;
                stream.Serialize(ref extraFlags);
                var clientReactionResults = new ClientReactionResults(effectResults, seqStartDataList, extraFlags);
                clientReactionResultsList.Add(clientReactionResults);
            }

            return clientReactionResultsList;
        }

        public static List<int> DeSerializePowerupsToRemoveFromStream(ref IBitStream stream)
        {
            var intList = new List<int>();
            sbyte num1 = 0;
            stream.Serialize(ref num1);
            for (var index = 0; index < (int) num1; ++index)
            {
                var num2 = 0;
                stream.Serialize(ref num2);
                intList.Add(num2);
            }

            return intList;
        }

        public static List<ClientPowerupStealData> DeSerializePowerupsToStealFromStream(Component context,
            ref IBitStream stream)
        {
            sbyte num = 0;
            stream.Serialize(ref num);
            var powerupStealDataList = new List<ClientPowerupStealData>(num);
            for (var index = 0; index < (int) num; ++index)
            {
                var powerupGuid = -1;
                stream.Serialize(ref powerupGuid);
                var powerupResults = DeSerializeClientPowerupResultsFromStream(context, ref stream);
                var powerupStealData = new ClientPowerupStealData(powerupGuid, powerupResults);
                powerupStealDataList.Add(powerupStealData);
            }

            return powerupStealDataList;
        }

        public static ClientPowerupResults DeSerializeClientPowerupResultsFromStream(Component context,
            ref IBitStream stream)
        {
            return new ClientPowerupResults(DeSerializeSequenceStartDataListFromStream(ref stream),
                DeSerializeClientAbilityResultsFromStream(context, ref stream));
        }

        public static List<ClientGameModeEvent> DeSerializeClientGameModeEventListFromStream(Component context,
            ref IBitStream stream)
        {
            var clientGameModeEventList = new List<ClientGameModeEvent>();
            sbyte num = 0;
            stream.Serialize(ref num);
            for (var index = 0; index < (int) num; ++index)
            {
                var clientGameModeEvent = DeSerializeClientGameModeEventFromStream(context, ref stream);
                clientGameModeEventList.Add(clientGameModeEvent);
            }

            return clientGameModeEventList;
        }

        public static ClientGameModeEvent DeSerializeClientGameModeEventFromStream(Component context,
            ref IBitStream stream)
        {
            sbyte num1 = 0;
            byte objectGuid = 0;
            sbyte num2 = 0;
            sbyte num3 = 0;
            sbyte num4 = -1;
            sbyte num5 = -1;
            var eventGuid = 0;
            stream.Serialize(ref num1);
            stream.Serialize(ref objectGuid);
            stream.Serialize(ref num2);
            stream.Serialize(ref num3);
            stream.Serialize(ref num4);
            stream.Serialize(ref num5);
            stream.Serialize(ref eventGuid);
            var eventType = (GameModeEventType) num1;
            var square = num4 != (sbyte) -1 || num5 != (sbyte) -1
                ? context.Board.method_10(num4, num5)
                : null;
            var primaryActor = (int) num2 != ActorData.s_invalidActorIndex
                ? context.GameFlowData.FindActorByActorIndex(num2)
                : null;
            var secondaryActor = (int) num3 != ActorData.s_invalidActorIndex
                ? context.GameFlowData.FindActorByActorIndex(num3)
                : null;
            return new ClientGameModeEvent(eventType, objectGuid, square, primaryActor, secondaryActor, eventGuid);
        }

        public static List<int> DeSerializeClientOverconListFromStream(ref IBitStream stream)
        {
            var intList = new List<int>();
            sbyte num1 = 0;
            stream.Serialize(ref num1);
            for (var index = 0; index < (int) num1; ++index)
            {
                var num2 = -1;
                stream.Serialize(ref num2);
                intList.Add(num2);
            }

            return intList;
        }
    }
}
