using System;
using EvoS.Framework.Network;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Packets
{
    public class PacketInfo : NetworkMessage
    {
        public PacketDirection Direction { get; internal set; }
        public uint PacketNum { get; internal set; }
        public double Timestamp;
        public object Message;
        public Exception Error;
        public PacketInteraction PacketInteraction;
        public uint NetId;

        public void Deserialize(UNetSerializer serializer, Component context)
        {
            Message = serializer.Deserialize(this, context, Direction == PacketDirection.FromClient);
        }
    }
}
