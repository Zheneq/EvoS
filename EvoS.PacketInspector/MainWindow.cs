using System;
using System.Linq;
using EvoS.Framework.Misc;
using GLib;
using Gtk;
using Newtonsoft.Json.Linq;
using EvoS.PacketAnalysis;
using EvoS.PacketAnalysis.Packets;
using Application = Gtk.Application;
using UI = Gtk.Builder.ObjectAttribute;

namespace EvoS.PacketInspector
{
    class MainWindow : Window
    {
        [UI] private TreeView _treePackets = null;
        [UI] private TreeView _treePacketInfo = null;

        private TreeStore _treeStorePackets =
            new TreeStore(typeof(PacketInfo), typeof(string), typeof(int), typeof(int), typeof(int), typeof(string),
                typeof(string));

        private TreeStore _treeStorePacketInfo = new TreeStore(typeof(string), typeof(string), typeof(string));

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
        }

        private MainWindow(Builder builder) : base(builder.GetObject("MainWindow").Handle)
        {
            builder.Autoconnect(this);

            _treePackets.Model = _treeStorePackets;
            _treePacketInfo.Model = _treeStorePacketInfo;

//            AddColumn(_treePackets, 0, "internal", false);
            AddColumn(_treePackets, 1, "Dir");
            AddColumn(_treePackets, 2, "Pkt");
            AddColumn(_treePackets, 3, "Seq");
            AddColumn(_treePackets, 4, "Size");
            AddColumn(_treePackets, 5, "Type");
            AddColumn(_treePackets, 6, "Info");

            AddColumn(_treePacketInfo, 0, "Key");
            AddColumn(_treePacketInfo, 1, "Type");
            AddColumn(_treePacketInfo, 2, "Value");

            DeleteEvent += Window_DeleteEvent;
            _treePackets.Selection.Changed += TreePackets_SelectionChanged;
        }

        private void AddColumn(TreeView treeView, int index, string title, bool display = true)
        {
            var column = new TreeViewColumn();
            column.Title = title;

            if (display)
            {
                var renderer = new CellRendererText();
                column.PackStart(renderer, true);
                column.AddAttribute(renderer, "text", index);
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
            _treeStorePackets.GetValue(selected, 0, ref pkt);
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

        private void SetInfoPanelJsonDump(object obj)
        {
            var node = Utils.SerializeObjectAsJObject(obj);

            foreach (var child in node.Children())
            {
                AddJsonInfoToPanel(TreeIter.Zero, child);
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
            foreach (var packet in pdp.Packets)
            {
                if (packet.msgType == 62 || packet.msgType == 61)
                    continue;

                _treeStorePackets.AppendValues(
                    packet,
                    packet.Direction == PacketDirection.FromClient ? ">" : "<",
                    packet.PacketNum,
                    packet.msgSeqNum,
                    packet.reader.Length,
                    packet.msgType.ToString(),
                    packet.Message?.ToString() ?? "[no message]"
                );
            }
        }
    }
}
