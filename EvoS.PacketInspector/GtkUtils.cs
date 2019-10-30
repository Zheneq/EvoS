using Gtk;

namespace EvoS.PacketInspector
{
    public static class GtkUtils
    {
        public static TreeIter SmartAppend(this TreeStore store, TreeIter parent, params object[] par)
        {
            if (TreeIter.Zero.Equals(parent))
                return store.AppendValues(par);
            return store.AppendValues(parent, par);
        }
    }
}
