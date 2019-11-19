using nucs.JsonSettings;

namespace EvoS.PacketInspector
{
    public class Settings : JsonSettings
    {
        public override string FileName { get; set; } = "settings.json";

        #region Settings

        public string AtlasReactorData { get; set; }

        #endregion

        public Settings()
        {
        }

        public Settings(string fileName) : base(fileName)
        {
        }
    }
}
