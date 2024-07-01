using System;
using EvoS.Framework.Logging;

namespace EvoS.Framework.Network.Unity
{
    public static class UNetMessage
    {
        public const int MSG_HEADER_SIZE = 9;

        public static byte[] Serialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length + 1 < MSG_HEADER_SIZE)
            {
                Log.Print(LogType.Error, $"BinaryMessage.Serialize invalid message numBytes={bytes.Length}");
                return null;
            }

            var numArray = new byte[bytes.Length + 1];
            numArray[0] = 0;
            Buffer.BlockCopy(bytes, 0, numArray, 1, bytes.Length);
            return numArray;
        }

        public static byte[] Deserialize(byte[] rawData)
        {
            if (rawData == null || rawData.Length < MSG_HEADER_SIZE)
            {
                Log.Print(LogType.Error, $"BinaryMessage.Deserialize invalid message bytes {rawData?.Length ?? -1}");
                return null;
            }

            var numBytes = rawData.Length - 1;
            var bytes = new byte[numBytes];
            Buffer.BlockCopy(rawData, 1, bytes, 0, numBytes);
            return bytes;
        }
    }
}
