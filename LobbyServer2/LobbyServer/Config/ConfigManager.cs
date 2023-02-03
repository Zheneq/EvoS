namespace CentralServer.LobbyServer.Config
{
    static class ConfigManager
    {
        public static string MOTDPopUpText = "";
        public static string MOTDText = "Lobby server version 2";

        public static string PatchNotesHeader = "Evos Emulator";
        public static string PatchNotesDescription = "";
        public static string PatchNotesText = "Patch notes not available";

        public static bool GameTypePracticeAvailable = false;
        public static bool GameTypeCoopAvailable = false;
        public static bool GameTypePvPAvailable = true;
        public static bool GameTypeRankedAvailable = false;
        public static bool GameTypeCustomAvailable = false;
    }
}
