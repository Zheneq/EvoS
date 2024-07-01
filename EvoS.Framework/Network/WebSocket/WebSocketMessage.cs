using System;

namespace EvoS.Framework.Network.WebSocket
{
    [Serializable]
    public abstract class WebSocketMessage
    {
        public static int MessageTypeID = 0;

        public const int INVALID_ID = 0;
        public int RequestId { get; set; }
        public int ResponseId { get; set; }

        [NonSerialized]
        public long DeserializationTicks;
        [NonSerialized]
        public long SerializedLength;
        [NonSerialized]
        public static bool LogData = true;
    }
}
