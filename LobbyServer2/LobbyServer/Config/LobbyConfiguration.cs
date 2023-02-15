using System.IO;
using YamlDotNet.Serialization;

namespace EvoS.Framework
{
    public class LobbyConfiguration
    {
        private static LobbyConfiguration Instance = null;
        
        public string MOTDPopUpText = "";
        public string MOTDText = "Lobby server version 2";

        public string PatchNotesHeader = "Evos Emulator";
        public string PatchNotesDescription = "";
        public string PatchNotesText = "Patch notes not available";
        public string PatchNotesCommitsUrl = "";

        public bool GameTypePracticeAvailable = false;
        public bool GameTypeCoopAvailable = false;
        public bool GameTypePvPAvailable = true;
        public bool GameTypeRankedAvailable = false;
        public bool GameTypeCustomAvailable = false;
        public int MaxGroupSize = 5;
        public string AdminChannelWebhook = "";
        public string ChannelWebhook = "";

        private static LobbyConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new DeserializerBuilder()
                    .Build();

                Instance = deserializer.Deserialize<LobbyConfiguration>(File.ReadAllText("Config/lobby.yaml"));
            }

            return Instance;
        }

        public static string GetMOTDPopUpText()
        {
            return GetInstance().MOTDPopUpText;
        }

        public static string GetMOTDText()
        {
            return GetInstance().MOTDText;
        }

        public static string GetPatchNotesHeader()
        {
            return GetInstance().PatchNotesHeader;
        }

        public static string GetPatchNotesDescription()
        {
            return GetInstance().PatchNotesDescription;
        }

        public static string GetPatchNotesText()
        {
            return GetInstance().PatchNotesText;
        }

        public static string GetPatchNotesCommitsUrl()
        {
            return GetInstance().PatchNotesCommitsUrl;
        }
        
        public static bool GetGameTypePracticeAvailable()
        {
            return GetInstance().GameTypePracticeAvailable;
        }
        
        public static bool GetGameTypeCoopAvailable()
        {
            return GetInstance().GameTypeCoopAvailable;
        }
        
        public static bool GetGameTypePvPAvailable()
        {
            return GetInstance().GameTypePvPAvailable;
        }
        
        public static bool GetGameTypeRankedAvailable()
        {
            return GetInstance().GameTypeRankedAvailable;
        }
        
        public static bool GetGameTypeCustomAvailable()
        {
            return GetInstance().GameTypeCustomAvailable;
        }

        public static int GetMaxGroupSize()
        {
            return GetInstance().MaxGroupSize > 5 ? 5 : GetInstance().MaxGroupSize;
        }

        public static string GetAdminChannelWebhook()
        {
            return GetInstance().AdminChannelWebhook;
        }

        public static string GetChannelWebhook()
        {
            return GetInstance().ChannelWebhook;
        }
    }
}
