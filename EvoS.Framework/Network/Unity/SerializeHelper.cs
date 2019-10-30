using System.Collections.Generic;
using System.Numerics;
using EvoS.Framework.Game;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkBehaviours;

namespace EvoS.Framework.Network.Unity
{
    public class SerializeHelper
    {
        private IBitStream _writeStream;
        private NetworkWriter _appendWriter;
        private byte[] _lastData;
        private uint _lastDataLength;

        public bool ShouldReturnImmediately(ref IBitStream stream)
        {
            if (stream.isWriting)
            {
                _writeStream = stream;
                _appendWriter = new NetworkWriter();
                stream = new NetworkWriterAdapter(_appendWriter);
            }
            else if (stream.isReading && EvoSGameConfig.NetworkIsServer)
            {
                if (_lastDataLength > 0U)
                    stream.ReadBytes((int) _lastDataLength);
                return true;
            }

            return false;
        }

        public bool End(bool initialState, uint syncVarDirtyBits)
        {
            var flag1 = false;
            if (_writeStream != null)
            {
                var flag2 = false;
                var array = _appendWriter.ToArray();
                var position = (uint) _appendWriter.Position;
                if (_lastData != null && (int) _lastDataLength == (int) position)
                {
                    flag2 = true;
                    for (var index = 0; (long) index < (long) _lastDataLength; ++index)
                    {
                        if (_lastData[index] != array[index])
                        {
                            flag2 = false;
                            break;
                        }
                    }
                }

                var num = !flag2 ? syncVarDirtyBits : 0U;
                if (!initialState)
                    _writeStream.Serialize(ref num);
                if (initialState || num != 0U)
                {
                    _writeStream.Write(array, (int) _writeStream.Position, (int) position);
                    _lastData = array;
                    _lastDataLength = position;
                    flag1 = position > 0U;
                }

                _writeStream = null;
                _appendWriter = null;
            }

            return flag1 || initialState;
        }

//        public static void SerializeActorDataArray(IBitStream stream, ref ActorData[] actorsToSerialize)
//        {
//            var length = 0;
//            int[] numArray1 = null;
//            if (stream.isWriting)
//            {
//                if (actorsToSerialize != null)
//                {
//                    length = actorsToSerialize.Length;
//                    numArray1 = new int[actorsToSerialize.Length];
//                    for (var index = 0; index < numArray1.Length; ++index)
//                        numArray1[index] = actorsToSerialize[index] == null
//                            ? ActorData.s_invalidActorIndex
//                            : actorsToSerialize[index].ActorIndex;
//                }
//
//                stream.Serialize(ref length);
//                for (var index = 0; index < length; ++index)
//                {
//                    var num = numArray1[index];
//                    stream.Serialize(ref num);
//                }
//            }
//
//            if (!stream.isReading)
//                return;
//            stream.Serialize(ref length);
//            var numArray2 = new int[length];
//            for (var index = 0; index < numArray2.Length; ++index)
//            {
//                var invalidActorIndex = ActorData.s_invalidActorIndex;
//                stream.Serialize(ref invalidActorIndex);
//                numArray2[index] = invalidActorIndex;
//            }
//
//            var actorDataArray = new ActorData[numArray2.Length];
//            for (var index = 0; index < actorDataArray.Length; ++index)
//            {
//                if (numArray2[index] != ActorData.s_invalidActorIndex)
//                    actorDataArray[index] = !(GameFlowData.Get() != null)
//                        ? (ActorData) null
//                        : GameFlowData.Get().FindActorByActorIndex(numArray2[index]);
//            }
//
//            actorsToSerialize = actorDataArray;
//        }
//
//        public static void SerializeActorToIntDictionary(
//            IBitStream stream,
//            ref Dictionary<ActorData, int> actorToInt)
//        {
//            var num1 = actorToInt != null ? checked((sbyte) actorToInt.Count) : (sbyte) 0;
//            stream.Serialize(ref num1);
//            if (num1 > 0 && actorToInt == null)
//                actorToInt = new Dictionary<ActorData, int>();
//            if (stream.isWriting && num1 > 0)
//            {
//                foreach (var keyValuePair in actorToInt)
//                {
//                    var num2 = keyValuePair.Key != null
//                        ? (short) keyValuePair.Key.ActorIndex
//                        : (short) ActorData.s_invalidActorIndex;
//                    if (num2 != ActorData.s_invalidActorIndex)
//                    {
//                        var num3 = (short) keyValuePair.Value;
//                        stream.Serialize(ref num2);
//                        stream.Serialize(ref num3);
//                    }
//                }
//            }
//
//            if (!stream.isReading)
//                return;
//            if (actorToInt != null)
//                actorToInt.Clear();
//            for (var index1 = 0; index1 < (int) num1; ++index1)
//            {
//                var invalidActorIndex = (short) ActorData.s_invalidActorIndex;
//                short num2 = 0;
//                stream.Serialize(ref invalidActorIndex);
//                stream.Serialize(ref num2);
//                ActorData index2 = null;
//                if (GameFlowData.Get() != null)
//                    index2 = GameFlowData.Get().FindActorByActorIndex((int) invalidActorIndex);
//                if (index2 != null)
//                    actorToInt[index2] = num2;
//            }
//        }

        public static void SerializeArray(IBitStream stream, ref int[] toSerialize)
        {
            SerializeArray_Base(stream, ref toSerialize, 0,
                (IBitStream s, ref int value) =>
                    s.Serialize(ref value));
        }

        public static void SerializeArray(IBitStream stream, ref Vector3[] toSerialize)
        {
            SerializeArray_Base(stream, ref toSerialize, Vector3.Zero,
                (IBitStream s, ref Vector3 value) =>
                    s.Serialize(ref value));
        }

        private static void SerializeArray_Base<T>(
            IBitStream stream,
            ref T[] toSerializeArray,
            T defaultValue,
            BitstreamSerializeDelegate<T> serializeDelegate)
        {
            var length = 0;
            if (stream.isWriting)
            {
                if (toSerializeArray != null)
                    length = toSerializeArray.Length;
                stream.Serialize(ref length);
                for (int index = 0; index < length; ++index)
                {
                    T val = toSerializeArray[index];
                    serializeDelegate(stream, ref val);
                }
            }

            if (!stream.isReading)
                return;
            stream.Serialize(ref length);
            var objArray = new T[length];
            for (var index = 0; index < objArray.Length; ++index)
            {
                var val = defaultValue;
                serializeDelegate(stream, ref val);
                objArray[index] = val;
            }

            toSerializeArray = objArray;
        }

        protected delegate void BitstreamSerializeDelegate<T>(IBitStream stream, ref T val);
    }
}
