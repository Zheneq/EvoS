using System.IO;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Packets
{
    public class ReplayPacketProvider : PacketProvider
    {
        public ReplayFile Replay { get; }

        public ReplayPacketProvider(string path) : base(path)
        {
            Replay = ReplayFile.FromJson(File.ReadAllText(path));

            for (uint pktId = 0; pktId < Replay.Messages.Length; pktId++)
            {
                var msg = Replay.Messages[pktId];
                ProcessRawUnet(pktId, msg.Timestamp, PacketDirection.FromServer, UNetMessage.Serialize(msg.Data));
            }
        }
    }
}
