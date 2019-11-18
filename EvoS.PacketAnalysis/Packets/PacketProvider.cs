using System.Collections.Generic;
using System.Collections.ObjectModel;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Packets
{
    public abstract class PacketProvider
    {
        protected List<PacketInfo> _packetInfos = new List<PacketInfo>();
        public ReadOnlyCollection<PacketInfo> Packets => _packetInfos.AsReadOnly();
        public readonly string Path;

        protected PacketProvider(string path)
        {
            Path = path;
        }

        protected void ProcessRawUnet(in uint pktNum, double timestamp, PacketDirection direction, byte[] data)
        {
            var unetMsg = UNetMessage.Deserialize(data);
            var reader = new NetworkReader(unetMsg);
            while (reader.Position < unetMsg.Length)
            {
                var msg = new PacketInfo
                {
                    PacketNum = pktNum,
                    Direction = direction,
                    Timestamp = timestamp,
                    msgSeqNum = reader.ReadUInt32()
                };
                var msgSize = reader.ReadUInt16();
                msg.msgType = reader.ReadInt16();
                msg.reader = new NetworkReader(reader.ReadBytes(msgSize));
                _packetInfos.Add(msg);
            }
        }
    }
}
