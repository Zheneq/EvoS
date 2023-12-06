using System;
using System.IO;
using CentralServer.LobbyServer.Discord;
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
        public bool GameTypeCustomAvailable = true;
        public int MaxGroupSize = 5;
        public bool MatchAbandoningPenalty = true;
        public int ServerReserveSize = 0;
        public TimeSpan ServerGGTime = TimeSpan.FromSeconds(5);
        public TimeSpan ServerShutdownTime = TimeSpan.FromMinutes(1);
        public DiscordConfiguration Discord = new DiscordConfiguration();
        public bool EnableTrustWar = true;
        public int TrustWarGamePlayedPoints = 5;
        public int TrustWarGameWonPoints = 10;

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

        public static int GetServerReserveSize()
        {
            return GetInstance().ServerReserveSize;
        }

        public static TimeSpan GetServerGGTime()
        {
            return GetInstance().ServerGGTime;
        }

        public static TimeSpan GetServerShutdownTime()
        {
            return GetInstance().ServerShutdownTime;
        }

        public static DiscordConfiguration GetDiscordConfiguration()
        {
            return GetInstance().Discord;
        }
        
        public static bool GetMatchAbandoningPenalty()
        {
            return GetInstance().MatchAbandoningPenalty;
        }

        public static int GetTrustWarGamePlayedPoints()
        {
            return GetInstance().TrustWarGamePlayedPoints;
        }

        public static int GetTrustWarGameWonPoints()
        {
            return GetInstance().TrustWarGameWonPoints;
        }

        public static bool IsTrustWarEnabled()
        {
            return GetInstance().EnableTrustWar;
        }
    }
}
