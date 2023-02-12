using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EvoS.DirectoryServer.Account;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Session
{
    public static class SessionManager
    {
        private static ConcurrentDictionary<long, LobbyServerPlayerInfo> ActivePlayers = new ConcurrentDictionary<long, LobbyServerPlayerInfo>();// key: AccountID
        private static ConcurrentDictionary<long, long> SessionTokenAccountIDCache = new ConcurrentDictionary<long, long>(); // key: SessionToken, value: AccountID
        private static ConcurrentDictionary<long, LobbyServerProtocol> ActiveConnections = new ConcurrentDictionary<long, LobbyServerProtocol>();
        private static ConcurrentDictionary<long, LobbySessionInfo> ActiveSessions = new ConcurrentDictionary<long, LobbySessionInfo>();

        public static LobbyServerPlayerInfo OnPlayerConnect(LobbyServerProtocol client, RegisterGameClientRequest clientRequest)
        {
            lock (ActiveSessions)
            {
                long accountId = LoginManager.Login(clientRequest.AuthInfo);
                if (ActiveConnections.ContainsKey(accountId))
                {
                    return null;
                }
            
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);

                client.AccountId = account.AccountId;
                LobbySessionInfo sessionInfo = clientRequest.SessionInfo;
                client.SessionToken = sessionInfo.SessionToken;  // we are kinda supposed to validate it
                client.UserName = account.UserName;
                client.SelectedGameType = GameType.PvP;
                client.SelectedSubTypeMask = 0;

                sessionInfo.AccountId = account.AccountId;
                sessionInfo.UserName = account.UserName;
                sessionInfo.Handle = account.Handle;

                LobbyServerPlayerInfo playerInfo = UpdateLobbyServerPlayerInfo(account.AccountId);
                SessionTokenAccountIDCache.TryAdd(client.SessionToken, playerInfo.AccountId);
                ActiveConnections.TryAdd(client.AccountId, client);
                ActiveSessions.TryAdd(client.AccountId, sessionInfo);

                return playerInfo;
            }
        }

        public static LobbyServerPlayerInfo UpdateLobbyServerPlayerInfo(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            LobbyServerPlayerInfo playerInfo = new LobbyServerPlayerInfo
            {
                AccountId = account.AccountId,
                BannerID = account.AccountComponent.SelectedBackgroundBannerID == -1 ? 95 : account.AccountComponent.SelectedBackgroundBannerID, // patch for existing users: default is 95
                BotCanTaunt = false,
                CharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]),
                ControllingPlayerId = 0,
                EffectiveClientAccessLevel = account.AccountComponent.AppliedEntitlements.ContainsKey("DEVELOPER_ACCESS") ? ClientAccessLevel.Admin : ClientAccessLevel.Full,
                EmblemID = account.AccountComponent.SelectedForegroundBannerID == -1 ? 65 : account.AccountComponent.SelectedForegroundBannerID, // patch for existing users: default is 65 
                Handle = account.Handle,
                IsGameOwner = true,
                IsLoadTestBot = false,
                IsNPCBot = false,
                PlayerId = 0,
                ReadyState = ReadyState.Unknown,
                ReplacedWithBots = false,
                RibbonID = account.AccountComponent.SelectedRibbonID,
                TitleID = account.AccountComponent.SelectedTitleID,
                TitleLevel = 1
            };
            ActivePlayers.AddOrUpdate(playerInfo.AccountId, playerInfo, (k, v) => playerInfo);
            return playerInfo;
        }

        public static void OnPlayerDisconnect(LobbyServerProtocol client)
        {
            lock (ActiveSessions)
            {
                ActivePlayers.TryRemove(client.AccountId, out _);
                SessionTokenAccountIDCache.TryRemove(client.SessionToken, out _);
                ActiveConnections.TryRemove(client.AccountId, out _);
                ActiveSessions.TryRemove(client.AccountId, out _);
            }
        }

        public static LobbyServerPlayerInfo GetPlayerInfo(long accountId)
        {
            LobbyServerPlayerInfo playerInfo = null;
            ActivePlayers.TryGetValue(accountId, out playerInfo);

            return playerInfo;
        }

        public static long GetAccountIdOf(long sessionToken)
        {
            if (SessionTokenAccountIDCache.TryGetValue(sessionToken, out long accountId))
            {
                return accountId;
            }

            return 0;
        }

        public static LobbyServerProtocol GetClientConnection(long accountId)
        {
            LobbyServerProtocol clientConnection = null;
            ActiveConnections.TryGetValue(accountId, out clientConnection);
            return clientConnection;
        }

        public static LobbySessionInfo GetSessionInfo(long accountId)
        {
            LobbySessionInfo sessionInfo = null;
            ActiveSessions.TryGetValue(accountId, out sessionInfo);
            return sessionInfo;
        }

        public static long? GetOnlinePlayerByHandle(string handle)
        {
            return ActivePlayers.Values.FirstOrDefault(lspi => lspi.Handle == handle)?.AccountId;
        }

        public static HashSet<long> GetOnlinePlayers()
        {
            return new HashSet<long>(ActivePlayers.Keys);
        }
        
    }
}
