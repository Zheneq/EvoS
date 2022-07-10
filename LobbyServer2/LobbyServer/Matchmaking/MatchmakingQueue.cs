using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using System;
using System.Collections.Generic;
using System.Text;
using EvoS.Framework.Logging;

namespace CentralServer.LobbyServer.Matchmaking
{
    class MatchmakingQueue
    {
        Dictionary<string, LobbyGameInfo> Games = new Dictionary<string, LobbyGameInfo>();
        SynchronizedCollection<LobbyServerProtocolBase> QueuedPlayers = new SynchronizedCollection<LobbyServerProtocolBase>();
        GameType GameType;
        
        private static int GameID = 0;

        public MatchmakingQueue(GameType gameType)
        {
            GameType = gameType;
        }

        public void Update()
        {
			for (int i = 0; i < QueuedPlayers.Count; i++)
            {
				LobbyServerProtocolBase connection = QueuedPlayers[i];
				if (connection.State != WebSocketSharp.WebSocketState.Open)
                {
                    Log.Print(LogType.Game, $"Removing disconnected player {connection.AccountId} from {GameType} queue");
                    QueuedPlayers.RemoveAt(i--);
				}
			}

            Log.Print(LogType.Game, $"{QueuedPlayers.Count} players in {GameType} queue");
            if (QueuedPlayers.Count >= 2)
			{
                List<LobbyServerProtocolBase> clients = new List<LobbyServerProtocolBase>();
                for (int i = 0; i < 2; i++)
				{
                    LobbyServerProtocolBase client = QueuedPlayers[0];
                    QueuedPlayers.RemoveAt(0);
                    clients.Add(client);
				}
                MatchmakingManager.StartGame(clients, GameType);
			}
        }

        public LobbyMatchmakingQueueInfo AddPlayer(LobbyServerProtocolBase connection)
		{
            QueuedPlayers.Add(connection);

            return new LobbyMatchmakingQueueInfo()
            {
                ShowQueueSize = true,
                QueuedPlayers = QueuedPlayers.Count,
                PlayersPerMinute = 1,
                GameConfig = new LobbyGameConfig()
                {
                    GameType = GameType
                },
                QueueStatus = QueueStatus.Success,
                AverageWaitTime = TimeSpan.FromSeconds(0)
            };
        }

        public LobbyGameInfo CreateGameInfo()
        {
            GameTypeAvailability gameAvailability = Gamemode.GameModeManager.GetGameTypeAvailabilities()[GameType];

            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = gameAvailability.TeamAPlayers + gameAvailability.TeamBPlayers,
                AcceptTimeout = GameType == GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                ActiveHumanPlayers = 0,
                ActivePlayers = 0,
                ActiveSpectators = 0,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.AllowDuplicateCharacters & GameOptionFlag.EnableTeamAIOutput & GameOptionFlag.NoInputIdleDisconnect,
                    GameType = GameType,
                    IsActive = true,
                    RoomName = $"Evos-{GameType.ToString()}-{GameID++}",
                    SubTypes = gameAvailability.SubTypes,
                    TeamAPlayers = gameAvailability.TeamAPlayers,
                    TeamABots = gameAvailability.TeamABots,
                    TeamBPlayers = gameAvailability.TeamBPlayers,
                    TeamBBots = gameAvailability.TeamBBots,
                    Map = SelectMap(gameAvailability),
                    ResolveTimeoutLimit = 1600,
                    Spectators = 0
                },
                GameResult = GameResult.NoResult,
                GameServerAddress = "",
                GameStatus = GameStatus.Assembling,
                GameServerHost = "",
                IsActive = true,
                LoadoutSelectTimeout = GameType==GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                SelectTimeout = GameType==GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                // TODO: there are more options that may be usefull
            };

            return gameInfo;
        }

        public string SelectMap(GameTypeAvailability gameTypeAvailability)
        {
            List<GameMapConfig> maps = gameTypeAvailability.SubTypes[0].GameMapConfigs;
            Random rand = new Random();
            int index = rand.Next(0, maps.Count);
            return maps[index].Map;
        }

        public void SetGameStatus(string roomName, GameStatus gameStatus)
        {
            Games[roomName].GameStatus = gameStatus;
        }

    }
}
