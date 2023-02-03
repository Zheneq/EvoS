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
        public string GameServerExecutableArgs = "";
        public string GitHubCommits = "https://api.github.com/repos/Zheneq/EvoS/commits?per_page=100";
        public bool AutoRegisterNewUsers = true;
        public DBConfig Database = new DBConfig();

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
        
        public static string GetGameServerExecutableArgs()
        {
            return GetInstance().GameServerExecutableArgs;
        }

        public static string GetGitHubCommits()
        {
            return GetInstance().GitHubCommits;
        }

        public static bool GetAutoRegisterNewUsers()
        {
            return GetInstance().AutoRegisterNewUsers;
        }
        
        public static DBConfig GetDBConfig()
        {
            return GetInstance().Database;
        }

        public enum DBType
        {
            None,
            Mongo,
        }

        public class DBConfig
        {
            public DBType Type = DBType.None;
            public string URI = "localhost";
            public string User = "user";
            public string Password = "password";
            public string Database = "atlas";
            public string Salt = "salt";
        }
    }
}
