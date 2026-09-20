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
    public class SessionManager : ISessionRegistry
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SessionManager));

        public static SessionManager Instance { get; internal set; } = new SessionManager();

        private class SessionInfo
        {
            public IClientConnection conn;
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

        private class ConnectingSessionInfo(LobbySessionInfo sessionInfo, DateTime createdAt)
        {
            public readonly LobbySessionInfo sessionInfo = sessionInfo;
            public readonly DateTime createdAt = createdAt;
        }

        private static readonly TimeSpan SessionExpiry = new TimeSpan(0, 10, 0);
        // Entries in ConnectingSessions are only removed on a successful websocket connect, so an abandoned
        // login attempt must stop counting as "active" after a while, or it would lock the account out.
        private static readonly TimeSpan ConnectingSessionExpiry = TimeSpan.FromSeconds(30);
        private readonly ConcurrentDictionary<long, SessionInfo> SessionInfos =
            new ConcurrentDictionary<long, SessionInfo>();
        private readonly ConcurrentDictionary<long, ConnectingSessionInfo> ConnectingSessions =
            new ConcurrentDictionary<long, ConnectingSessionInfo>();
        private readonly ConcurrentDictionary<long, DisconnectedSessionInfo> DisconnectedSessionInfos =
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

        public SessionManager()
        {
            Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
            {
                LobbySize.Set(GetOnlinePlayersCore().Count());

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

        // -----------------------------------------------------------------------
        // Static lifecycle methods — not on the interface; access state via Instance
        // -----------------------------------------------------------------------

        public static void OnPlayerConnect(IClientConnection client, RegisterGameClientRequest registerRequest)
        {
            lock (Instance.SessionInfos)
            {
                if (registerRequest.SessionInfo == null)
                    throw new RegisterGameException("Session Info not received");
                if (registerRequest.SessionInfo.SessionToken == 0)
                    throw new RegisterGameException("Session Info not received");

                LobbySessionInfo sessionInfo = Instance.ConnectingSessions.GetValueOrDefault(registerRequest.SessionInfo.AccountId)?.sessionInfo;

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

                client.Initialize(account.AccountId, account.UserName, sessionInfo.SessionToken);

                GroupManager.CreateGroup(client.AccountId);

                Instance.SessionInfos.TryRemove(client.AccountId, out _);
                Instance.SessionInfos.TryAdd(client.AccountId, new SessionInfo
                {
                    conn = client,
                    session = sessionInfo
                });
                Instance.ConnectingSessions.TryRemove(client.AccountId, out _);
            }

            OnPlayerConnected((LobbyServerProtocol)client);
        }

        public static void OnPlayerDisconnect(LobbyServerProtocol client)
        {
            lock (Instance.SessionInfos)
            {
                Instance.SessionInfos.TryGetValue(client.AccountId, out SessionInfo sessionInfo);
                // Sometimes on reconnections we first have the new connection and then we receive the previous disconnection
                // To avoid deleting the new connection, we check if the session token is the same
                if (sessionInfo != null && sessionInfo.session.SessionToken == client.SessionToken)
                {
                    if (Instance.SessionInfos.TryRemove(client.AccountId, out SessionInfo disconnectedSession))
                    {
                        Instance.DisconnectedSessionInfos.TryAdd(client.AccountId, new DisconnectedSessionInfo(disconnectedSession.session, DateTime.Now));
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
                && Instance.SessionInfos.IsEmpty)
            {
                CentralServer.PendingShutdown = CentralServer.PendingShutdownType.Now;
            }
        }

        public static void OnServerShutdown()
        {
            GameManager.StopAllGames();
            LobbyStatusNotification notify = new LobbyStatusNotification
            {
                LocalizedFailure = LocalizationPayload.Create("ServerShutdown@KickSession"),
                AllowRelogin = false,
            };
            foreach (SessionInfo session in Instance.SessionInfos.Values)
            {
                session.conn?.Send(notify);
            }
        }

        // -----------------------------------------------------------------------
        // Public static forwarders — preserve all existing call sites unchanged
        // -----------------------------------------------------------------------

        public static LobbyServerProtocol? GetClientConnection(long accountId)
            => Instance.GetClientConnectionCore(accountId) as LobbyServerProtocol;

        public static LobbySessionInfo GetSessionInfo(long accountId)
            => Instance.GetSessionInfoCore(accountId);

        public static long? GetOnlinePlayerByHandle(string handle)
            => Instance.GetOnlinePlayerByHandleCore(handle);

        public static long? GetOnlinePlayerByHandleOrUsername(string handleOrUsername)
            => Instance.GetOnlinePlayerByHandleOrUsernameCore(handleOrUsername);

        public static HashSet<long> GetOnlinePlayers()
            => new HashSet<long>(Instance.GetOnlinePlayersCore());

        /// <summary>
        /// Creates a (connecting) session for an account. When <paramref name="rejectIfActive"/> is set (the
        /// fresh-login path), the "already logged in" check covers both established sessions and logins still
        /// waiting for their websocket connect, and it happens under the same lock as
        /// OnPlayerConnect/OnPlayerDisconnect, so two concurrent logins cannot both pass the check.
        /// The reconnection path leaves it false, since reconnecting to an existing session is expected.
        /// </summary>
        public static LobbySessionInfo CreateSession(long accountId, LobbySessionInfo connectingSessionInfo, IPAddress ipAddress, bool rejectIfActive = false)
            => Instance.CreateSessionCore(accountId, connectingSessionInfo, ipAddress, rejectIfActive);

        public static LobbySessionInfo GetDisconnectedSessionInfo(long accountId)
            => Instance.GetDisconnectedSessionInfoCore(accountId);

        public static LobbySessionInfo KillSession(long accountId)
            => Instance.KillSessionCore(accountId);

        public static void Broadcast(WebSocketMessage message)
            => Instance.BroadcastCore(message);

        // -----------------------------------------------------------------------
        // ISessionRegistry explicit implementation
        // -----------------------------------------------------------------------

        IClientConnection? ISessionRegistry.GetClientConnection(long accountId)
            => GetClientConnectionCore(accountId);

        LobbySessionInfo? ISessionRegistry.GetSessionInfo(long accountId)
            => GetSessionInfoCore(accountId);

        IEnumerable<long> ISessionRegistry.GetOnlinePlayers()
            => GetOnlinePlayersCore();

        long? ISessionRegistry.GetOnlinePlayerByHandle(string handle)
            => GetOnlinePlayerByHandleCore(handle);

        long? ISessionRegistry.GetOnlinePlayerByHandleOrUsername(string handleOrUsername)
            => GetOnlinePlayerByHandleOrUsernameCore(handleOrUsername);

        LobbySessionInfo ISessionRegistry.CreateSession(long accountId, LobbySessionInfo connectingSessionInfo, IPAddress ipAddress, bool rejectIfActive)
            => CreateSessionCore(accountId, connectingSessionInfo, ipAddress, rejectIfActive);

        LobbySessionInfo? ISessionRegistry.GetDisconnectedSessionInfo(long accountId)
            => GetDisconnectedSessionInfoCore(accountId);

        LobbySessionInfo? ISessionRegistry.KillSession(long accountId)
            => KillSessionCore(accountId);

        void ISessionRegistry.Broadcast(WebSocketMessage message)
            => BroadcastCore(message);

        // -----------------------------------------------------------------------
        // Private core methods — contain the original logic
        // -----------------------------------------------------------------------

        private IClientConnection? GetClientConnectionCore(long accountId)
        {
            SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
            return sessionInfo?.conn;
        }

        private LobbySessionInfo GetSessionInfoCore(long accountId)
        {
            SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
            return sessionInfo?.session;
        }

        private long? GetOnlinePlayerByHandleCore(string handle)
        {
            return SessionInfos.Values.FirstOrDefault(si => si.session?.Handle == handle)?.session?.AccountId;
        }

        private long? GetOnlinePlayerByHandleOrUsernameCore(string handleOrUsername)
        {
            return SessionInfos.Values.FirstOrDefault(si =>
                si.session?.Handle == handleOrUsername
                || si.session?.UserName == handleOrUsername)?.session?.AccountId;
        }

        private IEnumerable<long> GetOnlinePlayersCore()
        {
            return SessionInfos.Keys;
        }

        private LobbySessionInfo CreateSessionCore(long accountId, LobbySessionInfo connectingSessionInfo, IPAddress ipAddress, bool rejectIfActive = false)
        {
            PersistedAccountData account;
            LobbySessionInfo sessionInfo;
            lock (SessionInfos) {
                if (rejectIfActive)
                {
                    if (SessionInfos.ContainsKey(accountId))
                    {
                        throw new ConflictException("This account is already logged in");
                    }
                    if (ConnectingSessions.TryGetValue(accountId, out ConnectingSessionInfo connecting)
                        && DateTime.UtcNow - connecting.createdAt < ConnectingSessionExpiry)
                    {
                        throw new ConflictException("This account is already logging in");
                    }
                }
                // If we have a game with this accountId do not remove the session we need the info to be able to reconnect
                // Else remove it and create a new Session
                Game game = GameManager.GetGameWithPlayer(accountId);
                LobbySessionInfo oldSession = null;
                if (game != null)
                {
                    oldSession = GetDisconnectedSessionInfoCore(accountId);
                    if (oldSession == null)
                    {
                        oldSession = KillSessionCore(accountId);
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

                KillSessionCore(accountId);
                ConnectingSessions[accountId] = new ConnectingSessionInfo(sessionInfo, DateTime.UtcNow);
            }

            account.AdminComponent.RecordLogin(ipAddress);
            DB.Get().AccountDao.UpdateAdminComponent(account);
            return sessionInfo;
        }

        private LobbySessionInfo GetDisconnectedSessionInfoCore(long accountId)
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

        private LobbySessionInfo KillSessionCore(long accountId)
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

        private void BroadcastCore(WebSocketMessage message)
        {
            (SessionInfos.Values.FirstOrDefault()?.conn as LobbyServerProtocol)?.Broadcast(message);
        }
    }
}
