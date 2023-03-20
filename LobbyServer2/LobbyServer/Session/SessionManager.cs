using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Group;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Exceptions;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Session
{
    public static class SessionManager
    {
        private static ConcurrentDictionary<long, LobbyServerPlayerInfo> ActivePlayers = new ConcurrentDictionary<long, LobbyServerPlayerInfo>();// key: AccountID
        private static ConcurrentDictionary<long, LobbyServerProtocol> ActiveConnections = new ConcurrentDictionary<long, LobbyServerProtocol>();
        private static ConcurrentDictionary<long, LobbySessionInfo> Sessions = new ConcurrentDictionary<long, LobbySessionInfo>();

        private static readonly ILog log = LogManager.GetLogger(typeof(SessionManager));

        public static LobbyServerPlayerInfo OnPlayerConnect(LobbyServerProtocol client, RegisterGameClientRequest registerRequest)
        {
            if (registerRequest == null) return null;

            lock (Sessions)
            {
                
                if (registerRequest.SessionInfo == null) 
                    throw new RegisterGameException("Session Info not received");
                if (registerRequest.SessionInfo.SessionToken == null || registerRequest.SessionInfo.SessionToken == 0)
                    throw new RegisterGameException("Session Info not received"); ;
                
                LobbySessionInfo sessionInfo = GetSessionInfo(registerRequest.SessionInfo.AccountId);

                if (sessionInfo == null) throw new RegisterGameException("Session not found. User not logged"); ; // Session not found

                if (sessionInfo.SessionToken != registerRequest.SessionInfo.SessionToken) throw new RegisterGameException("This session is not valid anymore"); ; // Session token do not match

                long accountId = sessionInfo.AccountId;
            
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);

                client.AccountId = account.AccountId;
                client.UserName = account.UserName;
                client.SelectedGameType = GameType.PvP;
                client.SelectedSubTypeMask = 0;
                client.SessionToken = sessionInfo.SessionToken;

                LobbyServerPlayerInfo playerInfo = UpdateLobbyServerPlayerInfo(account.AccountId);
                ActiveConnections.TryAdd(client.AccountId, client);

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
            lock (Sessions)
            {
                ActivePlayers.TryRemove(client.AccountId, out _);
                LobbySessionInfo session = null;
                Sessions.TryGetValue(client.AccountId, out session);
                
                // Sometimes on reconnections we first have the new connection and then we receive the previous disconnection
                // To avoid deleting the new connection, we check if the sessiontoken is the same
                if (session != null && session.SessionToken == client.SessionToken)
                {
                    ActiveConnections.TryRemove(client.AccountId, out _);
                    // TODO: this sends to every player even if its in game and the disconnected player is not
                    //client.Broadcast(new ChatNotification() { Text = $"{client.UserName} disconnected", ConsoleMessageType = ConsoleMessageType.SystemMessage });
                }                
            }
        }

        public static LobbyServerPlayerInfo GetPlayerInfo(long accountId)
        {
            LobbyServerPlayerInfo playerInfo = null;
            ActivePlayers.TryGetValue(accountId, out playerInfo);

            return playerInfo;
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
            Sessions.TryGetValue(accountId, out sessionInfo);
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

        public static LobbySessionInfo CreateSession(long accountId)
        {
            // If we have a game with this accountId do not remove the session we need the info to be able to reconnect
            // Else remove it and create a new Session
            BridgeServerProtocol server = ServerManager.GetServerWithPlayer(accountId);
            if (server == null)
            {
                Sessions.TryRemove(accountId, out _);
            }

            Sessions.TryGetValue(accountId, out LobbySessionInfo session);
            LobbySessionInfo sessionInfo;
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);

            if (session == null)
            {
                sessionInfo = new LobbySessionInfo()
                {
                    AccountId = accountId,
                    Handle = account.Handle,
                    UserName = account.Handle,
                    ConnectionAddress = "127.0.0.1",
                    BuildVersion = "STABLE-122-100",
                    LanguageCode = "",
                    FakeEntitlements = "",
                    ProcessCode = "",
                    ProcessType = ProcessType.AtlasReactor,
                    SessionToken = GenerateToken(account.Handle),
                    ReconnectSessionToken = GenerateToken(account.Handle),
                    Region = Region.EU,
                };

                // Add session for account
                Sessions.TryAdd(accountId, sessionInfo);
            }
            else
            {
                sessionInfo = new LobbySessionInfo()
                {
                    AccountId = accountId,
                    Handle = account.Handle,
                    UserName = account.Handle,
                    ConnectionAddress = "127.0.0.1",
                    BuildVersion = "STABLE-122-100",
                    LanguageCode = "",
                    FakeEntitlements = "",
                    ProcessCode = "",
                    ProcessType = ProcessType.AtlasReactor,
                    SessionToken = session.SessionToken,
                    ReconnectSessionToken = GenerateToken(account.Handle), // This can be regenerated since we send ReconnectPlayerRequest that sends the new ReconnectSessionToken
                    Region = Region.EU,
                };
            }
            return sessionInfo;
        }

        public static void CleanSessionAfterReconnect(long accountId)
        {
            LobbyServerProtocol client = null;
            ActiveConnections.TryGetValue(accountId, out client);
            if (client != null)
            {
                if (!client.SessionCleaned)
                {
                    client.SessionCleaned = true;
                    GroupManager.LeaveGroup(accountId, false);
                }
                client.WebSocket.Close();
                ActiveConnections.TryRemove(accountId, out client);
            }
        }

        private static long GenerateToken(string a)
        {
            int num = (Guid.NewGuid() + a).GetHashCode();
            if (num < 0)
            {
                num = -num;
            }
            return num;
        }
    }
}
