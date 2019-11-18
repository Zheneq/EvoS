using System;
using EvoS.Framework.Assets;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using Gtk;
using EvoS.PacketAnalysis;
using EvoS.PacketAnalysis.Packets;
using McMaster.Extensions.CommandLineUtils;

namespace EvoS.PacketInspector
{
    class Program
    {
        [STAThread]
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
            Patcher.ResolveSyncListFields();
            Patcher.PatchAll();

            Application.Init();

            var app = new Application("EvoS.PacketInspector", GLib.ApplicationFlags.NonUnique);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            if (!PacketsDir.IsNullOrEmpty())
                win.LoadPacketDump(PacketDumpType.PacketDirectory, PacketsDir);
            else if (!ReplayFile.IsNullOrEmpty())
                win.LoadPacketDump(PacketDumpType.ReplayFile, ReplayFile);

            app.AddWindow(win);
            win.Show();
            Application.Run();
        }
    }
}
