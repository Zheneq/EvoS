using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using WebSocketSharp;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol : WebSocketBehaviorBase<AllianceMessageBase>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeServerProtocol));
        
        public string Address;
        public int Port;
        private LobbySessionInfo SessionInfo;
        private MatchOrchestrator Orchestrator;
        public LobbyGameInfo GameInfo { private set; get; }
        public LobbyServerTeamInfo TeamInfo { private set; get; } = new LobbyServerTeamInfo() { TeamPlayerInfo = new List<LobbyServerPlayerInfo>() };

        public string URI => "ws://" + Address + ":" + Port;
        
        // TODO sync with GameInfo.GameStatus or get rid of it (GameInfo can be null)
        public GameStatus ServerGameStatus { get; private set; } = GameStatus.None;
        public string ProcessCode { get; private set; }
        public string Name => SessionInfo?.UserName ?? "ATLAS";
        public string BuildVersion => SessionInfo?.BuildVersion ?? "";
        public bool IsPrivate { get; private set; }

        public LobbyServerPlayerInfo GetPlayerInfo(long accountId)
        {
            return TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == accountId);
        }

        public void ForceReady()
        {
            TeamInfo.TeamPlayerInfo.ForEach(p => p.ReadyState = ReadyState.Ready);
        }

        public IEnumerable<long> GetPlayers(Team team)
        {
            return from p in TeamInfo.TeamInfo(team) select p.AccountId;
        }

        public IEnumerable<long> GetPlayers()
        {
            return from p in TeamInfo.TeamPlayerInfo select p.AccountId;
        }

        public List<LobbyServerProtocol> GetClients()
        {
            List<LobbyServerProtocol> clients = new List<LobbyServerProtocol>();

            // If we don't have any player in teams, return an empty list
            if (TeamInfo == null || TeamInfo.TeamPlayerInfo == null) return clients;

            foreach (LobbyServerPlayerInfo player in TeamInfo.TeamPlayerInfo)
            {
                if (player.IsSpectator || player.IsNPCBot || player.ReplacedWithBots) continue;
                LobbyServerProtocol client = SessionManager.GetClientConnection(player.AccountId);
                if (client != null) clients.Add(client);
            }

            return clients;
        }

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
            Orchestrator = new MatchOrchestrator(this);
            
            RegisterHandler<RegisterGameServerRequest>(HandleRegisterGameServerRequest);
            RegisterHandler<ServerGameSummaryNotification>(HandleServerGameSummaryNotification);
            RegisterHandler<PlayerDisconnectedNotification>(HandlePlayerDisconnectedNotification);
            RegisterHandler<DisconnectPlayerRequest>(HandleDisconnectPlayerRequest);
            RegisterHandler<ReconnectPlayerRequest>(HandleReconnectPlayerRequest);
            RegisterHandler<ServerGameMetricsNotification>(HandleServerGameMetricsNotification);
            RegisterHandler<ServerGameStatusNotification>(HandleServerGameStatusNotification);
            RegisterHandler<MonitorHeartbeatNotification>(HandleMonitorHeartbeatNotification);
            RegisterHandler<LaunchGameResponse>(HandleLaunchGameResponse);
            RegisterHandler<JoinGameServerResponse>(HandleJoinGameServerResponse);
        }

        private void HandleRegisterGameServerRequest(RegisterGameServerRequest request, int callbackId)
        {
            string data = request.SessionInfo.ConnectionAddress;
            Address = data.Split(":")[0];
            Port = Convert.ToInt32(data.Split(":")[1]);
            SessionInfo = request.SessionInfo;
            IsPrivate = request.isPrivate;
            ProcessCode = $"{Name}-{Guid.NewGuid()}";
            ServerManager.AddServer(this);

            Send(new RegisterGameServerResponse
                {
                    Success = true
                },
                callbackId);
        }

        private void HandleServerGameSummaryNotification(ServerGameSummaryNotification notify)
        {
            LobbyGameSummary gameSummary = notify.GameSummary;
            if (gameSummary == null)
            {
                GameInfo.GameResult = GameResult.TieGame;
                gameSummary = new LobbyGameSummary();
            }
            else
            {
                GameInfo.GameResult = gameSummary.GameResult;
            }

            log.Info($"Game {GameInfo?.Name} at {gameSummary?.GameServerAddress} finished " +
                     $"({gameSummary.NumOfTurns} turns), " +
                     $"{gameSummary.GameResult} {gameSummary.TeamAPoints}-{gameSummary.TeamBPoints}");

            try
            {
                gameSummary.BadgeAndParticipantsInfo = AccoladeUtils.ProcessGameSummary(gameSummary);
            }
            catch (Exception ex)
            {
                log.Error("Failed to process game summary", ex);
            }
            
            DB.Get().MatchHistoryDao.Save(MatchHistoryDao.MatchEntry.Cons(GameInfo, gameSummary));
            
            ServerGameStatus = GameStatus.Stopped;

            _ = Orchestrator.FinalizeGame(gameSummary);
        }

        private void HandlePlayerDisconnectedNotification(PlayerDisconnectedNotification request)
        {
            log.Info($"Player {request.PlayerInfo.AccountId} left game {GameInfo?.GameServerProcessCode}");

            foreach (LobbyServerProtocol client in GetClients())
            {
                if (client.AccountId == request.PlayerInfo.AccountId)
                {
                    client.LeaveServer(this);
                    break;
                }
            }

            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(request.PlayerInfo.AccountId);
            if (playerInfo != null)
            {
                playerInfo.ReplacedWithBots = true;
            }
        }

        private void HandleDisconnectPlayerRequest(DisconnectPlayerRequest request)
        {
            log.Info($"Sending Disconnect player Request for accountId {request.PlayerInfo.AccountId}");
        }

        private void HandleReconnectPlayerRequest(ReconnectPlayerRequest request)
        {
            log.Info($"Sending reconnect player Request for accountId {request.AccountId} with reconectionsession id {request.NewSessionId}");
        }

        private void HandleServerGameMetricsNotification(ServerGameMetricsNotification request)
        {
            log.Info($"Game {GameInfo?.Name} Turn {request.GameMetrics?.CurrentTurn}, " +
                     $"{request.GameMetrics?.TeamAPoints}-{request.GameMetrics?.TeamBPoints}, " +
                     $"frame time: {request.GameMetrics?.AverageFrameTime}");
        }

        private void HandleServerGameStatusNotification(ServerGameStatusNotification request)
        {
            log.Info($"Game {GameInfo?.Name} {request.GameStatus}");

            ServerGameStatus = request.GameStatus;

            if (ServerGameStatus == GameStatus.Stopped)
            {
                foreach (LobbyServerProtocol client in GetClients())
                {
                    if (!client.LeaveServer(this))
                    {
                        continue;
                    }

                    if (GameInfo != null)
                    {
                        //Unready people when game is finisht
                        ForceMatchmakingQueueNotification forceMatchmakingQueueNotification =
                            new ForceMatchmakingQueueNotification()
                            {
                                Action = ForceMatchmakingQueueNotification.ActionType.Leave,
                                GameType = GameInfo.GameConfig.GameType
                            };
                        client.Send(forceMatchmakingQueueNotification);
                    }

                    // Set client back to previus CharacterType
                    if (GetPlayerInfo(client.AccountId).CharacterType != client.OldCharacter)
                    {
                        ResetCharacterToOriginal(client);
                    }

                    client.OldCharacter = CharacterType.None;
                }
            }
        }

        private void HandleMonitorHeartbeatNotification(MonitorHeartbeatNotification notify)
        {
            
        }

        private void HandleLaunchGameResponse(LaunchGameResponse response)
        {
            log.Info(
                $"Game {GameInfo?.Name} launched ({response.GameServerAddress}, {response.GameInfo?.GameStatus}) with {response.GameInfo?.ActiveHumanPlayers} players");
        }

        private void HandleJoinGameServerResponse(JoinGameServerResponse response)
        {
            log.Info(
                $"Player {response.PlayerInfo?.Handle} {response.PlayerInfo?.AccountId} {response.PlayerInfo?.CharacterType} " +
                $"joined {GameInfo?.Name}  ({response.GameServerProcessCode})");
        }

        protected override void HandleClose(CloseEventArgs e)
        {
            UnregisterAllHandlers();
            ServerManager.RemoveServer(ProcessCode);
        }

        public void OnPlayerUsedGGPack(long accountId)
        {
            GameInfo.ggPackUsedAccountIDs.TryGetValue(accountId, out int ggPackUsedAccountIDs);
            GameInfo.ggPackUsedAccountIDs[accountId] = ggPackUsedAccountIDs + 1;
        }

        public bool IsAvailable()
        {
            return ServerGameStatus == GameStatus.None && !IsPrivate && IsConnected;
        }

        public void ReserveForGame()
        {
            ServerGameStatus = GameStatus.Assembling;
            // TODO release if game did not start?
        }

        public async Task StartGameAsync(List<long> teamA, List<long> teamB, GameType gameType, GameSubType gameSubType)
        {
            await Orchestrator.StartGameAsync(teamA, teamB, gameType, gameSubType);
        }

        public void StartGameForReconection(long accountId)
        {
            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(accountId);
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);

            //Can we modify ReconnectPlayerRequest and send the a new SessionToken to?
            ReconnectPlayerRequest reconnectPlayerRequest = new ReconnectPlayerRequest()
            {
                AccountId = accountId,
                NewSessionId = sessionInfo.ReconnectSessionToken
            };

            Send(reconnectPlayerRequest);

            JoinGameServerRequest request = new JoinGameServerRequest
            {
                OrigRequestId = 0,
                GameServerProcessCode = GameInfo.GameServerProcessCode,
                PlayerInfo = playerInfo,
                SessionInfo = sessionInfo
            };
            Send(request);
        }

        public void StartGame()
        {
            ServerGameStatus = GameStatus.Assembling;
            Dictionary<int, LobbySessionInfo> sessionInfos = TeamInfo.TeamPlayerInfo
                .ToDictionary(
                    playerInfo => playerInfo.PlayerId,
                    playerInfo => SessionManager.GetSessionInfo(playerInfo.AccountId) ?? new LobbySessionInfo());  // fallback for bots TODO something smarter

            foreach (LobbyServerPlayerInfo playerInfo in TeamInfo.TeamPlayerInfo)
            {
                LobbySessionInfo sessionInfo = sessionInfos[playerInfo.PlayerId];
                JoinGameServerRequest request = new JoinGameServerRequest
                {
                    OrigRequestId = 0,
                    GameServerProcessCode = GameInfo.GameServerProcessCode,
                    PlayerInfo = playerInfo,
                    SessionInfo = sessionInfo
                };
                Send(request);
            }

            Send(new LaunchGameRequest()
            {
                GameInfo = GameInfo,
                TeamInfo = TeamInfo,
                SessionInfo = sessionInfos,
                GameplayOverrides = new LobbyGameplayOverrides()
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

        public void BuildGameInfo(GameType gameType, GameSubType gameMode)
        {
            int playerCount = GetClients().Count;
            GameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = playerCount,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                SelectTimeout = TimeSpan.FromSeconds(30),
                LoadoutSelectTimeout = TimeSpan.FromSeconds(30),
                ActiveHumanPlayers = playerCount,
                ActivePlayers = playerCount,
                CreateTimestamp = DateTime.UtcNow.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = MatchmakingQueue.SelectMap(gameMode),
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = TeamInfo.TeamAPlayerInfo.Count(),
                    TeamBBots = 0,
                    TeamBPlayers = TeamInfo.TeamBPlayerInfo.Count(),
                },
                GameResult = GameResult.NoResult,
                GameServerAddress = this.URI,
                GameServerProcessCode = this.ProcessCode
            };
        }

        public void SetGameStatus(GameStatus status)
        {
            GameInfo.GameStatus = status;
        }

        public void SendGameInfoNotifications()
        {
            foreach (long player in GetPlayers())
            {
                LobbyServerProtocol playerConnection = SessionManager.GetClientConnection(player);
                if (playerConnection != null)
                {
                    SendGameInfo(playerConnection);
                }
            }
        }

        public void SendGameInfo(LobbyServerProtocol playerConnection, GameStatus gamestatus = GameStatus.None)
        {
            if (gamestatus != GameStatus.None)
            {
                GameInfo.GameStatus = gamestatus;
            }

            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(playerConnection.AccountId);
            GameInfoNotification notification = new GameInfoNotification
            {
                GameInfo = GameInfo,
                TeamInfo = LobbyTeamInfo.FromServer(TeamInfo, 0, new MatchmakingQueueConfig()),
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig())
            };

            playerConnection.Send(notification);
        }

        public void SendGameAssignmentNotification(LobbyServerProtocol client, bool reconnection = false)
        {
            LobbyServerPlayerInfo playerInfo = GetPlayerInfo(client.AccountId);
            GameAssignmentNotification notification = new GameAssignmentNotification
            {
                GameInfo = GameInfo,
                GameResult = GameInfo.GameResult,
                Observer = false,
                PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
                Reconnection = reconnection,
                GameplayOverrides = client.GetGameplayOverrides()
            };

            client.Send(notification);
        }

        public void ResetCharacterToOriginal(LobbyServerProtocol playerConnection, bool isDisconnected = false) 
        {
            Orchestrator.ResetCharacterToOriginal(playerConnection, isDisconnected);
        }

        public bool UpdateCharacterInfo(long accountId, LobbyCharacterInfo characterInfo, LobbyPlayerInfoUpdate update)
        {
            return Orchestrator.UpdateCharacterInfo(accountId, characterInfo, update);
        }

        public void OnAccountVisualsUpdated(long accountId)
        {
            Orchestrator.UpdateAccountVisuals(accountId);
        }

        public void SetPlayerReady(long accountId)
        {
            GetPlayerInfo(accountId).ReadyState = ReadyState.Ready;
        }
    }
}