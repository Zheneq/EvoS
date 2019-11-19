using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;
using GLib;
using Gtk;
using Newtonsoft.Json.Linq;
using EvoS.PacketAnalysis;
using EvoS.PacketAnalysis.Packets;
using Application = Gtk.Application;
using UI = Gtk.Builder.ObjectAttribute;

namespace EvoS.PacketInspector
{
    partial class MainWindow : Window
    {
        [UI] private TreeView _treePackets = null;
        [UI] private TreeView _treePacketInfo = null;
        [UI] private TreeView _treeNetObjects = null;
        [UI] private Button _buttonOpenFolder = null;
        [UI] private Button _buttonOpenReplay = null;
        [UI] private Button _buttonSettings = null;
        [UI] private Label _statusLabel = null;
        [UI] private ProgressBar _statusProgressBar = null;

        private TreeStore _treeStorePackets =
            new TreeStore(typeof(PacketInfo), typeof(string), typeof(int), typeof(int), typeof(int), typeof(string),
                typeof(string));

        private TreeModelFilter _treeFilterPackets;
        private TreeStore _treeStorePacketInfo = new TreeStore(typeof(string), typeof(string), typeof(string));

        private TreeStore _treeStoreNetObjects =
            new TreeStore(typeof(GameObject), typeof(uint), typeof(string), typeof(string));

        private uint _filterTargetNetId;

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
        }

        private MainWindow(Builder builder) : base(builder.GetObject("MainWindow").Handle)
        {
            builder.Autoconnect(this);

            _treeFilterPackets = new TreeModelFilter(_treeStorePackets, null) {VisibleFunc = FilterPackets};

            _treePackets.Model = _treeFilterPackets;
            _treePacketInfo.Model = _treeStorePacketInfo;
            _treeNetObjects.Model = _treeStoreNetObjects;

//            AddColumn(_treePackets, 0, "internal", false);
            AddColumn(_treePackets, 1, "Dir");
            AddColumn(_treePackets, 2, "Pkt");
            AddColumn(_treePackets, 3, "Seq");
            AddColumn(_treePackets, 4, "Size");
            AddColumn(_treePackets, 5, "Type");
            AddColumn(_treePackets, 6, "Info", cellData: PacketsCellDataFunc);

            AddColumn(_treePacketInfo, 0, "Key");
            AddColumn(_treePacketInfo, 1, "Type");
            AddColumn(_treePacketInfo, 2, "Value");

            AddColumn(_treeNetObjects, 1, "Id");
            AddColumn(_treeNetObjects, 2, "Type");
            AddColumn(_treeNetObjects, 3, "Info");

            DeleteEvent += Window_DeleteEvent;
            _treePackets.Selection.Changed += TreePackets_SelectionChanged;
            _treeNetObjects.RowActivated += TreeNetObjects_RowActivated;
            _buttonOpenReplay.Activated += OpenReplay_Activated;
            _buttonOpenReplay.Clicked += OpenReplay_Activated;
            _buttonOpenFolder.Activated += OpenFolder_Activated;
            _buttonOpenFolder.Clicked += OpenFolder_Activated;
            _buttonSettings.Activated += ButtonSettings_Activated;
            _buttonSettings.Clicked += ButtonSettings_Activated;

            InitPacketTypeFiltering();
        }

        private void ButtonSettings_Activated(object sender, EventArgs e)
        {
            using var settingsUi = new SettingsUi();
            settingsUi.Show();
            settingsUi.Run();
        }

        private void OpenFolder_Activated(object sender, EventArgs e)
        {
            using var chooser = new PacketDumpSelector(this, "Open Dump Folder", FileChooserAction.SelectFolder);
            chooser.Callback += s => LoadPacketDump(PacketDumpType.PacketDirectory, s);
            chooser.Run();
        }

