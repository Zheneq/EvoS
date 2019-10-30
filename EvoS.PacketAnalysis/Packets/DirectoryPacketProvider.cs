using System.IO;
using EvoS.Framework.Network.Unity;

namespace EvoS.PacketAnalysis.Packets
{
    public class DirectoryPacketProvider : PacketProvider
    {
        public readonly string Path;

        public DirectoryPacketProvider(string path)
        {
            Path = path;

            var skipped = 0;
            for (uint i = 0;; i++)
            {
                var toServer = $"{Path}/{i}_to_server";
                var fromServer = $"{Path}/{i}_from_server_raw";
                byte[] data;
                PacketDirection direction;

                if (File.Exists(toServer))
                {
                    direction = PacketDirection.ToServer;
                    data = File.ReadAllBytes(toServer);
                }
                else if (File.Exists(fromServer))
                {
                    direction = PacketDirection.FromServer;
                    data = File.ReadAllBytes(fromServer);
                }
                else
                {
                    if (skipped++ > 200) break;

                    continue;
                }

                skipped = 0;

                if (data.Length < 8)
                {
                    continue;
                }

                ProcessRawUnet(i, direction, data);
            }
        }
    }
}
