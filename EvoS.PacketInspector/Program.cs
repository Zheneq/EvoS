using System;
using EvoS.Framework.Assets;
using EvoS.Framework.Misc;
using Gtk;
using EvoS.PacketAnalysis;
using McMaster.Extensions.CommandLineUtils;
using nucs.JsonSettings;
using EvoS.Framework.Logging;

namespace EvoS.PacketInspector
{
    class Program
    {
        [STAThread]
        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        [Option(Description = "Path to packet dump", ShortName = "P")]
        public string PacketsDir { get; }

        [Option(Description = "Path to replay file", ShortName = "R")]
        public string ReplayFile { get; }

        public static Settings Settings { get; } = JsonSettings.Load<Settings>();

        private void OnExecute()
        {
            Application.Init();

            var app = new Application("EvoS.PacketInspector", GLib.ApplicationFlags.NonUnique);
            app.Register(GLib.Cancellable.Current);

            // We require data from the game assets, so ensure we have a valid data path
            if (!AssetLoader.FindAssetRoot(Settings.AtlasReactorData))
            {
                using var settingsUi = new SettingsUi();
                settingsUi.Show();
                app.AddWindow(settingsUi);
                var x = (ResponseType) settingsUi.Run();

                if (x != ResponseType.Ok)
                {
                    return;
                }
            }

            Log.Print(LogType.Misc, $"Found asset root at {Settings.AtlasReactorData}");
            HashResolver.Init(AssetLoader.BasePath);
            Log.Print(LogType.Misc, $"Resolved hashes");
            Patcher.ResolveSyncListFields(Settings.AtlasReactorData);
            Log.Print(LogType.Misc, $"Resolved sync lists");
            Patcher.PatchAll();
            Log.Print(LogType.Misc, $"Patched the library");

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
