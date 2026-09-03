using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol: WebSocketBehaviorBase<AllianceMessageBase>, IGameServerConnection
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeServerProtocol));
        
        public event Action<BridgeServerProtocol, LobbyGameSummary, LobbyGameSummaryOverrides> OnGameEnded = delegate {};
        public event Action<BridgeServerProtocol, GameStatus> OnStatusUpdate = delegate {};
        public event Action<BridgeServerProtocol, ServerGameMetrics> OnGameMetricsUpdate = delegate {};
        public event Action<BridgeServerProtocol, LobbyServerPlayerInfo, LobbySessionInfo> OnPlayerDisconnect = delegate {};
        public event Action<BridgeServerProtocol> OnServerDisconnect = delegate {};

        private string ProtocolStr;
        private string Address;
        private int Port;
        private string AddressForLog;
        private LobbySessionInfo SessionInfo;

        private byte[] _authNonce;
        private int _pendingCallbackId;
        private bool _registered;

        public string URI => ProtocolStr + "://" + Address + ":" + Port;
        public string BuildVersion => SessionInfo?.BuildVersion ?? "";
        public bool IsPrivate { get; private set; }
        public bool IsReserved { get; private set; }

        public string ProcessCode { set; get; }
        public string Fingerprint { get; private set; }
        public string Name { protected set; get; }
        
        protected override AllianceMessageBase DeserializeMessage(byte[] data, out int callbackId)
        {
            return BridgeMessageSerializer.DeserializeMessage(data, out callbackId);
        }
        
        protected override string GetConnContext()
        {
            return $"S {AddressForLog}:{Port}";
        }

        public BridgeServerProtocol()
        {
            RegisterHandler<RegisterGameServerRequest>(HandleRegisterGameServerRequest);
            RegisterHandler<ServerGameSummaryNotification>(HandleServerGameSummaryNotification);
            RegisterHandler<PlayerDisconnectedNotification>(HandlePlayerDisconnectedNotification);
            RegisterHandler<ServerGameMetricsNotification>(HandleServerGameMetricsNotification);
            RegisterHandler<ServerGameStatusNotification>(HandleServerGameStatusNotification);
            RegisterHandler<MonitorHeartbeatNotification>(HandleMonitorHeartbeatNotification);
            RegisterHandler<LaunchGameResponse>(HandleLaunchGameResponse);
            RegisterHandler<JoinGameServerResponse>(HandleJoinGameServerResponse);
            RegisterHandler<ReconnectPlayerResponse>(HandleReconnectPlayerResponse);
        }

        public void SendJoinGameRequests(LobbyServerTeamInfo TeamInfo, Dictionary<int, LobbySessionInfo> sessionInfos, string GameServerProcessCode)
        {
            foreach (LobbyServerPlayerInfo playerInfo in TeamInfo.TeamPlayerInfo)
            {
                LobbySessionInfo sessionInfo = sessionInfos[playerInfo.PlayerId];
                JoinGameServerRequest request = new JoinGameServerRequest
                {
                    OrigRequestId = 0,
                    GameServerProcessCode = GameServerProcessCode,
                    PlayerInfo = playerInfo,
                    SessionInfo = sessionInfo
                };
                Send(request);
            }
        }

        protected override void HandleOpen()
        {
            // Challenge the server to prove possession of its private key before it may register.
            _authNonce = GameServerAuth.GenerateNonce();
            Send(new ServerAuthChallengeNotification { Nonce = Convert.ToBase64String(_authNonce) });
        }

        private void HandleRegisterGameServerRequest(RegisterGameServerRequest request, int callbackId)
        {
            if (_authNonce == null
                || !GameServerAuth.VerifySignature(request.PublicKey, _authNonce, DecodeSignature(request.Signature)))
            {
                log.Warn("Rejecting game server registration: invalid challenge signature");
                Send(new RegisterGameServerResponse { Success = false }, callbackId);
                CloseConnection();
                return;
            }

            string fingerprint = GameServerAuth.ComputeFingerprint(request.PublicKey);
            if (fingerprint == null)
            {
                log.Warn("Rejecting game server registration: unparseable public key");
                Send(new RegisterGameServerResponse { Success = false }, callbackId);
                CloseConnection();
                return;
            }

            ParseConnectionAddress(request.SessionInfo.ConnectionAddress);
            SessionInfo = request.SessionInfo;
            ProcessCode = SessionInfo.ProcessCode;
            Name = SessionInfo.UserName ?? "ATLAS";
            IsPrivate = request.isPrivate;
            Fingerprint = fingerprint;
            _pendingCallbackId = callbackId;

            if (GameServerKeyManager.IsKeyInUse(fingerprint, ProcessCode))
            {
                log.Warn($"Rejecting game server {Name} ({fingerprint}): key already in use by another connection");
                RejectRegistration();
                return;
            }

            GameServerKeyStatus status = GameServerKeyManager.RegisterConnection(
                fingerprint, request.PublicKey, Address, BuildVersion);

            switch (status)
            {
                case GameServerKeyStatus.Approved:
                    CompleteRegistration();
                    break;
                case GameServerKeyStatus.Pending:
                    log.Info($"Game server {Name} ({fingerprint}) is awaiting admin approval");
                    GameServerKeyManager.AddPending(fingerprint, this);
                    // Keep the connection open; registration completes when an admin approves.
                    break;
                default: // Declined / Revoked
                    log.Warn($"Rejecting game server {Name} ({fingerprint}): key status {status}");
                    RejectRegistration();
                    break;
            }
        }

        public void CompleteRegistration()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            ServerManager.AddServer(this);
            Send(new RegisterGameServerResponse { Success = true }, _pendingCallbackId);
        }

        public void RejectRegistration()
        {
            Send(new RegisterGameServerResponse { Success = false }, _pendingCallbackId);
            CloseConnection();
        }

        private static byte[] DecodeSignature(string signature)
        {
            if (string.IsNullOrEmpty(signature))
            {
                return null;
            }
            try
            {
                return Convert.FromBase64String(signature);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private void ParseConnectionAddress(string address)
        {
            string[] parts = address.Split("://");
            
            string hostPort;
            if (parts.Length == 1)
            {
                ProtocolStr = "ws";
                hostPort = address;
            }
            else
            {
                ProtocolStr = parts[0];
                hostPort = parts[1];
            }

            string[] hostPortParts = hostPort.Split(":");
            Address = hostPortParts[0];
            Port = Convert.ToInt32(hostPortParts[1]);
            AddressForLog = Address.Truncate(16);
        }

        private void HandleServerGameSummaryNotification(ServerGameSummaryNotification notify)
        {
            OnGameEnded(this, notify.GameSummary, notify.GameSummaryOverrides);
        }

        private void HandlePlayerDisconnectedNotification(PlayerDisconnectedNotification request)
        {
            OnPlayerDisconnect(this, request.PlayerInfo, request.SessionInfo);
        }

        private void HandleServerGameMetricsNotification(ServerGameMetricsNotification request)
        {
            if (request.GameMetrics is null)
            {
                log.Error("Invalid game metrics notification");
                return;
            }
            OnGameMetricsUpdate(this, request.GameMetrics);
        }

        private void HandleServerGameStatusNotification(ServerGameStatusNotification request)
        {
            OnStatusUpdate(this, request.GameStatus);
        }

        private void HandleMonitorHeartbeatNotification(MonitorHeartbeatNotification notify)
        {

        }

        private void HandleLaunchGameResponse(LaunchGameResponse response)
        {
            log.Info(
                $"Game {response.GameInfo?.Name} launched ({response.GameServerAddress}, {response.GameInfo?.GameStatus}) " +
                $"with {response.GameInfo?.ActiveHumanPlayers} players");
        }

        private void HandleJoinGameServerResponse(JoinGameServerResponse response)
        {
            log.Info(
                $"Player {response.PlayerInfo?.Handle} {response.PlayerInfo?.AccountId} {response.PlayerInfo?.CharacterType} " +
                $"joined {response.GameServerProcessCode}");
        }

        private void HandleReconnectPlayerResponse(ReconnectPlayerResponse response)
        {
            if (!response.Success)
            {
                log.Error("Reconnecting player is not found on the server");
            }
        }

        protected override void HandleClose(WsCloseEventArgs e)
        {
            UnregisterAllHandlers();
            GameServerKeyManager.RemovePending(this);
            ServerManager.RemoveServer(ProcessCode);
            OnServerDisconnect(this);
        }

        public bool IsAvailable()
        {
            return !IsReserved && !IsPrivate && IsConnected;
        }

        public void ReserveForGame()
        {
            IsReserved = true;
            // TODO release if game did not start?
        }

        public void StartGameForReconnection(long accountId)
        {
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);
            Send(new ReconnectPlayerRequest
            {
                AccountId = accountId,
                NewSessionId = sessionInfo.SessionToken
            });
        }

        public void LaunchGame(LobbyGameInfo GameInfo, LobbyServerTeamInfo TeamInfo, Dictionary<int, LobbySessionInfo> sessionInfos)
        {
            Send(new LaunchGameRequest()
            {
                GameInfo = GameInfo,
                TeamInfo = TeamInfo,
                SessionInfo = sessionInfos,
                GameplayOverrides = GameConfig.GetGameplayOverrides()
            });
        }

        public bool Send(AllianceMessageBase msg, int originalCallbackId = 0)
        {
            return Wrap(SendImpl, msg, originalCallbackId);
        }

        private bool SendImpl(AllianceMessageBase msg, int originalCallbackId)
        {
            short messageType = BridgeMessageSerializer.GetMessageType(msg);
            if (messageType >= 0)
            {
                LogMessage(">", msg);
                Send(messageType, msg, originalCallbackId);
                return true;
            }
            log.Error($"No sender for {msg.GetType().Name}");
            LogMessage(">X", msg);

            return false;
        }

        private void Send(short msgType, AllianceMessageBase msg, int originalCallbackId = 0)
        {
            Send(BridgeMessageSerializer.SerializeMessage(msgType, msg, originalCallbackId));
        }

        public void Shutdown()
        {
            Send(new ShutdownGameRequest());
        }

        public void AdminShutdown(GameResult gameResult)
        {
            Send(new AdminShutdownGameRequest()
            {
                GameResult = gameResult
            });
        }

        public void AdminClearCooldown()
        {
            Send(new AdminClearCooldownsRequest());
        }

        public void DisconnectPlayer(LobbyServerPlayerInfo playerInfo)
        {
            Send(new DisconnectPlayerRequest
            {
                SessionInfo = SessionManager.GetSessionInfo(playerInfo.AccountId),
                PlayerInfo = playerInfo,
                GameResult = GameResult.ClientLeft
            });
        }
    }
}