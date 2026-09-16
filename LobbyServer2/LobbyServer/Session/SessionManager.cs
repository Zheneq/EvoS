using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Group;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Exceptions;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;
using Prometheus;

namespace CentralServer.LobbyServer.Session
{
    public static class SessionManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SessionManager));
        
        private class SessionInfo
        {
            public LobbyServerProtocol conn;
            public LobbySessionInfo session;
        }

        private class DisconnectedSessionInfo
        {
            public readonly LobbySessionInfo sessionInfo;
            public readonly DateTime disconnectedAt;

            public DisconnectedSessionInfo(LobbySessionInfo sessionInfo, DateTime disconnectedAt)
            {
                this.sessionInfo = sessionInfo;
                this.disconnectedAt = disconnectedAt;
            }
        }

        private static readonly TimeSpan SessionExpiry = new TimeSpan(0, 10, 0);
        private static readonly ConcurrentDictionary<long, SessionInfo> SessionInfos =
            new ConcurrentDictionary<long, SessionInfo>();
        private static readonly ConcurrentDictionary<long, LobbySessionInfo> ConnectingSessions =
            new ConcurrentDictionary<long, LobbySessionInfo>();
        private static readonly ConcurrentDictionary<long, DisconnectedSessionInfo> DisconnectedSessionInfos =
            new ConcurrentDictionary<long, DisconnectedSessionInfo>();
        
        public static event Action<LobbyServerProtocol> OnPlayerConnected = delegate {};
        public static event Action<LobbyServerProtocol> OnPlayerDisconnected = delegate {};
        
        private static readonly Gauge LobbySize = Metrics
            .CreateGauge(
                "evos_lobby_size",
                "Number of people in the lobby.");
        
        private static readonly Gauge ClientVersions = Metrics
            .CreateGauge(
                "evos_lobby_client_branch",
                "How many players currently online use which branch of the client.",
                "branch");

        static SessionManager()
        {
            Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
            {
                LobbySize.Set(GetOnlinePlayers().Count);
                
                Dictionary<string, int> branches = SessionInfos
                    .Values
                    .GroupBy(s => s.session.BuildVersionInfo.Branch)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                var branchesToClear = ClientVersions
                    .GetAllLabelValues()
                    .Where(label => !branches.ContainsKey(label[0]));
                foreach (var branch in branchesToClear)
                {
                    ClientVersions.WithLabels(branch).Set(0);
                }
                
                foreach (var (branch, count) in branches)
                {
                    ClientVersions.WithLabels(branch).Set(count);
                }
            });
        }

        public static void OnPlayerConnect(LobbyServerProtocol client, RegisterGameClientRequest registerRequest)
        {
            lock (SessionInfos)
            {
                if (registerRequest.SessionInfo == null) 
                    throw new RegisterGameException("Session Info not received");
                if (registerRequest.SessionInfo.SessionToken == 0)
                    throw new RegisterGameException("Session Info not received");
                
                LobbySessionInfo sessionInfo = ConnectingSessions.GetValueOrDefault(registerRequest.SessionInfo.AccountId);

                if (sessionInfo == null)
                    throw new RegisterGameException("Session not found. User not logged"); // Session not found
                if (sessionInfo.SessionToken != registerRequest.SessionInfo.SessionToken)
                    throw new RegisterGameException("This session is not valid anymore"); // Session token do not match

                long accountId = sessionInfo.AccountId;
            
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                
                AdminManager.Get().UpdatePenalties(accountId);
                if (account.AdminComponent.Locked)
                {
                    throw new RegisterGameException("This account is temporarily banned. Please, try again later.");
                }

                client.AccountId = account.AccountId;
                client.UserName = account.UserName;
                client.SelectedGameType = GameType.PvP;
                client.SelectedSubTypeMask = 0;
                client.SessionToken = sessionInfo.SessionToken;

                GroupManager.CreateGroup(client.AccountId);
                
                SessionInfos.TryRemove(client.AccountId, out _);
                SessionInfos.TryAdd(client.AccountId, new SessionInfo
                {
                    conn = client,
                    session = sessionInfo
                });
                ConnectingSessions.TryRemove(client.AccountId, out _);
            }
                
            OnPlayerConnected(client);
        }

        public static void OnPlayerDisconnect(LobbyServerProtocol client)
        {
            lock (SessionInfos)
            {
                SessionInfos.TryGetValue(client.AccountId, out SessionInfo sessionInfo);
                // Sometimes on reconnections we first have the new connection and then we receive the previous disconnection
                // To avoid deleting the new connection, we check if the session token is the same
                if (sessionInfo != null && sessionInfo.session.SessionToken == client.SessionToken)
                {
                    if (SessionInfos.TryRemove(client.AccountId, out SessionInfo disconnectedSession))
                    {
                        DisconnectedSessionInfos.TryAdd(client.AccountId, new DisconnectedSessionInfo(disconnectedSession.session, DateTime.Now));
                        // TODO: this sends to every player even if its in game and the disconnected player is not
                        //client.Broadcast(new ChatNotification() { Text = $"{client.UserName} disconnected", ConsoleMessageType = ConsoleMessageType.SystemMessage });
                    }

                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(client.AccountId);
                    account.AdminComponent.LastLogout = DateTime.UtcNow;
                    account.AdminComponent.LastLogoutSessionToken = $"{sessionInfo.session.SessionToken}";
                    DB.Get().AccountDao.UpdateAdminComponent(account);
                }
            }

            OnPlayerDisconnected(client);

            if (CentralServer.PendingShutdown == CentralServer.PendingShutdownType.WaitForPlayersToLeave
                && SessionInfos.IsEmpty)
            {
                CentralServer.PendingShutdown = CentralServer.PendingShutdownType.Now;
            }
        }

        public static LobbyServerProtocol GetClientConnection(long accountId)
        {
            SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
            return sessionInfo?.conn;
        }

        public static LobbySessionInfo GetSessionInfo(long accountId)
        {
            SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
            return sessionInfo?.session;
        }

        public static long? GetOnlinePlayerByHandle(string handle)
        {
            return SessionInfos.Values.FirstOrDefault(si => si.session?.Handle == handle)?.session?.AccountId;
        }

        public static long? GetOnlinePlayerByHandleOrUsername(string handleOrUsername)
        {
            return SessionInfos.Values.FirstOrDefault(si =>
                si.session?.Handle == handleOrUsername
                || si.session?.UserName == handleOrUsername)?.session?.AccountId;
        }

        public static HashSet<long> GetOnlinePlayers()
        {
            return new HashSet<long>(SessionInfos.Keys);
        }

        public static void OnServerShutdown()
        {
            GameManager.StopAllGames();
            LobbyStatusNotification notify = new LobbyStatusNotification
            {
                LocalizedFailure = LocalizationPayload.Create("ServerShutdown@KickSession"),
                AllowRelogin = false,
            };
            foreach (SessionInfo session in SessionInfos.Values)
            {
                session.conn?.Send(notify);
            }
        }

        /// <summary>
        /// Creates a (connecting) session for an account. When <paramref name="rejectIfActive"/> is set (the
        /// fresh-login path), the "already logged in" check and the session creation happen under the same lock
        /// as OnPlayerConnect/OnPlayerDisconnect, so two concurrent logins cannot both pass the check.
        /// The reconnection path leaves it false, since reconnecting to an existing session is expected.
        /// </summary>
        public static LobbySessionInfo CreateSession(long accountId, LobbySessionInfo connectingSessionInfo, IPAddress ipAddress, bool rejectIfActive = false)
        {
            PersistedAccountData account;
            LobbySessionInfo sessionInfo;
            lock (SessionInfos) {
                if (rejectIfActive && SessionInfos.ContainsKey(accountId))
                {
                    throw new ConflictException("This account is already logged in");
                }
                // If we have a game with this accountId do not remove the session we need the info to be able to reconnect
                // Else remove it and create a new Session
                Game game = GameManager.GetGameWithPlayer(accountId);
                LobbySessionInfo oldSession = null;
                if (game != null)
                {
                    oldSession = GetDisconnectedSessionInfo(accountId);
                    if (oldSession == null)
                    {
                        oldSession = KillSession(accountId);
                        if (oldSession != null)
                        {
                            log.Warn($"Account {accountId} reconnected before disconnecting previous session");
                        }
                    }
                }
                DisconnectedSessionInfos.TryRemove(accountId, out _);

                account = DB.Get().AccountDao.GetAccount(accountId);
                sessionInfo = new LobbySessionInfo
                {
                    AccountId = accountId,
                    Handle = account.Handle,
                    UserName = account.UserName,
                    ConnectionAddress = ipAddress.ToString(),
                    BuildVersion = connectingSessionInfo?.BuildVersion ?? "unknown",
                    ProtocolVersion = connectingSessionInfo?.ProtocolVersion ?? ProtocolVersion.VANILLA,
                    LanguageCode = connectingSessionInfo?.LanguageCode ?? "",
                    FakeEntitlements = "",
                    ProcessCode = connectingSessionInfo?.ProcessCode ?? "",
                    ProcessType = connectingSessionInfo?.ProcessType ?? ProcessType.AtlasReactor,
                    SessionToken = oldSession?.SessionToken ?? GenerateToken(account.Handle),
                    ReconnectSessionToken = GenerateToken(account.Handle), // This can be regenerated even on reconnection since we send ReconnectPlayerRequest that sends the new ReconnectSessionToken
                    Region = connectingSessionInfo?.Region ?? Region.EU,
                };
                
                KillSession(accountId);
                ConnectingSessions[accountId] = sessionInfo;
            }

            account.AdminComponent.RecordLogin(ipAddress);
            DB.Get().AccountDao.UpdateAdminComponent(account);
            return sessionInfo;
        }

        public static LobbySessionInfo GetDisconnectedSessionInfo(long accountId)
        {
            if (!DisconnectedSessionInfos.TryGetValue(accountId, out DisconnectedSessionInfo session))
            {
                return null;
            }
            if (DateTime.Now - session.disconnectedAt > SessionExpiry)
            {
                log.Warn($"Attempted to access an expired session for {accountId}");
                DisconnectedSessionInfos.TryRemove(accountId, out _);
                return null;
            }
            return session.sessionInfo;
        }

        public static LobbySessionInfo KillSession(long accountId)
        {
            if (SessionInfos.TryRemove(accountId, out SessionInfo sessionInfo))
            {
                sessionInfo.conn?.CloseConnection();
                return sessionInfo.session;
            }

            return null;
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

        public static void Broadcast(WebSocketMessage message)
        {
            SessionInfos.Values.FirstOrDefault()?.conn.Broadcast(message);
        }
    }
}
