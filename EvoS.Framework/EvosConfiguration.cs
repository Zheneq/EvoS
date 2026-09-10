using System;
using System.Collections.Generic;
using System.IO;
using CentralServer.LobbyServer.Config;
using EvoS.Framework.Auth;
using YamlDotNet.Serialization;
using EvoS.Framework.Misc;

namespace EvoS.Framework
{
    public class EvosConfiguration
    {
        public bool DevMode = false;
        public int DirectoryServerPort = 6050;
        public string LobbyServerAddress = "127.0.0.1";
        public int LobbyServerPort = 6060;
        public TimeSpan LobbyServerTimeOut = TimeSpan.FromSeconds(20);
        public string GameServerExecutable = "";
        public string GameServerExecutableArgs = "";
        public GameServerPickOrder GameServerPickOrder = GameServerPickOrder.RANDOM;
        public string SteamWebApiKey = "";
        public bool AutoRegisterNewUsers = true;
        public HashSet<LinkedAccount.AccountType> LinkedAccountAllowedTypes = new HashSet<LinkedAccount.AccountType>();
        public List<List<LinkedAccount.Condition>> LinkedAccountRegistrationConditions = new List<List<LinkedAccount.Condition>>();
        public List<List<LinkedAccount.Condition>> LinkedAccountLoginConditions = null;
        public HashSet<LinkedAccount.AccountType> LinkedAccountsForPasswordReset = new HashSet<LinkedAccount.AccountType>();
        public bool RequireRegistrationCode = false;
        public TimeSpan RegistrationCodeLifetime = TimeSpan.Zero;
        public TimeSpan TempPasswordLifetime = TimeSpan.FromHours(12);
        public bool DisableUserIpCheck = false;
        public int MaxLinkedAccounts = 6;
        public DBConfig Database = new DBConfig();
        public string UserApiKey = "";
        public int UserApiPort = 3002;
        public string AdminApiKey = "";
        public int AdminApiPort = 3001;
        public string TicketAuthKey = "";
        public string ClientIpHeader = "";
        public ushort MetricsPort = 1234;
        public bool AllowUsernamePasswordAuth = true;
        
        public bool PingOnGroupRequest = true;

        private static Lazy<EvosConfiguration> _instance = new Lazy<EvosConfiguration>(() =>
            new DeserializerBuilder().Build().Deserialize<EvosConfiguration>(File.ReadAllText("Config/settings.yaml")));

        private static EvosConfiguration Instance => _instance.Value;

        public static bool GetDevMode() => Instance.DevMode;

        public static int GetDirectoryServerPort() => Instance.DirectoryServerPort;

        public static string GetLobbyServerAddress() => Instance.LobbyServerAddress;

        public static int GetLobbyServerPort() => Instance.LobbyServerPort;

        public static TimeSpan GetLobbyServerTimeOut() => Instance.LobbyServerTimeOut;

        /// <summary>
        /// Full path to server's "AtlasReactor.exe"
        /// </summary>
        /// <returns></returns>
        public static string GetGameServerExecutable() => Instance.GameServerExecutable;

        public static string GetGameServerExecutableArgs() => Instance.GameServerExecutableArgs;

        public static GameServerPickOrder GetGameServerPickOrder() => Instance.GameServerPickOrder;

        /// <summary>
        /// You can get one from https://steamcommunity.com/dev/registerkey
        /// </summary>
        /// <returns></returns>
        public static string GetSteamWebApiKey() => Instance.SteamWebApiKey;
        public static bool SteamApiEnabled => !string.IsNullOrWhiteSpace(GetSteamWebApiKey());

        public static bool GetAutoRegisterNewUsers() => Instance.AutoRegisterNewUsers;
        
        public static HashSet<LinkedAccount.AccountType> GetLinkedAccountAllowedTypes() => Instance.LinkedAccountAllowedTypes;
        
        public static List<List<LinkedAccount.Condition>> GetLinkedAccountRegistrationConditions() => Instance.LinkedAccountRegistrationConditions;
        
        public static List<List<LinkedAccount.Condition>> GetLinkedAccountLoginConditions() => Instance.LinkedAccountLoginConditions ?? GetLinkedAccountRegistrationConditions();
        
        public static HashSet<LinkedAccount.AccountType> GetLinkedAccountsForPasswordReset() => Instance.LinkedAccountsForPasswordReset;
        
        public static bool GetRequireRegistrationCode() => Instance.RequireRegistrationCode;
        
        public static TimeSpan GetRegistrationCodeLifetime() => Instance.RegistrationCodeLifetime;
        
        public static TimeSpan GetTempPasswordLifetime() => Instance.TempPasswordLifetime;
        
        public static bool GetDisableUserIpCheck() => Instance.DisableUserIpCheck;

        public static int GetMaxLinkedAccounts() => Instance.MaxLinkedAccounts;
        
        public static DBConfig GetDBConfig() => Instance.Database;

        public static bool GetPingOnGroupRequest() => Instance.PingOnGroupRequest;

        public static string GetAdminApiKey() => Instance.AdminApiKey;

        public static string GetUserApiKey() => Instance.UserApiKey;

        public static int GetAdminApiPort() => Instance.AdminApiPort;

        public static int GetUserApiPort() => Instance.UserApiPort;

        public static ushort GetMetricsPort() => Instance.MetricsPort;

        public static string GetClientIpHeader() => Instance.ClientIpHeader;

        public static string GetTicketAuthKey() => Instance.TicketAuthKey;

        public static bool GetAllowUsernamePasswordAuth() => Instance.AllowUsernamePasswordAuth;

        public static bool GetAllowTicketAuth() => !GetUserApiKey().IsNullOrEmpty() && !GetTicketAuthKey().IsNullOrEmpty();

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
            public bool UseCredentials = true;
        }
    }
}