        private void OpenReplay_Activated(object sender, EventArgs e)
        {
            using var chooser = new PacketDumpSelector(this, "Open Replay", FileChooserAction.Open);
            chooser.SetFilter("AtlasReactor Replay", "*.arr");
            chooser.Callback += s => LoadPacketDump(PacketDumpType.ReplayFile, s);
            chooser.Run();
        }

        public void Reset()
        {
            _treeStorePackets.Clear();
            _treeStoreNetObjects.Clear();
            _treeStorePacketInfo.Clear();
        }

        public void LoadPacketDump(PacketDumpType type, string path)
        {
            _buttonOpenFolder.Sensitive = false;
            _buttonOpenReplay.Sensitive = false;

            Reset();

            PacketProvider provider;
            switch (type)
            {
                case PacketDumpType.ReplayFile:
                    provider = new ReplayPacketProvider(path);
                    break;
                case PacketDumpType.PacketDirectory:
                    provider = new DirectoryPacketProvider(path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            var pdp = new PacketDumpProcessor(provider);
            pdp.Process();

            AddNetObjects(pdp);
            AddPackets(pdp);
        }

        private void PacketsCellDataFunc(TreeViewColumn treeColumn, CellRenderer cell, ITreeModel treeModel,
            TreeIter iter)
        {
            var pkt = new Value();
            treeModel.GetValue(iter, 0, ref pkt);
            var packet = (PacketInfo) pkt.Val;
            var val = new Value(GType.String)
            {
                Val = packet.Error != null ? "#8B0000" : "#000000"
            };

            cell.SetProperty("foreground", val);
        }

        private void AddColumn(TreeView treeView, int index, string title, bool display = true,
            TreeCellDataFunc cellData = null)
        {
            var column = new TreeViewColumn();
            column.Title = title;

            if (display)
            {
                var renderer = new CellRendererText();
                column.PackStart(renderer, true);
                column.AddAttribute(renderer, "text", index);
                if (cellData != null)
                    column.SetCellDataFunc(renderer, cellData);
            }

            treeView.AppendColumn(column);
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void TreePackets_SelectionChanged(object sender, EventArgs _)
        {
            ClearInfoPanel();

            if (!_treePackets.Selection.GetSelected(out var selected)) return;

            var pkt = new Value();
            _treeFilterPackets.GetValue(selected, 0, ref pkt);
            var packet = (PacketInfo) pkt.Val;

            if (packet.Message == null) return;

            if (packet.PacketInteraction != null)
                SetInfoPanelInteractionDump(packet.PacketInteraction);
            else
                SetInfoPanelJsonDump(packet.Message);
        }

        private void SetInfoPanelInteractionDump(PacketInteraction interaction)
        {
            foreach (var call in interaction.Interactions)
            {
                AddInteractionInfoToPanel(TreeIter.Zero, call);
            }

            // check if there are two rows at the top level
            if (!_treeStorePacketInfo.GetIterFirst(out var iter)) return;
            var sibling = iter;
            if (_treeStorePacketInfo.IterNext(ref sibling)) return;

            // expand rows until there are at least two children visible at the same level
            for (;; _treeStorePacketInfo.IterChildren(out iter, iter))
            {
                var path = _treeStorePacketInfo.GetPath(iter);
                _treePacketInfo.ExpandRow(path, false);

                if (_treeStorePacketInfo.IterNChildren(iter) != 1) break;
            }
        }

        private void AddInteractionInfoToPanel(TreeIter parent, PacketInteractionEvent e)
        {
            switch (e)
            {
                case PacketInteractionCall call:
                    var callIter = _treeStorePacketInfo.SmartAppend(parent,
                        $"{call.ClassName}::{call.MethodName}",
                        $"call method, {call.BytesRead} bytes read",
                        $"({call.Events.Count} events)"
                    );

                    foreach (var childEvent in call.Events)
                    {
                        AddInteractionInfoToPanel(callIter, childEvent);
                    }

                    break;
                case PacketInteractionCallSetterLikeEvent setterLike:
                    var args = string.Join(", ", setterLike.Args.Select(v => v?.ToString() ?? "null").ToList());
                    _treeStorePacketInfo.SmartAppend(parent,
                        $"{setterLike.MethodCalled}( .. )",
                        "setter call",
                        args
                    );
                    break;
                case PacketInteractionSetFieldEvent field:
                    _treeStorePacketInfo.SmartAppend(parent,
                        field.FieldName,
                        "set field",
                        field.Value != null ? field.Value.ToString() : "null"
                    );
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void SetInfoPanelJsonDump(object obj, TreeIter parent = default)
        {
            var node = Utils.SerializeObjectAsJObject(obj);

            foreach (var child in node.Children())
            {
                AddJsonInfoToPanel(parent, child);
            }
        }

        private void AddJsonInfoToPanel(TreeIter parent, JToken node, string name = null)
        {
            if (node is JProperty prop)
            {
                name = prop.Name;
                node = prop.Value;
            }

            switch (node)
            {
                case JValue val:
                    _treeStorePacketInfo.SmartAppend(parent, name, val.Type.ToString(),
                        val.Value != null ? val.Value.ToString() : "null");
                    break;
                case JArray array:
                {
                    var iter = _treeStorePacketInfo.SmartAppend(parent, name, "array",
                        $"({array.Children().Count()} items)");
                    var i = 0;
                    foreach (var child in array.Children())
                    {
                        AddJsonInfoToPanel(iter, child, (i++).ToString());
                    }

                    break;
                }
                case JObject obj:
                {
                    var iter = _treeStorePacketInfo.SmartAppend(parent, name, "object",
                        $"({obj.Children().Count()} fields)");
                    foreach (var child in obj.Children())
                    {
                        AddJsonInfoToPanel(iter, child);
                    }

                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }

        private void ClearInfoPanel()
        {
            _treeStorePacketInfo.Clear();
        }

        public void AddPackets(PacketDumpProcessor pdp)
        {
            var sw = Stopwatch.StartNew();

            const int batchSize = 100;
            var processedCount = 0;

            var x = AddPacketsInternal(pdp, batchSize).GetEnumerator();
            Idle.Add(() =>
            {
                if (!x.MoveNext() || !x.Current)
                {
                    x.Dispose();

                    sw.Stop();
                    Console.WriteLine($"AddPackets: {sw.ElapsedMilliseconds}ms");

                    _buttonOpenFolder.Sensitive = true;
                    _buttonOpenReplay.Sensitive = true;
                }
                else
                {
                    processedCount += batchSize;
                    SetStatus($"Added {processedCount}/{pdp.Packets.Count} packets", processedCount, pdp.Packets.Count);
                }

                return x.Current;
            });
        }

        private void SetStatus(string statusText, in int current, in int total)
        {
            _statusProgressBar.Fraction = current / (double) total;
            _statusLabel.Text = statusText;
        }

        private IEnumerable<bool> AddPacketsInternal(PacketDumpProcessor pdp, int batchSize)
        {
            var i = 0;
            foreach (var packet in pdp.Packets)
            {
                _treeStorePackets.AppendValues(
                    packet,
                    packet.Direction == PacketDirection.FromClient ? ">" : "<",
                    packet.PacketNum,
                    packet.msgSeqNum,
                    packet.reader.Length,
                    packet.msgType.ToString(),
                    packet.Message?.ToString() ?? $"[msgType={packet.msgType} no message]"
                );

                if (++i % batchSize == 0)
                {
                    yield return true;
                }
            }

            yield return false;
        }

        public void AddNetObjects(PacketDumpProcessor pdp)
        {
            foreach (var (netId, obj) in pdp.Game.NetObjects)
            {
                _treeStoreNetObjects.AppendValues(
                    obj,
                    netId,
                    obj.Name,
                    ""
                );
            }
        }
    }
}
