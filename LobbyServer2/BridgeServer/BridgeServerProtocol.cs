using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;
using WebSocketSharp;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol: WebSocketBehaviorBase<AllianceMessageBase>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeServerProtocol));
        
        public event Action<BridgeServerProtocol, LobbyGameSummary, LobbyGameSummaryOverrides> OnGameEnded = delegate {};
        public event Action<BridgeServerProtocol, GameStatus> OnStatusUpdate = delegate {};
        public event Action<BridgeServerProtocol, ServerGameMetrics> OnGameMetricsUpdate = delegate {};
        public event Action<BridgeServerProtocol, LobbyServerPlayerInfo, LobbySessionInfo> OnPlayerDisconnect = delegate {};
        public event Action<BridgeServerProtocol> OnServerDisconnect = delegate {};

        public string Address;
        public int Port;
        private LobbySessionInfo SessionInfo;

        public string URI => "ws://" + Address + ":" + Port;
        public string BuildVersion => SessionInfo?.BuildVersion ?? "";
        public bool IsPrivate { get; private set; }
        public bool IsReserved { get; private set; }

        public string ProcessCode { set; get; }
        public string Name { protected set; get; }
        
        protected override AllianceMessageBase DeserializeMessage(byte[] data, out int callbackId)
        {
            return BridgeMessageSerializer.DeserializeMessage(data, out callbackId);
        }
        
        protected override string GetConnContext()
        {
            return $"S {Address}:{Port}";
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

        private void HandleRegisterGameServerRequest(RegisterGameServerRequest request, int callbackId)
        {
            string data = request.SessionInfo.ConnectionAddress;
            Address = data.Split(":")[0];
            Port = Convert.ToInt32(data.Split(":")[1]);
            SessionInfo = request.SessionInfo;
            ProcessCode = SessionInfo.ProcessCode;
            Name = SessionInfo.UserName ?? "ATLAS";
            IsPrivate = request.isPrivate;
            ServerManager.AddServer(this);

            Send(new RegisterGameServerResponse
            {
                Success = true
            },
                callbackId);
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

        protected override void HandleClose(CloseEventArgs e)
        {
            UnregisterAllHandlers();
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
            short messageType = BridgeMessageSerializer.GetMessageType(msg);
            if (messageType >= 0)
            {
                Send(messageType, msg, originalCallbackId);
                LogMessage(">", msg);
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