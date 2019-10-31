using System;
using EvoS.Framework.Assets;
using EvoS.Framework.Logging;
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

        private void OnExecute()
        {
            if (!AssetLoader.FindAssetRoot(Assets))
            {
                Log.Print(LogType.Error, "AtlasReactor_Data folder not found, please specify with --assets!");
                Log.Print(LogType.Misc, "Alternatively, place Win64 or AtlasReactor_Data in this folder.");
                return;
            }

            HashResolver.Init(AssetLoader.BasePath);
            Patcher.PatchAll();

            var dpp = new DirectoryPacketProvider(PacketsDir);
            var pdp = new PacketDumpProcessor(dpp);

            pdp.Process();

            Application.Init();

            var app = new Application("org.GtkApplication.GtkApplication", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            win.AddPackets(pdp);
            win.AddNetObjects(pdp);

            app.AddWindow(win);
            win.Show();
            Application.Run();
        }
    }
}
