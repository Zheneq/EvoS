using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CentralServer.LobbyServer.Matchmaking
{
    /// <summary>
    /// Manages the matchmaking process. it contains queues where all the player are assigned and when there are enough
    /// players launches a new game
    /// </summary>
    public static class MatchmakingManager
    {
        // List of matchmaking queues by game mode (practive, coop, pvp, ranked and custom)
        private static Dictionary<GameType, MatchmakingQueue> Queues = new Dictionary<GameType, MatchmakingQueue>()
        {
            { GameType.Practice, new MatchmakingQueue(GameType.Practice) },
            { GameType.Coop, new MatchmakingQueue(GameType.Coop) },
            { GameType.PvP, new MatchmakingQueue(GameType.PvP) },
            { GameType.Ranked, new MatchmakingQueue(GameType.Ranked) },
            { GameType.Custom, new MatchmakingQueue(GameType.Custom) }
        };

        /// <summary>
        /// Updates all the queues
        /// </summary>
        public static void Update()
        {
            foreach (var queue in Queues.Values)
            {
                queue.Update();
            }
        }

        /// <summary>
        /// Adds a player to a queue
        /// </summary>
        /// <param name="gameType">player's selected gamemode</param>
        /// <param name="client">client</param>
        public static void AddToQueue(GameType gameType, LobbyServerProtocolBase client)
        {
            // Get the queue
            MatchmakingQueue queue = Queues[gameType];

            // Add player to the queue
            LobbyMatchmakingQueueInfo info = queue.AddPlayer(client);

            // Send 'Assigned to queue notification' to the player
            client.Send(new MatchmakingQueueAssignmentNotification() { MatchmakingQueueInfo = info });

            // Update the queue
            queue.Update();
        }

        public static void StartPractice(LobbyServerProtocolBase client)
        {
            LobbyGameInfo practiceGameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = 1,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = 1,
                ActivePlayers = 1,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = GameType.PvP,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = Maps.Skyway_Deathmatch,
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[GameType.Practice].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = 1,
                    TeamBBots = 2,
                    TeamBPlayers = 0,
                }
            };

            LobbyTeamInfo teamInfo = new LobbyTeamInfo();
            teamInfo.TeamPlayerInfo = new List<LobbyPlayerInfo>
            {
                SessionManager.GetPlayerInfo(client.AccountId),
                CharacterManager.GetPunchingDummyPlayerInfo(),
                CharacterManager.GetPunchingDummyPlayerInfo()
            };
            teamInfo.TeamPlayerInfo[0].TeamId = Team.TeamA;
            teamInfo.TeamPlayerInfo[0].PlayerId = 1;
            teamInfo.TeamPlayerInfo[1].TeamId = Team.TeamB;
            teamInfo.TeamPlayerInfo[1].PlayerId = 2;
            teamInfo.TeamPlayerInfo[2].TeamId = Team.TeamB;
            teamInfo.TeamPlayerInfo[2].PlayerId = 3;

            BridgeServerProtocol server = ServerManager.GetServer(practiceGameInfo, teamInfo);
            if (server == null)
            {
                Log.Print(LogType.Error, "No available server for practice gamemode");
            }
            else
            {
                practiceGameInfo.GameServerAddress = server.URI;
                practiceGameInfo.GameServerProcessCode = server.ProcessCode;
                practiceGameInfo.GameStatus = GameStatus.Launching;

                GameAssignmentNotification notification1 = new GameAssignmentNotification
                {
                    GameInfo = practiceGameInfo,
                    GameResult = GameResult.NoResult,
                    Observer = false,
                    PlayerInfo = teamInfo.TeamPlayerInfo[0],
                    Reconnection = false,
                    GameplayOverrides = client.GetGameplayOverrides()
                };

                client.Send(notification1);

                practiceGameInfo.GameStatus = GameStatus.Launched;
                GameInfoNotification notification2 = new GameInfoNotification()
                {
                    TeamInfo = teamInfo,
                    GameInfo = practiceGameInfo,
                    PlayerInfo = teamInfo.TeamPlayerInfo[0]
                };

                client.Send(notification2);
            }
        }

        public static void StartGame(List<LobbyServerProtocolBase> clients, GameType gameType)
        {
            Log.Print(LogType.Lobby, $"Starting {gameType} game...");

            MatchmakingQueue lobbyQueue = Queues[gameType];
            GameSubType subType = lobbyQueue.MatchmakingQueueInfo.GameConfig.SubTypes[0];
            LobbyTeamInfo teamInfo = new LobbyTeamInfo { TeamPlayerInfo = new List<LobbyPlayerInfo>() };

            // Fill team A
            for (int i = 0; i < subType.TeamAPlayers; i++)
            {
                LobbyServerProtocolBase client = clients[i];
                LobbyPlayerInfo playerInfo = SessionManager.GetPlayerInfo(client.AccountId);
                playerInfo.TeamId = Team.TeamA;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count;
                Log.Print(LogType.Game, $"adding player {client.UserName}, {client.AccountId} to team A");
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            // Fill team B
            for (int i = 0; i < subType.TeamBPlayers; i++)
            {
                LobbyServerProtocolBase client = clients[subType.TeamAPlayers + i];
                LobbyPlayerInfo playerInfo = SessionManager.GetPlayerInfo(client.AccountId);
                playerInfo.TeamId = Team.TeamB;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count;
                Log.Print(LogType.Game, $"adding player {client.UserName}, {client.AccountId} to team B");
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = clients.Count,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = clients.Count,
                ActivePlayers = clients.Count,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = Maps.Skyway_Deathmatch,
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = teamInfo.TeamAPlayerInfo.Count(),
                    TeamBBots = 0,
                    TeamBPlayers = teamInfo.TeamBPlayerInfo.Count(),
                }
            };

            BridgeServerProtocol server = ServerManager.GetServer(gameInfo, teamInfo);
            if (server == null)
            {
                Log.Print(LogType.Error, $"No available server for {gameType} gamemode");
                return;
            }
            else
            {
                lobbyQueue.RemovePlayers(clients);
                server.clients = clients;
                gameInfo.GameServerAddress = server.URI;
                gameInfo.GameServerProcessCode = server.ProcessCode;
                gameInfo.GameStatus = GameStatus.Launching;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameAssignmentNotification notification = new GameAssignmentNotification
                    {
                        GameInfo = gameInfo,
                        GameResult = GameResult.NoResult,
                        Observer = false,
                        PlayerInfo = teamInfo.TeamPlayerInfo[i],
                        Reconnection = false,
                        GameplayOverrides = client.GetGameplayOverrides()
                    };

                    client.Send(notification);
                    client.CurrentServer = server;
                }

                gameInfo.GameStatus = GameStatus.Launched;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameInfoNotification notification = new GameInfoNotification()
                    {
                        TeamInfo = teamInfo,
                        GameInfo = gameInfo,
                        PlayerInfo = teamInfo.TeamPlayerInfo[i]
                    };

                    client.Send(notification);
                }

                Log.Print(LogType.Error, $"Game {gameType} started");
            }
        }

        public static void StartGameVBots(List<LobbyServerProtocolBase> clients, GameType gameType)
        {
            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = clients.Count,
                AcceptTimeout = new TimeSpan(0, 0, 0),
                ActiveHumanPlayers = clients.Count,
                ActivePlayers = clients.Count,
                CreateTimestamp = DateTime.Now.Ticks,
                GameConfig = new LobbyGameConfig
                {
                    GameOptionFlags = GameOptionFlag.NoInputIdleDisconnect & GameOptionFlag.NoInputIdleDisconnect,
                    GameServerShutdownTime = -1,
                    GameType = gameType,
                    InstanceSubTypeBit = 1,
                    IsActive = true,
                    Map = Maps.Skyway_Deathmatch,
                    ResolveTimeoutLimit = 1600, // TODO ?
                    RoomName = "",
                    Spectators = 0,
                    SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes,
                    TeamABots = 0,
                    TeamAPlayers = clients.Count,
                    TeamBBots = 2,
                    TeamBPlayers = 0,
                }
            };

            LobbyTeamInfo teamInfo = new LobbyTeamInfo();
            teamInfo.TeamPlayerInfo = new List<LobbyPlayerInfo>();

            for (int i = 0; i < clients.Count; i++)
            {
                LobbyPlayerInfo playerInfo = SessionManager.GetPlayerInfo(clients[i].AccountId);
                playerInfo.TeamId = Team.TeamA;
                playerInfo.PlayerId = i;
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            for (int i = 0; i < 2; i++)
            {
                LobbyPlayerInfo playerInfo = CharacterManager.GetPunchingDummyPlayerInfo();
                playerInfo.TeamId = Team.TeamB;
                playerInfo.PlayerId = teamInfo.TeamPlayerInfo.Count;
                teamInfo.TeamPlayerInfo.Add(playerInfo);
            }

            BridgeServerProtocol server = ServerManager.GetServer(gameInfo, teamInfo);
            if (server == null)
            {
                Log.Print(LogType.Error, "No available server for practice gamemode");
            }
            else
            {
                gameInfo.GameServerAddress = server.URI;
                gameInfo.GameServerProcessCode = server.ProcessCode;
                gameInfo.GameStatus = GameStatus.Launching;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameAssignmentNotification notification = new GameAssignmentNotification
                    {
                        GameInfo = gameInfo,
                        GameResult = GameResult.NoResult,
                        Observer = false,
                        PlayerInfo = teamInfo.TeamPlayerInfo[i],
                        Reconnection = false,
                        GameplayOverrides = client.GetGameplayOverrides()
                    };

                    client.Send(notification);
                }

                gameInfo.GameStatus = GameStatus.Launched;

                for (int i = 0; i < clients.Count; i++)
                {
                    LobbyServerProtocolBase client = clients[i];
                    GameInfoNotification notification = new GameInfoNotification()
                    {
                        TeamInfo = teamInfo,
                        GameInfo = gameInfo,
                        PlayerInfo = teamInfo.TeamPlayerInfo[i]
                    };

                    client.Send(notification);
                }
            }
        }
    }
}