using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        
        public event Func<BridgeServerProtocol, LobbyGameSummary, LobbyGameSummaryOverrides, Task> OnGameEnded = delegate { return Task.CompletedTask; };
        public event Func<BridgeServerProtocol, GameStatus, Task> OnStatusUpdate = delegate { return Task.CompletedTask; };
        public event Func<BridgeServerProtocol, ServerGameMetrics, Task> OnGameMetricsUpdate = delegate { return Task.CompletedTask; };
        public event Func<BridgeServerProtocol, LobbyServerPlayerInfo, LobbySessionInfo, Task> OnPlayerDisconnect = delegate { return Task.CompletedTask; };
        public event Func<BridgeServerProtocol, Task> OnServerDisconnect = delegate { return Task.CompletedTask; };

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

        protected override bool SerializeMessage(MemoryStream stream, AllianceMessageBase message, int callbackId)
        {
            short messageType = BridgeMessageSerializer.GetMessageType(message);
            if (messageType >= 0)
            {
                stream.Write(BridgeMessageSerializer.SerializeMessage(messageType, message, callbackId));
                return true;
            }

            return false;
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

        private Task HandleRegisterGameServerRequest(RegisterGameServerRequest request, int callbackId)
        {
            string data = request.SessionInfo.ConnectionAddress;
            Address = data.Split(":")[0];
            Port = Convert.ToInt32(data.Split(":")[1]);
            SessionInfo = request.SessionInfo;
            ProcessCode = SessionInfo.ProcessCode;
            Name = SessionInfo.UserName ?? "ATLAS";
            IsPrivate = request.isPrivate;
            ServerManager.AddServer(this);

            return Send(new RegisterGameServerResponse
                {
                    Success = true
                },
                callbackId);
        }

        private Task HandleServerGameSummaryNotification(ServerGameSummaryNotification notify)
        {
            return OnGameEnded(this, notify.GameSummary, notify.GameSummaryOverrides);
        }

        private Task HandlePlayerDisconnectedNotification(PlayerDisconnectedNotification request)
        {
            return OnPlayerDisconnect(this, request.PlayerInfo, request.SessionInfo);
        }

        private Task HandleServerGameMetricsNotification(ServerGameMetricsNotification request)
        {
            if (request.GameMetrics is null)
            {
                log.Error("Invalid game metrics notification");
                return Task.CompletedTask;
            }

            return OnGameMetricsUpdate(this, request.GameMetrics);
        }

        private Task HandleServerGameStatusNotification(ServerGameStatusNotification request)
        {
            return OnStatusUpdate(this, request.GameStatus);
        }

        private Task HandleMonitorHeartbeatNotification(MonitorHeartbeatNotification notify)
        {
            return Task.CompletedTask;
        }

        private Task HandleLaunchGameResponse(LaunchGameResponse response)
        {
            log.Info(
                $"Game {response.GameInfo?.Name} launched ({response.GameServerAddress}, {response.GameInfo?.GameStatus}) " +
                $"with {response.GameInfo?.ActiveHumanPlayers} players");
            return Task.CompletedTask;
        }

        private Task HandleJoinGameServerResponse(JoinGameServerResponse response)
        {
            log.Info(
                $"Player {response.PlayerInfo?.Handle} {response.PlayerInfo?.AccountId} {response.PlayerInfo?.CharacterType} " +
                $"joined {response.GameServerProcessCode}");
            return Task.CompletedTask;
        }

        private Task HandleReconnectPlayerResponse(ReconnectPlayerResponse response)
        {
            if (!response.Success)
            {
                log.Error("Reconnecting player is not found on the server");
            }

            return Task.CompletedTask;
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

        public Task StartGameForReconnection(long accountId)
        {
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);
            return Send(new ReconnectPlayerRequest
            {
                AccountId = accountId,
                NewSessionId = sessionInfo.SessionToken
            });
        }

        public Task LaunchGame(LobbyGameInfo GameInfo, LobbyServerTeamInfo TeamInfo, Dictionary<int, LobbySessionInfo> sessionInfos)
        {
            return Send(new LaunchGameRequest()
            {
                GameInfo = GameInfo,
                TeamInfo = TeamInfo,
                SessionInfo = sessionInfos,
                GameplayOverrides = GameConfig.GetGameplayOverrides()
            });
        }

        public Task Shutdown()
        {
            return Send(new ShutdownGameRequest());
        }

        public Task DisconnectPlayer(LobbyServerPlayerInfo playerInfo)
        {
            return Send(new DisconnectPlayerRequest
            {
                SessionInfo = SessionManager.GetSessionInfo(playerInfo.AccountId),
                PlayerInfo = playerInfo,
                GameResult = GameResult.ClientLeft
            });
        }
    }
}