using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization.NamingConventions;

namespace EvoS.Framework
{
    public class EvosConfiguration
    {
        private static EvosConfiguration Instance = null;
        public int DirectoryServerPort = 6050;
        public string LobbyServerAddress = "127.0.0.1";
        public int LobbyServerPort = 6060;
        public string GameServerExecutable = "";

        private static EvosConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .Build();

                Instance = deserializer.Deserialize<EvosConfiguration>(File.ReadAllText("settings.yaml"));
            }

            return Instance;
        }

        public static int GetDirectoryServerPort()
        {
            return GetInstance().DirectoryServerPort;
        }

        public static string GetLobbyServerAddress()
        {
            return GetInstance().LobbyServerAddress;
        }

        public static int GetLobbyServerPort()
        {
            return GetInstance().LobbyServerPort;
        }

        /// <summary>
        /// Full path to server's "AtlasReactor.exe"
        /// </summary>
        /// <returns></returns>
        public static string GetGameServerExecutable()
        {
            return GetInstance().GameServerExecutable;
        }
    }
}
