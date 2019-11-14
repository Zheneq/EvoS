using System;
using System.Linq;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.Unity;
using EvoS.PacketAnalysis.Packets;
using GLib;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace EvoS.PacketInspector
{
    public partial class MainWindow
    {
        [UI] private Button _buttonPacketFilter;
        [UI] private Popover _popoverPacketFilter;
        [UI] private TreeView _treeFilterPacketType;
        [UI] private EntryCompletion _entrycompletionPacketTypeFilter;
        [UI] private SearchEntry _searchEntryPacketType;

        private TreeModelFilter _treeStoreFilterFilterPacketType;
        private readonly TreeStore _treeStoreFilterPacketType = new TreeStore(typeof(string), typeof(int), typeof(string));
        private bool _packetFilterIsDirty;
        private readonly bool[] _packetTypeFilter = new bool[256];
        private string _typeFilterSearch = string.Empty;

        private void InitPacketTypeFiltering()
        {   
            _treeStoreFilterFilterPacketType = new TreeModelFilter(_treeStoreFilterPacketType, null)
            {
                VisibleFunc = (model, iter) =>
                {
                    var name = (string) model.GetValue(iter, 2) ?? "";
                    var pktId = (int) model.GetValue(iter, 1);

                    int.TryParse(name, out var nameAsNum);

                    return name.ToLower().Contains(_typeFilterSearch) || nameAsNum == pktId;
                }
            };

            for (var i = 0; i < _packetTypeFilter.Length; i++)
            {
                _packetTypeFilter[i] = true;
            }
            _packetTypeFilter[48] = false; // ReplayManagerFile
            _packetTypeFilter[61] = false; // AssetsLoadingProgress
            _packetTypeFilter[62] = false; // AssetsLoadingProgress

            foreach (var (pktId, type) in UNetSerializer.ClientIdsByType
                .Union(UNetSerializer.ServerIdsByType)
                .SelectMany(pair => pair.Value.Select(s => new Tuple<short, Type>(s, pair.Key)))
                .Distinct(new FuncEqualityComparer<Tuple<short, Type>>((a, b) => a.Item1 == b.Item1,
                    tuple => tuple.Item1))
                .OrderBy(i => i.Item1))
            {
                _treeStoreFilterPacketType.AppendValues(
                    _packetTypeFilter[pktId] ? "Shown" : "Hidden",
                    (int) pktId,
                    type.Name
                );
            }

            _treeFilterPacketType.Model = _treeStoreFilterFilterPacketType;
            _entrycompletionPacketTypeFilter.Model = _treeStoreFilterPacketType;

            AddColumn(_treeFilterPacketType, 0, "State");
            AddColumn(_treeFilterPacketType, 1, "Id");
            AddColumn(_treeFilterPacketType, 2, "Name");

            _buttonPacketFilter.Activated += PacketFilter_Activated;
            _buttonPacketFilter.Clicked += PacketFilter_Activated;
            _popoverPacketFilter.Closed += PacketFilter_Closed;
            _searchEntryPacketType.Changed += PacketFilterSearch_Changed;
            _treeFilterPacketType.RowActivated += PacketFilter_RowActivated;
        }

        private void PacketFilter_RowActivated(object o, RowActivatedArgs args)
        {
            _treeStoreFilterPacketType.GetIter(out var iter, args.Path);
            var pktId = (int) _treeStoreFilterPacketType.GetValue(iter, 1);

            var state = _packetTypeFilter[pktId];
            _treeStoreFilterPacketType.SetValue(iter, 0, !state ? "Shown" : "Hidden");
            _packetTypeFilter[pktId] = !state;
            
            _packetFilterIsDirty = true;
        }

        private void PacketFilterSearch_Changed(object sender, EventArgs e)
        {
            _typeFilterSearch = _searchEntryPacketType.Buffer.Text.ToLower();
            _treeStoreFilterFilterPacketType.Refilter();
        }

        private void PacketFilter_Closed(object sender, EventArgs e)
        {
            if (_packetFilterIsDirty)
            {
                _packetFilterIsDirty = false;

                _treeFilterPackets.Refilter();
            }
        }

        private void PacketFilter_Activated(object sender, EventArgs e)
        {
            _popoverPacketFilter.Popup();
        }

        private void TreeNetObjects_RowActivated(object o, RowActivatedArgs args)
        {
            _treeStoreNetObjects.GetIter(out var iter, args.Path);
            var pkt = new Value();
            _treeStoreNetObjects.GetValue(iter, 0, ref pkt);
            var gameObj = (GameObject) pkt.Val;

            if (gameObj != null)
            {
                var netIdent = gameObj.GetComponent<NetworkIdentity>();
                _filterTargetNetId = _filterTargetNetId != netIdent.netId.Value ? netIdent.netId.Value : 0;
            }
            else
                _filterTargetNetId = 0;

            _treeFilterPackets.Refilter();
        }

        private bool FilterPackets(ITreeModel model, TreeIter iter)
        {
            var pkt = new Value();
            model.GetValue(iter, 0, ref pkt);
            var packet = (PacketInfo) pkt.Val;

            return packet == null ||
                   (_filterTargetNetId == 0 || packet.NetId == _filterTargetNetId) &&
                   _packetTypeFilter[packet.msgType];
        }
    }
}
