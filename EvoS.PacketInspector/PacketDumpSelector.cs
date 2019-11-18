using System;
using Gtk;

namespace EvoS.PacketInspector
{
    public delegate void PacketDumpPathDelegate(string data);

    public class PacketDumpSelector : IDisposable
    {
        private readonly FileChooserDialog _chooserDialog;
        private FileFilter _fileFilter;
        public event PacketDumpPathDelegate Callback = delegate { };

        public PacketDumpSelector(Window parent, string title, FileChooserAction chooserAction)
        {
            _chooserDialog = new FileChooserDialog(title, parent, chooserAction, "_Cancel", 1, "_Open", 0, null);

            _chooserDialog.FileActivated += ChooserOnFileActivated;
        }

        public void SetFilter(string filterName, string filterPattern)
        {
            _fileFilter = new FileFilter {Name = filterName};
            _fileFilter.AddPattern(filterPattern);
            _chooserDialog.AddFilter(_fileFilter);
        }

        public void Run()
        {
            _chooserDialog.Show();
            var retVal = _chooserDialog.Run();

            // If the user clicked Open, fire Activated ourselves
            // double click activation is will fire the callback itself, triggering dialog closure
            if (retVal == 0)
            {
                ChooserOnFileActivated(_chooserDialog, null);
            }
        }

        private void ChooserOnFileActivated(object sender, EventArgs e)
        {
            Callback(_chooserDialog.Filename);

            if (e != null)
                _chooserDialog.Hide();
        }

        public void Dispose()
        {
            _chooserDialog?.Dispose();
            _fileFilter?.Dispose();
            Callback = null;
        }
    }
}
