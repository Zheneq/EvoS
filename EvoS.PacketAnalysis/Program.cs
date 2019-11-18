using System;
using EvoS.Framework.Assets;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
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

        [Option(Description = "Path to replay file", ShortName = "R")]
        public string ReplayFile { get; }

        private void OnExecute()
        {
            if (!AssetLoader.FindAssetRoot(Assets))
            {
                Log.Print(LogType.Error, "AtlasReactor_Data folder not found, please specify with --assets!");
                Log.Print(LogType.Misc, "Alternatively, place Win64 or AtlasReactor_Data in this folder.");
                return;
            }

            HashResolver.Init(AssetLoader.BasePath);

            PacketProvider provider;
            if (!PacketsDir.IsNullOrEmpty())
                provider = new DirectoryPacketProvider(PacketsDir);
            else if (!ReplayFile.IsNullOrEmpty())
                provider = new ReplayPacketProvider(ReplayFile);
            else throw new ArgumentOutOfRangeException(nameof(provider), "Neither PacketsDir or ReplayFile provided!");

            var pdp = new PacketDumpProcessor(provider);

            pdp.Process();
        }
    }
}
