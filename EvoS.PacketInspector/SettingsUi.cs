using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EvoS.Framework.Assets;
using EvoS.Framework.Logging;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using GLib;
using Gtk;
using Newtonsoft.Json.Linq;
using EvoS.PacketAnalysis;
using EvoS.PacketAnalysis.Packets;
using Application = Gtk.Application;
using Log = EvoS.Framework.Logging.Log;
using UI = Gtk.Builder.ObjectAttribute;

namespace EvoS.PacketInspector
{
    public class SettingsUi : Dialog
    {
        [UI] private Button _buttonOk = null;
        [UI] private Button _buttonCancel = null;
        [UI] private FileChooserButton _atlasDataChooser = null;

        public SettingsUi() : this(new Builder("SettingsUi.glade"))
        {
        }

        private SettingsUi(Builder builder) : base(builder.GetObject("SettingsUi").Handle)
        {
            builder.Autoconnect(this);

            _buttonOk.Activated += ButtonOk_Activated;
            _buttonOk.Clicked += ButtonOk_Activated;
            _buttonCancel.Activated += ButtonCancel_Activated;
            _buttonCancel.Clicked += ButtonCancel_Activated;
            _atlasDataChooser.FileSet += AtlasDataChooser_FileSet;

            if (Program.Settings.AtlasReactorData != null)
                _atlasDataChooser.SetFilename(Program.Settings.AtlasReactorData);

            VerifyAtlasDataFolder();
        }

        private void VerifyAtlasDataFolder()
        {
            Log.Print(LogType.Debug, $"Verifying atlas data folder: {_atlasDataChooser.Filename}");

            _buttonOk.Sensitive = AssetLoader.FindAssetRoot(_atlasDataChooser.Filename);
        }

        private void AtlasDataChooser_FileSet(object sender, EventArgs e)
        {
            Console.WriteLine(_atlasDataChooser.Filename);
            VerifyAtlasDataFolder();
        }

        private void ButtonOk_Activated(object sender, EventArgs e)
        {
            Program.Settings.AtlasReactorData = _atlasDataChooser.Filename;
            Program.Settings.Save();

            Respond(ResponseType.Ok);
        }

        private void ButtonCancel_Activated(object sender, EventArgs e)
        {
            Respond(ResponseType.Cancel);
        }
    }
}
