using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CentralServer.BridgeServer
{
    public class BridgeServerProtocol : WebSocketBehavior
    {
        private static readonly string PATH = Path.GetTempPath() + @"atlas-reactor-hc-server-game.json";

        public string Address;
        public int Port;
        private LobbyGameInfo GameInfo;
        private LobbyTeamInfo TeamInfo;
        private GameStatus GameStatus = GameStatus.Stopped;

        public static readonly List<Type> BridgeMessageTypes = new List<Type>
        {
            typeof(RegisterGameServerRequest),
            typeof(RegisterGameServerResponse),
            // typeof(LaunchGameRequest),
            // typeof(JoinGameServerRequest),
            // typeof(JoinGameAsObserverRequest),
            // typeof(ShutdownGameRequest),
            // typeof(DisconnectPlayerRequest),
            // typeof(ReconnectPlayerRequest),
            // typeof(MonitorHeartbeatResponse),
            // typeof(ServerGameSummaryNotification),
            // typeof(PlayerDisconnectedNotification)
        };

        protected List<Type> GetMessageTypes()
        {
            return BridgeMessageTypes;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            NetworkReader networkReader = new NetworkReader(e.RawData);
            short messageType = networkReader.ReadInt16();
            int callbackId = networkReader.ReadInt32();
            List<Type> messageTypes = GetMessageTypes();
            if (messageType >= messageTypes.Count)
            {
                Log.Print(LogType.Error, $"Unknown bridge message type {messageType}");
                return;
            }

            Type type = messageTypes[messageType];

            if (type == typeof(RegisterGameServerRequest))
            {
                RegisterGameServerRequest request = Deserialize<RegisterGameServerRequest>(networkReader);
                string data = request.SessionInfo.ConnectionAddress;
                Address = data.Split(":")[0];
                Port = Convert.ToInt32(data.Split(":")[1]);
                ServerManager.AddServer(this);

                Send(new RegisterGameServerResponse
                    {
                        Success = true
                    },
                    callbackId);
            }
            else
            {
                Log.Print(LogType.Game, $"Received unhandled bridge message type {type.Name}");
            }
        }

        private T Deserialize<T>(NetworkReader reader) where T : AllianceMessageBase
        {
            ConstructorInfo constructor = typeof(T).GetConstructor(Type.EmptyTypes);
            T o = (T)(AllianceMessageBase)constructor.Invoke(Array.Empty<object>());
            o.Deserialize(reader);
            return o;
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            ServerManager.RemoveServer(this.ID);
        }

        public bool IsAvailable()
        {
            return GameStatus == GameStatus.Stopped;
        }

        public void StartGame(LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
        {
            GameInfo = gameInfo;
            TeamInfo = teamInfo;
            GameStatus = GameStatus.Assembling;

            WriteGame(gameInfo, teamInfo);
        }

        public void WriteGame(LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
        {
            var _data = new ServerGame()
            {
                gameInfo = gameInfo,
                teamInfo = teamInfo
            };
            using StreamWriter file = File.CreateText(PATH);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, _data);
            Log.Print(LogType.Game, $"Setting Game Info at {PATH}");
        }

        private ReadOnlySpan<byte> GetBytesSpan(string str)
        {
            return new ReadOnlySpan<byte>(Encoding.GetEncoding("UTF-8").GetBytes(str));
        }

        public bool Send(AllianceMessageBase msg, int originalCallbackId = 0)
        {
            short messageType = GetMessageType(msg);
            if (messageType >= 0)
            {
                Send(messageType, msg, originalCallbackId);
                return true;
            }

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
                Log.Print(LogType.Error, $"Message type {msg.GetType().Name} is not in the MonitorGameServerInsightMessages MessageTypes list and doesnt have a type");
            }

            return num;
        }

        class ServerGame
        {
            public LobbyGameInfo gameInfo;
            public LobbyTeamInfo teamInfo;
        }
    }
}