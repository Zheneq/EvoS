using System;
using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game.Resolution;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Network.Unity
{
    public class ServerClientUtils
    {
        public static ActionBufferPhase GetCurrentActionPhase()
        {
            ActionBufferPhase actionBufferPhase = ActionBufferPhase.Done;
//            if (!NetworkServer.active)
//            {
//                if (ClientActionBuffer.Get() != null)
//                    actionBufferPhase = ClientActionBuffer.Get().CurrentActionPhase;
//                else if (GameManager.Get() != null && GameManager.Get().GameStatus == GameStatus.Started)
//                    Log.Print(LogType.Error,"Trying to examine current action phase, but ClientActionBuffer does not exist.");
//            }
            return actionBufferPhase;
        }

        public static AbilityPriority GetCurrentAbilityPhase()
        {
            AbilityPriority abilityPriority = AbilityPriority.INVALID;
//            if (!NetworkServer.active)
//            {
//                if (ClientActionBuffer.Get() != null)
//                    abilityPriority = ClientActionBuffer.Get().AbilityPhase;
//                else
//                    Log.Print(LogType.Error,"Trying to examine current ability phase, but ClientActionBuffer does not exist.");
//            }
            return abilityPriority;
        }

        public static byte CreateBitfieldFromBoolsList(List<bool> bools)
        {
            byte num1 = 0;
            int num2 = Math.Min(bools.Count, 8);
            for (int index = 0; index < num2; ++index)
            {
                if (bools[index])
                    num1 |= (byte) (1 << index);
            }

            return num1;
        }

        public static short CreateBitfieldFromBoolsList_16bit(List<bool> bools)
        {
            short num1 = 0;
            int num2 = Math.Min(bools.Count, 16);
            for (int index = 0; index < num2; ++index)
            {
                if (bools[index])
                    num1 |= (short) (1 << index);
            }

            return num1;
        }

        public static int CreateBitfieldFromBoolsList_32bit(List<bool> bools)
        {
            int num1 = 0;
            int num2 = Math.Min(bools.Count, 32);
            for (int index = 0; index < num2; ++index)
            {
                if (bools[index])
                    num1 |= 1 << index;
            }

            return num1;
        }

        public static byte CreateBitfieldFromBools(
            bool b0,
            bool b1,
            bool b2,
            bool b3,
            bool b4,
            bool b5,
            bool b6,
            bool b7)
        {
            byte num = 0;
            if (b0)
                num |= 1;
            if (b1)
                num |= 2;
            if (b2)
                num |= 4;
            if (b3)
                num |= 8;
            if (b4)
                num |= 16;
            if (b5)
                num |= 32;
            if (b6)
                num |= 64;
            if (b7)
                num |= 128;
            return num;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2,
            out bool out3,
            out bool out4,
            out bool out5,
            out bool out6,
            out bool out7)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
            out3 = (bitField & 8) != 0;
            out4 = (bitField & 16) != 0;
            out5 = (bitField & 32) != 0;
            out6 = (bitField & 64) != 0;
            out7 = (bitField & 128) != 0;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2,
            out bool out3,
            out bool out4,
            out bool out5,
            out bool out6)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
            out3 = (bitField & 8) != 0;
            out4 = (bitField & 16) != 0;
            out5 = (bitField & 32) != 0;
            out6 = (bitField & 64) != 0;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2,
            out bool out3,
            out bool out4,
            out bool out5)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
            out3 = (bitField & 8) != 0;
            out4 = (bitField & 16) != 0;
            out5 = (bitField & 32) != 0;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2,
            out bool out3,
            out bool out4)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
            out3 = (bitField & 8) != 0;
            out4 = (bitField & 16) != 0;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2,
            out bool out3)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
            out3 = (bitField & 8) != 0;
        }

        public static void GetBoolsFromBitfield(
            byte bitField,
            out bool out0,
            out bool out1,
            out bool out2)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
            out2 = (bitField & 4) != 0;
        }

        public static void GetBoolsFromBitfield(byte bitField, out bool out0, out bool out1)
        {
            out0 = (bitField & 1) != 0;
            out1 = (bitField & 2) != 0;
        }

        public static void GetBoolsFromBitfield(byte bitField, out bool out0)
        {
            out0 = (bitField & 1) != 0;
        }

        public class SequenceStartData
        {
            private short m_prefabID;
            private GameObject m_serverOnlyPrefabReference;
            private bool m_useTargetPos;
            private Vector3 m_targetPos;
            private bool m_useTargetSquare;
            private int m_targetSquareX;
            private int m_targetSquareY;
            private bool m_useTargetRotation;
            private Quaternion m_targetRotation;
            private byte m_numTargetActors;
            private int[] m_targetActorIndices;
            private int m_casterActorIndex;
            private byte m_numExtraParams;
            private Sequence.IExtraSequenceParams[] m_extraParams;
            private uint m_sourceRootID;
            private bool m_sourceRemoveAtEndOfTurn;
            private bool m_waitForClientEnable;

//            public SequenceStartData(
//                GameObject prefab,
//                BoardSquare targetSquare,
//                ActorData[] targetActorArray,
//                ActorData caster,
//                SequenceSource source,
//                Sequence.IExtraSequenceParams[] extraParams = null)
//            {
//                InitToDefaults();
//                InitPrefab(prefab);
//                InitSquare(targetSquare);
//                InitTargetActors(targetActorArray);
//                InitCasterActor(caster);
//                InitSequenceSourceData(source);
//                InitExtraParams(extraParams);
//            }

//            public SequenceStartData(
//                GameObject prefab,
//                BoardSquare targetSquare,
//                Quaternion targetRotation,
//                ActorData[] targetActorArray,
//                ActorData caster,
//                SequenceSource source,
//                Sequence.IExtraSequenceParams[] extraParams = null)
//            {
//                InitToDefaults();
//                InitPrefab(prefab);
//                InitSquare(targetSquare);
//                InitRotation(targetRotation);
//                InitTargetActors(targetActorArray);
//                InitCasterActor(caster);
//                InitSequenceSourceData(source);
//                InitExtraParams(extraParams);
//            }
//
//            public SequenceStartData(
//                GameObject prefab,
//                Vector3 targetPos,
//                ActorData[] targetActorArray,
//                ActorData caster,
//                SequenceSource source,
//                Sequence.IExtraSequenceParams[] extraParams = null)
//            {
//                InitToDefaults();
//                InitPrefab(prefab);
//                InitPos(targetPos);
//                InitTargetActors(targetActorArray);
//                InitCasterActor(caster);
//                InitSequenceSourceData(source);
//                InitExtraParams(extraParams);
//            }
//
//            public SequenceStartData(
//                GameObject prefab,
//                Vector3 targetPos,
//                Quaternion targetRotation,
//                ActorData[] targetActorArray,
//                ActorData caster,
//                SequenceSource source,
//                Sequence.IExtraSequenceParams[] extraParams = null)
//            {
//                InitToDefaults();
//                InitPrefab(prefab);
//                InitPos(targetPos);
//                InitRotation(targetRotation);
//                InitTargetActors(targetActorArray);
//                InitCasterActor(caster);
//                InitSequenceSourceData(source);
//                InitExtraParams(extraParams);
//            }

            public SequenceStartData()
            {
                InitToDefaults();
            }

            public void SetRemoveAtEndOfTurn(bool val)
            {
                m_sourceRemoveAtEndOfTurn = val;
            }

            public void SetTargetPos(Vector3 pos)
            {
                InitPos(pos);
            }

            public Vector3 GetTargetPos()
            {
                return m_targetPos;
            }

            public int GetCasterActorIndex()
            {
                return m_casterActorIndex;
            }

            public Sequence.IExtraSequenceParams[] GetExtraParams()
            {
                return m_extraParams;
            }

            private void InitToDefaults()
            {
                m_prefabID = -1;
                m_serverOnlyPrefabReference = null;
                m_useTargetPos = false;
                m_targetPos = Vector3.Zero;
                m_useTargetSquare = false;
                m_targetSquareX = 0;
                m_targetSquareY = 0;
                m_useTargetRotation = false;
                m_targetRotation = Quaternion.Identity;
                m_numTargetActors = 0;
                m_targetActorIndices = null;
                m_casterActorIndex = ActorData.s_invalidActorIndex;
                m_numExtraParams = 0;
                m_extraParams = null;
                m_sourceRootID = 0U;
                m_sourceRemoveAtEndOfTurn = true;
                m_waitForClientEnable = false;
            }

//            private void InitPrefab(GameObject prefab)
//            {
//                m_prefabID = SequenceLookup.Get().GetSequenceIdOfPrefab(prefab);
//                m_serverOnlyPrefabReference = prefab;
//            }

            private void InitPos(Vector3 targetPos)
            {
                m_useTargetPos = true;
                m_targetPos = targetPos;
            }

            private void InitSquare(BoardSquare square)
            {
                if (square == null)
                    return;
                m_useTargetSquare = true;
                m_targetSquareX = square.X;
                m_targetSquareY = square.Y;
                if (m_useTargetPos)
                    return;
                m_targetPos = square.ToVector3();
            }

            private void InitRotation(Quaternion targetRotation)
            {
                m_useTargetRotation = true;
                m_targetRotation = targetRotation;
            }

            public void InitTargetActors(ActorData[] targetActorArray)
            {
                if (targetActorArray == null || targetActorArray.Length <= 0)
                    return;
                m_numTargetActors = (byte) targetActorArray.Length;
                m_targetActorIndices = new int[m_numTargetActors];
                for (int index = 0; index < (int) m_numTargetActors; ++index)
                    m_targetActorIndices[index] = targetActorArray[index].ActorIndex;
            }

            public List<int> GetTargetActorIndices()
            {
                if (m_targetActorIndices != null)
                    return new List<int>(m_targetActorIndices);
                return new List<int>();
            }

            private void InitCasterActor(ActorData caster)
            {
                if (caster != null)
                    m_casterActorIndex = caster.ActorIndex;
                else
                    Log.Print(LogType.Error,"SequenceStartData trying to init its caster actor, but that actor is null.");
            }

            internal void InitSequenceSourceData(SequenceSource source)
            {
                if (!(source != null))
                    return;
                m_sourceRootID = source.RootID;
                m_sourceRemoveAtEndOfTurn = source.RemoveAtEndOfTurn;
                m_waitForClientEnable = source.WaitForClientEnable;
            }

            public void InitExtraParams(Sequence.IExtraSequenceParams[] extraParams)
            {
                if (extraParams == null || extraParams.Length <= 0)
                    return;
                m_extraParams = extraParams;
                m_numExtraParams = (byte) extraParams.Length;
            }

            public short GetSequencePrefabId()
            {
                return m_prefabID;
            }

            public GameObject GetServerOnlyPrefabReference()
            {
                return m_serverOnlyPrefabReference;
            }

            public string GetTargetActorsString(Component context)
            {
                var str = string.Empty;
                if (m_targetActorIndices != null && m_targetActorIndices.Length > 0)
                {
                    foreach (var actorIndex in m_targetActorIndices)
                    {
                        var actorByActorIndex = context.GameFlowData.FindActorByActorIndex(actorIndex);
                        str = actorByActorIndex == null
                            ? $"{str} | (Unknown Actor) {actorIndex}"
                            : $"{str} | {actorByActorIndex.method_95()}";
                    }
                }
                else
                    str = "(Empty)";

                return str;
            }

            public void SequenceStartData_SerializeToStream(ref IBitStream stream)
            {
                throw new NotImplementedException();
//                uint position = stream.Position;
//                byte bitfieldFromBools = CreateBitfieldFromBools(m_useTargetPos, m_useTargetSquare, m_useTargetRotation,
//                    m_sourceRemoveAtEndOfTurn, m_waitForClientEnable, false, false, false);
//                stream.Serialize(ref m_prefabID);
//                stream.Serialize(ref bitfieldFromBools);
//                if (m_useTargetPos)
//                    stream.Serialize(ref m_targetPos);
//                if (m_useTargetSquare)
//                {
//                    byte targetSquareX = (byte) m_targetSquareX;
//                    byte targetSquareY = (byte) m_targetSquareY;
//                    stream.Serialize(ref targetSquareX);
//                    stream.Serialize(ref targetSquareY);
//                }
//
//                if (m_useTargetRotation)
//                {
//                    float num = VectorUtils.HorizontalAngle_Deg(m_targetRotation * new Vector3(1f, 0.0f, 0.0f));
//                    stream.Serialize(ref num);
//                }
//
//                stream.Serialize(ref m_numTargetActors);
//                for (byte index = 0; (int) index < (int) m_numTargetActors; ++index)
//                {
//                    sbyte targetActorIndex = (sbyte) m_targetActorIndices[index];
//                    stream.Serialize(ref targetActorIndex);
//                }
//
//                sbyte casterActorIndex = (sbyte) m_casterActorIndex;
//                stream.Serialize(ref casterActorIndex);
//                stream.Serialize(ref m_sourceRootID);
//                stream.Serialize(ref m_numExtraParams);
//                for (int index = 0; index < (int) m_numExtraParams; ++index)
//                {
//                    var extraParam = m_extraParams[index];
//                    short enumOfExtraParam = (short) SequenceLookup.GetEnumOfExtraParam(extraParam);
//                    stream.Serialize(ref enumOfExtraParam);
//                    extraParam.XSP_SerializeToStream(stream);
//                }
//
//                uint num1 = stream.Position - position;
//                if (!ClientAbilityResults.Boolean_1)
//                    return;
//                Log.Print(LogType.Warning,
//                    $"\t\t\t\t\t Serializing Sequence Start Data, using targetPos? {m_useTargetPos} prefab id {m_prefabID}: \n\t\t\t\t\t numBytes: {num1}");
            }

            public static SequenceStartData SequenceStartData_DeserializeFromStream(ref IBitStream stream)
            {
                throw new NotImplementedException();
//                short num1 = -1;
//                byte bitField = 0;
//                bool out0 = false;
//                Vector3 zero = Vector3.Zero;
//                bool out1 = false;
//                byte num2 = 0;
//                byte num3 = 0;
//                bool out2 = false;
//                Quaternion quaternion = Quaternion.Identity;
//                byte num4 = 0;
//                List<int> intList = new List<int>();
//                sbyte num5 = 0;
//                uint num6 = 0;
//                bool out3 = true;
//                bool out4 = false;
//                byte num7 = 0;
//                var iextraSequenceParamsList = new List<Sequence.IExtraSequenceParams>();
//                stream.Serialize(ref num1);
//                stream.Serialize(ref bitField);
//                bool out5;
//                bool out6;
//                bool out7;
//                GetBoolsFromBitfield(bitField, out out0, out out1, out out2, out out3, out out4, out out5, out out6,
//                    out out7);
//                if (out0)
//                    stream.Serialize(ref zero);
//                if (out1)
//                {
//                    stream.Serialize(ref num2);
//                    stream.Serialize(ref num3);
//                }
//
//                if (out2)
//                {
//                    float angle = 0.0f;
//                    stream.Serialize(ref angle);
//                    quaternion = Quaternion.FromToRotation(new Vector3(1f, 0.0f, 0.0f),
//                        VectorUtils.AngleDegreesToVector(angle));
//                }
//
//                stream.Serialize(ref num4);
//                for (int index = 0; index < (int) num4; ++index)
//                {
//                    sbyte invalidActorIndex = (sbyte) ActorData.s_invalidActorIndex;
//                    stream.Serialize(ref invalidActorIndex);
//                    intList.Add(invalidActorIndex);
//                }
//
//                stream.Serialize(ref num5);
//                stream.Serialize(ref num6);
//                stream.Serialize(ref num7);
//                for (int index = 0; index < (int) num7; ++index)
//                {
//                    short num8 = 0;
//                    stream.Serialize(ref num8);
//                    Sequence.IExtraSequenceParams extraParamOfEnum = SequenceLookup.Get()
//                        .CreateExtraParamOfEnum((SequenceLookup.SequenceExtraParamEnum) num8);
//                    extraParamOfEnum.XSP_DeserializeFromStream(stream);
//                    iextraSequenceParamsList.Add(extraParamOfEnum);
//                }
//
//                return new SequenceStartData
//                {
//                    m_prefabID = num1,
//                    m_useTargetPos = out0,
//                    m_targetPos = zero,
//                    m_useTargetSquare = out1,
//                    m_targetSquareX = !out1 ? -1 : num2,
//                    m_targetSquareY = !out1 ? -1 : num3,
//                    m_useTargetRotation = out2,
//                    m_targetRotation = quaternion,
//                    m_numTargetActors = num4,
//                    m_targetActorIndices = intList.ToArray(),
//                    m_casterActorIndex = num5,
//                    m_sourceRootID = num6,
//                    m_sourceRemoveAtEndOfTurn = out3,
//                    m_waitForClientEnable = out4,
//                    m_numExtraParams = num7,
//                    m_extraParams = iextraSequenceParamsList.ToArray()
//                };
            }

            internal Sequence[] CreateSequencesFromData(
                Component context,
                SequenceSource.ActorDelegate onHitActor,
                SequenceSource.Vector3Delegate onHitPos)
            {
                throw new NotImplementedException();
//                GameObject prefabOfSequenceId = context.SequenceLookup.GetPrefabOfSequenceId(m_prefabID);
//                var targetSquare = !m_useTargetSquare
//                    ? null
//                    : context.Board.method_10(m_targetSquareX, m_targetSquareY);
//                var targets = new ActorData[m_numTargetActors];
//                for (int index = 0; index < (int) m_numTargetActors; ++index)
//                {
//                    ActorData actorByActorIndex = context.GameFlowData.FindActorByActorIndex(m_targetActorIndices[index]);
//                    targets[index] = actorByActorIndex;
//                }
//
//                ActorData actorByActorIndex1 = context.GameFlowData.FindActorByActorIndex(m_casterActorIndex);
//                var source =
//                    new SequenceSource(onHitActor, onHitPos, m_sourceRootID, m_sourceRemoveAtEndOfTurn);
//                source.SetWaitForClientEnable(m_waitForClientEnable);
//                return !m_useTargetRotation
//                    ? (!m_useTargetSquare
//                        ? context.SequenceManager.CreateClientSequences(prefabOfSequenceId, m_targetPos, targets,
//                            actorByActorIndex1, source, m_extraParams)
//                        : context.SequenceManager.CreateClientSequences(prefabOfSequenceId, targetSquare, targets,
//                            actorByActorIndex1, source, m_extraParams))
//                    : (!m_useTargetSquare
//                        ? context.SequenceManager.CreateClientSequences(prefabOfSequenceId, m_targetPos, m_targetRotation,
//                            targets, actorByActorIndex1, source, m_extraParams)
//                        : context.SequenceManager.CreateClientSequences(prefabOfSequenceId, targetSquare, m_targetPos,
//                            m_targetRotation, targets, actorByActorIndex1, source, m_extraParams));
            }

            internal bool HasSequencePrefab()
            {
                throw new NotImplementedException();
//                GameObject gameObject = SequenceLookup.Get().GetPrefabOfSequenceId(m_prefabID);
//                if (gameObject == null && SequenceLookup.Get() != null)
//                    gameObject = SequenceLookup.Get().GetSimpleHitSequencePrefab();
//                return gameObject != null;
            }

            internal bool Contains(SequenceSource sequenceSource)
            {
                if (sequenceSource != null)
                    return (int) m_sourceRootID == (int) sequenceSource.RootID;
                return false;
            }

            internal bool ContainsSequenceSourceID(uint id)
            {
                return (int) m_sourceRootID == (int) id;
            }
        }

        public class SequenceEndData
        {
            private short m_prefabId;
            private uint m_association;
            private AssociationType m_associationType;
            private Vector3 m_targetPos;

            public SequenceEndData(
                int prefabIdToEnd,
                AssociationType associationType,
                int guid,
                Vector3 targetPos)
            {
                m_prefabId = (short) prefabIdToEnd;
                m_associationType = associationType;
                m_association = (uint) Mathf.Max(0, guid);
                m_targetPos = targetPos;
            }

//            public SequenceEndData(
//                GameObject prefabToEnd,
//                AssociationType associationType,
//                int guid,
//                Vector3 targetPos)
//            {
//                m_prefabId = SequenceLookup.Get().GetSequenceIdOfPrefab(prefabToEnd);
//                m_associationType = associationType;
//                m_association = (uint) Mathf.Max(0, guid);
//                m_targetPos = targetPos;
//            }

            public void SequenceEndData_SerializeToStream(ref IBitStream stream)
            {
                sbyte associationType = (sbyte) m_associationType;
                stream.Serialize(ref m_prefabId);
                stream.Serialize(ref associationType);
                stream.Serialize(ref m_association);
                bool flag = m_targetPos != Vector3.Zero;
                stream.Serialize(ref flag);
                if (!flag)
                    return;
                stream.Serialize(ref m_targetPos);
            }

            public static SequenceEndData SequenceEndData_DeserializeFromStream(
                ref IBitStream stream)
            {
                short num1 = -1;
                uint num2 = 0;
                sbyte num3 = -1;
                bool flag = false;
                Vector3 zero = Vector3.Zero;
                stream.Serialize(ref num1);
                stream.Serialize(ref num3);
                stream.Serialize(ref num2);
                stream.Serialize(ref flag);
                if (flag)
                    stream.Serialize(ref zero);
                return new SequenceEndData(num1, (AssociationType) num3, (int) num2, zero);
            }

//            public void EndClientSequences()
//            {
//                if (m_associationType == AssociationType.EffectGuid)
//                    ClientEffectBarrierManager.Get()
//                        .EndSequenceOfEffect((int) m_prefabId, (int) m_association, m_targetPos);
//                else if (m_associationType == AssociationType.BarrierGuid)
//                {
//                    ClientEffectBarrierManager.Get()
//                        .EndSequenceOfBarrier((int) m_prefabId, (int) m_association, m_targetPos);
//                }
//                else
//                {
//                    if (m_associationType != AssociationType.SequenceSourceId)
//                        return;
//                    SequenceManager.Get()
//                        .MarkSequenceToEndBySourceId((int) m_prefabId, (int) m_association, m_targetPos);
//                }
//            }

            public enum AssociationType
            {
                EffectGuid,
                BarrierGuid,
                SequenceSourceId
            }
        }
    }
}
