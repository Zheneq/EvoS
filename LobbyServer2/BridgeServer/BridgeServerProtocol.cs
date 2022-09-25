using System;
using System.IO;
using System.Text;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;
using StreamReader = System.IO.StreamReader;

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
        public string URI => "ws://" + Address + ":" + Port;

        public enum BridgeMessageType
        {
            InitialConfig,
            SetLobbyGameInfo,
            SetTeamInfo,
            Start,
            Stop,
            GameStatusChange,
            PlayerLeaving
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            MemoryStream stream = new MemoryStream(e.RawData);
            string data = "";

            BridgeMessageType messageType;
            
            using (StreamReader reader = new StreamReader(stream))
            {
                messageType = (BridgeMessageType)reader.Read();
                data = reader.ReadToEnd();
            }

            switch (messageType)
            {
                case BridgeMessageType.InitialConfig:
                    Address = data.Split(":")[0];
                    Port = Convert.ToInt32(data.Split(":")[1]);
                    ServerManager.AddServer(this);
                    break;
                default:
                    Log.Print(LogType.Game, "Received unhandled message type");
                    break;
            }
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

        public void SendGameInfo()
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte) BridgeMessageType.SetLobbyGameInfo);
            string jsonData = JsonConvert.SerializeObject(GameInfo);
            stream.Write(GetBytesSpan(jsonData));
            Send(stream.ToArray());
            Log.Print(LogType.Game, "Setting Game Info");
        }

        public void SendTeamInfo()
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)BridgeMessageType.SetTeamInfo);
            string jsonData = JsonConvert.SerializeObject(TeamInfo);
            stream.Write(GetBytesSpan(jsonData));
            Send(stream.ToArray());
            Log.Print(LogType.Game, "Setting Team Info");
        }

        public void SendStartNotification()
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)BridgeMessageType.Start);
            Send(stream.ToArray());
            Log.Print(LogType.Game, "Starting Game Server");
        }

        public void SendPlayerLeavingNotification(long accountId, bool isPermanent, GameResult gameResult)
        {
            MemoryStream stream = new MemoryStream();
            stream.WriteByte((byte)BridgeMessageType.PlayerLeaving);
            string jsonData = JsonConvert.SerializeObject(new PlayerLeavingNotification()
            {
                AccountId = accountId,
                IsPermanent = isPermanent,
                GameResult = gameResult
            });
            stream.Write(GetBytesSpan(jsonData));
            Send(stream.ToArray());
            Log.Print(LogType.Game, $"Player {accountId} leaves game");
        }

        [Serializable]
        class PlayerLeavingNotification
        {
            public long AccountId;
            public bool IsPermanent;
            public GameResult GameResult;
        }

        class ServerGame {
            public LobbyGameInfo gameInfo;
            public LobbyTeamInfo teamInfo;
        }
    }
}
