using System;
using System.Collections.Generic;
using System.Text;

namespace CentralServer.LobbyServer.Config
{
    static class ConfigManager
    {
        public static string MOTDPopUpText = "";
        public static string MOTDText = "Lobby server version 2";

        public static string PatchNotesHeader = "Evos Emulator v0.1";
        public static string PatchNotesDescription = "The resurrection";
        public static string PatchNotesText = "" +
            "- Queue: only practice for the moment\n" +
            "- <b>Groups</b>: not <color=#ff0000>implemented</color> yet\n" +
            "- Friends: not <size=72>implemented</size> yet\n" +
            "- Game Server: work in progress\n" +
            "- Inventory: not implemented yet\n" +
            "- Collection Store: not implemented yet \n" +
            "";

        public static bool GameTypePracticeAvailable = true;
        public static bool GameTypeCoopAvailable = false;
        public static bool GameTypePvPAvailable = true;
        public static bool GameTypeRankedAvailable = false;
        public static bool GameTypeCustomAvailable = false;

        public static bool DailyQuestsAvailable = true;

        public static CharacterType DefaultCharacterType = CharacterType.Tracker;
        public static GameType DefaultGameType = GameType.PvP;

        public static int DefaultISOAmount = 100000;
        public static int DefaultFluxAmount = 50000;
        public static int DefaultGGAmount = 508;
        public static int DefaultRankedCurrencyAmount = 3;
    }
}
