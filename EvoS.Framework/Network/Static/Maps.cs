using System;
using System.Collections.Generic;
using System.Text;

namespace EvoS.Framework.Network.Static
{
    public class Maps
    {
        public static String CargoShip_Tutorial1 = "CargoShip_Tutorial1";
        public static String VR_Practice = "VR_Practice";
        public static String CargoShip_Deathmatch = "CargoShip_Deathmatch";
        public static String Casino01_Deathmatch = "Casino01_Deathmatch";
        public static String EvosLab_Deathmatch = "EvosLab_Deathmatch";
        public static String Oblivion_Deathmatch = "Oblivion_Deathmatch";
        public static String Reactor_Deathmatch = "Reactor_Deathmatch";
        public static String RobotFactory_Deathmatch = "RobotFactory_Deathmatch";
        public static String Skyway_Deathmatch = "Skyway_Deathmatch"; // Cloud Piercer
        public static String CargoShip_CTF = "CargoShip_CTF";
        public static String EvosLab_CTF = "EvosLab_CTF";
        public static String Reactor_CTF = "Reactor_CTF";
        public static String RobotFactory_CTF = "RobotFactory_CTF";
        public static String Skyway_CTF = "Skyway_CTF";
        public static String CargoShip_OPU_GameMode = "CargoShip_OPU_GameMode";
        public static String CargoShip_SupportalCombat_GameMode = "CargoShip_SupportalCombat_GameMode";
        public static String EvosLab_OPU_GameMode = "EvosLab_OPU_GameMode";
        public static String EvosLab_SupportalCombat_GameMode = "EvosLab_SupportalCombat_GameMode";
        public static String Reactor_OPU_GameMode = "Reactor_OPU_GameMode";
        public static String Reactor_SupportalCombat_GameMode = "Reactor_SupportalCombat_GameMode";
        public static String RobotFactory_OPU_GameMode = "RobotFactory_OPU_GameMode";
        public static String RobotFactory_SupportalCombat_GameMode = "RobotFactory_SupportalCombat_GameMode";
        public static String Skyway_LightsOut_GameMode = "Skyway_LightsOut_GameMode";
        public static String Skyway_OPU_GameMode = "Skyway_OPU_GameMode";
        public static String Skyway_SupportalCombat_GameMode = "Skyway_SupportalCombat_GameMode";

        public static Dictionary<string, string> GetMapName = new Dictionary<string, string>() {
            { "CargoShip_Deathmatch", "Flyway Freighter" },
            { "Casino01_Deathmatch", "Hexelence" },
            { "EvosLab_Deathmatch", "EvoS Labs" },
            { "Oblivion_Deathmatch", "Oblivion" },
            { "Reactor_Deathmatch", "Omni Reactor Core" },
            { "RobotFactory_Deathmatch", "Hyperforge" },
            { "Skyway_Deathmatch", "Cloudspire" },
            // Add others if we ever add those types
        };

    }
}
