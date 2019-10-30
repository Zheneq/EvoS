using EvoS.Framework.Assets;
using EvoS.Framework.Logging;
using EvoS.PacketAnalysis.Packets;
using McMaster.Extensions.CommandLineUtils;

namespace EvoS.PacketAnalysis
{
    class Program
    {
        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        [Option(Description = "Path to AtlasReactor_Data", ShortName = "D")]
        public string Assets { get; }

        [Option(Description = "Path to packet dump", ShortName = "P")]
        public string PacketsDir { get; }

        private void OnExecute()
        {
            if (!AssetLoader.FindAssetRoot(Assets))
            {
                Log.Print(LogType.Error, "AtlasReactor_Data folder not found, please specify with --assets!");
                Log.Print(LogType.Misc, "Alternatively, place Win64 or AtlasReactor_Data in this folder.");
                return;
            }

            var dpp = new DirectoryPacketProvider(PacketsDir);
            var pdp = new PacketDumpProcessor(dpp);

            pdp.Process();
        }
    }
}
