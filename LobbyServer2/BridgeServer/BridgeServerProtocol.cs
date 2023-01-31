﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using log4net;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol : WebSocketBehaviorBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BridgeServerProtocol));
        
        public string Address;
        public int Port;
        private LobbySessionInfo SessionInfo;
        public LobbyGameInfo GameInfo { private set; get; }
        private LobbyServerTeamInfo TeamInfo;
        public List<LobbyServerProtocol> clients = new List<LobbyServerProtocol>();
        public string URI => "ws://" + Address + ":" + Port;
        public GameStatus GameStatus { get; private set; } = GameStatus.Stopped;
        public string ProcessCode { get; } = "Artemis" + DateTime.Now.Ticks;
        public string Name => SessionInfo?.UserName ?? "ATLAS";
        public string BuildVersion { get; private set; } = "";

        public LobbyServerPlayerInfo GetServerPlayerInfo(long accountId)
        {
            return TeamInfo.TeamPlayerInfo.Find(p => p.AccountId == accountId);
        }

        public IEnumerable<long> GetPlayers(Team team)
        {
            return from p in TeamInfo.TeamInfo(team) select p.AccountId;
        }

        public IEnumerable<long> GetPlayers()
        {
            return from p in TeamInfo.TeamPlayerInfo select p.AccountId;
        }

        public static readonly List<Type> BridgeMessageTypes = new List<Type>
        {
            typeof(RegisterGameServerRequest),
            typeof(RegisterGameServerResponse),
            typeof(LaunchGameRequest),
            typeof(JoinGameServerRequest),
            null, // typeof(JoinGameAsObserverRequest),
            typeof(ShutdownGameRequest),
            null, // typeof(DisconnectPlayerRequest),
            null, // typeof(ReconnectPlayerRequest),
            null, // typeof(MonitorHeartbeatResponse),
            typeof(ServerGameSummaryNotification),
            typeof(PlayerDisconnectedNotification),
            null, // typeof(ServerGameMetricsNotification),
            typeof(ServerGameStatusNotification),
            null, // typeof(MonitorHeartbeatNotification),
            null, // typeof(LaunchGameResponse),
            null, // typeof(JoinGameServerResponse),
            null, // typeof(JoinGameAsObserverResponse)
        };
        
        protected List<Type> GetMessageTypes()
        {
            return BridgeMessageTypes;
        }

        protected override string GetConnContext()
        {
            return $"S {Address}:{Port}";
        }

        protected override void HandleMessage(MessageEventArgs e)
        {
            NetworkReader networkReader = new NetworkReader(e.RawData);
            short messageType = networkReader.ReadInt16();
            int callbackId = networkReader.ReadInt32();
            List<Type> messageTypes = GetMessageTypes();
            if (messageType >= messageTypes.Count)
            {
                log.Error($"Unknown bridge message type {messageType}");
                return;
            }

            Type type = messageTypes[messageType];

            if (type == typeof(RegisterGameServerRequest))
            {
                RegisterGameServerRequest request = Deserialize<RegisterGameServerRequest>(networkReader);
                log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                string data = request.SessionInfo.ConnectionAddress;
                Address = data.Split(":")[0];
                Port = Convert.ToInt32(data.Split(":")[1]);
                SessionInfo = request.SessionInfo;
                BuildVersion = GetChangelistNumberFromFullVersionString(SessionInfo);
                ServerManager.AddServer(this);

                Send(new RegisterGameServerResponse
                    {
                        Success = true
                    },
                    callbackId);
            }
            else if (type == typeof(ServerGameSummaryNotification))
            {
                try 
                {
                    ServerGameSummaryNotification request = Deserialize<ServerGameSummaryNotification>(networkReader);

                    if (request.GameSummary == null) request.GameSummary = new LobbyGameSummary();
                    if (request.GameSummary.BadgeAndParticipantsInfo == null) request.GameSummary.BadgeAndParticipantsInfo = new List<BadgeAndParticipantInfo>();
                    log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                    log.Info($"Game {GameInfo.Name} at {request.GameSummary.GameServerAddress} finished " +
                                            $"({request.GameSummary.NumOfTurns} turns), " +
                                            $"{request.GameSummary.GameResult} {request.GameSummary.TeamAPoints}-{request.GameSummary.TeamBPoints}");
                    foreach (LobbyServerProtocolBase client in clients)
                    {
                        MatchResultsNotification response = new MatchResultsNotification
                        {
                            // TODO
                            BadgeAndParticipantsInfo = request.GameSummary.BadgeAndParticipantsInfo
                        };
                        client.Send(response);
                    }
                } catch(NullReferenceException ex)
                {
                    log.Error(ex);
                }
                

                Send(new ShutdownGameRequest());
            }
            else if (type == typeof(PlayerDisconnectedNotification))
            {
                PlayerDisconnectedNotification request = Deserialize<PlayerDisconnectedNotification>(networkReader);
                log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                log.Info($"Player {request.PlayerInfo.AccountId} left game {GameInfo.GameServerProcessCode}");
                
                foreach (LobbyServerProtocol client in clients)
                {
                    if (client.AccountId == request.PlayerInfo.AccountId)
                    {
                        client.CurrentServer = null;
                        break;
                    }
                }
            }
            else if (type == typeof(ServerGameStatusNotification))
            {
                ServerGameStatusNotification request = Deserialize<ServerGameStatusNotification>(networkReader);
                log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                log.Info($"Game {GameInfo.Name} {request.GameStatus}");
                GameStatus = request.GameStatus;
                if (GameStatus == GameStatus.Stopped)
                {
                    foreach (LobbyServerProtocol client in clients)
                    {
                        client.CurrentServer = null;
                    }
                }
            }
            else
            {
                log.Warn($"Received unhandled bridge message type {(type != null ? type.Name : "id_" + messageType)}");
            }
        }

        private T Deserialize<T>(NetworkReader reader) where T : AllianceMessageBase
        {
            ConstructorInfo constructor = typeof(T).GetConstructor(Type.EmptyTypes);
            T o = (T)(AllianceMessageBase)constructor.Invoke(Array.Empty<object>());
            o.Deserialize(reader);
            return o;
        }

        protected override void HandleClose(CloseEventArgs e)
        {
            ServerManager.RemoveServer(this.ID);
        }

        public bool IsAvailable()
        {
            return GameStatus == GameStatus.Stopped;
        }

        public void StartGame(LobbyGameInfo gameInfo, LobbyServerTeamInfo teamInfo)
        {
            GameInfo = gameInfo;
            TeamInfo = teamInfo;
            GameStatus = GameStatus.Assembling;
            Dictionary<int, LobbySessionInfo> sessionInfos = teamInfo.TeamPlayerInfo
                .ToDictionary(
                    playerInfo => playerInfo.PlayerId,
                    playerInfo => SessionManager.GetSessionInfo(playerInfo.AccountId) ?? new LobbySessionInfo());  // fallback for bots TODO something smarter

            foreach (LobbyServerPlayerInfo playerInfo in teamInfo.TeamPlayerInfo)
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
                GameInfo = gameInfo,
                TeamInfo = teamInfo,
                SessionInfo = sessionInfos,
                GameplayOverrides = new LobbyGameplayOverrides()
            });
        }

        public bool Send(AllianceMessageBase msg, int originalCallbackId = 0)
        {
            short messageType = GetMessageType(msg);
            if (messageType >= 0)
            {
                Send(messageType, msg, originalCallbackId);
                log.Debug($"> {msg.GetType().Name} {DefaultJsonSerializer.Serialize(msg)}");
                return true;
            }
            log.Error($"No sender for {msg.GetType().Name}");
            log.Debug($">X {msg.GetType().Name} {DefaultJsonSerializer.Serialize(msg)}");

            return false;
        }

        private bool Send(short msgType, AllianceMessageBase msg, int originalCallbackId = 0)
        {
            NetworkWriter networkWriter = new NetworkWriter();
            networkWriter.Write(msgType);
            networkWriter.Write(originalCallbackId);
            msg.Serialize(networkWriter);
            Send(networkWriter.ToArray());
            return true;
        }

        public short GetMessageType(AllianceMessageBase msg)
        {
            short num = (short)GetMessageTypes().IndexOf(msg.GetType());
            if (num < 0)
            {
                log.Error($"Message type {msg.GetType().Name} is not in the MonitorGameServerInsightMessages MessageTypes list and doesnt have a type");
            }

            return num;
        }

        private static string GetChangelistNumberFromFullVersionString(LobbySessionInfo sessionInfo)
        {
            // see BuildVersion#FullVersionString in hc
            string[] buildVersionParts = sessionInfo.BuildVersion.Split('-', 5);
            return buildVersionParts.Length >= 5 ? buildVersionParts[4] : "";
        }
    }
}