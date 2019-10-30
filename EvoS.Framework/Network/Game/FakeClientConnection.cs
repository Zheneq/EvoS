using System;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Game
{
    public class FakeClientConnection : ClientConnection
    {
        public FakeClientConnection()
        {
            isReady = true;
        }

        public override string address { get; } = "fake client";

        public override void SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            Log.Print(LogType.Packet, $"SendByChannel(channel={channelId}, {msgType}, {msg})");
//            base.SendByChannel(msgType, msg, channelId);
        }

        public override void SendWriter(NetworkWriter writer, int channelId)
        {
            Log.Print(LogType.Packet, $"SendWriter(channel={channelId}, {Utils.ToHex(writer.ToArray())})");
//            base.SendWriter(writer, channelId);
        }

        public override bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            
            Log.Print(LogType.Server, $"SendBytes(channel={channelId}, {Utils.ToHex(bytes, numBytes)})");
//            return base.SendBytes(bytes, numBytes, channelId);
            return true;
        }

        public T Deserialize<T>(string hex) where T: MessageBase
        {
            var inst = Activator.CreateInstance<T>();
            var reader = new NetworkReader(Utils.ToByteArray(hex));
            inst.Deserialize(reader);
            return inst;
        }

        public void Deserialize(short msgType, string hex)
        {
            var writer = new NetworkWriter();
            writer.StartMessage(msgType);
            writer.WriteBytesFull(Utils.ToByteArray(hex));
            writer.FinishMessage();
            Serializer.ProcessUNetMessages(UNetMessage.Serialize(writer.ToArray()));
        }
    }
}
