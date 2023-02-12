using System.IO;
using YamlDotNet.Serialization;

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
        public bool AutoRegisterNewUsers = true;
        public DBConfig Database = new DBConfig();
        
        public bool PingOnGroupRequest = true;

        private static EvosConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new DeserializerBuilder()
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

        public static bool GetAutoRegisterNewUsers()
        {
            return GetInstance().AutoRegisterNewUsers;
        }
        
        public static DBConfig GetDBConfig()
        {
            return GetInstance().Database;
        }

        public static bool GetPingOnGroupRequest()
        {
            return GetInstance().PingOnGroupRequest;
        }

        public enum DBType
        {
            None,
            Mongo,
        }

        public class DBConfig
        {
            public DBType Type = DBType.None;
            public string URI = "localhost:27017";
            public string User = "user";
            public string Password = "password";
            public string Database = "atlas";
            public string Salt = "salt";
            public bool MongoDbSrv = false;
        }
    }
}
