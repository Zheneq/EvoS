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
using System.Text;

namespace CentralServer.LobbyServer.Matchmaking
{
    public static class MatchmakingManager
    {
        private static Dictionary<GameType, MatchmakingQueue> Queues = new Dictionary<GameType, MatchmakingQueue>()
        {
            {GameType.Practice, new MatchmakingQueue(GameType.Practice)},
            {GameType.Coop, new MatchmakingQueue(GameType.Coop)},
            {GameType.PvP, new MatchmakingQueue(GameType.PvP)},
            {GameType.Ranked, new MatchmakingQueue(GameType.Ranked)},
            {GameType.Custom, new MatchmakingQueue(GameType.Custom)}
        };

        public static void AddToQueue(GameType gameType, LobbyPlayerInfo playerInfo)
        {
            // TODO
        }
        public static void StartPractice(LobbyServerProtocolBase client)
        {
            LobbyGameInfo practiceGameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = 1,
                AcceptTimeout = new TimeSpan(0,0,0),
                ActiveHumanPlayers = 1,
                ActivePlayers = 1,
                CreateTimestamp = DateTime.Now.Ticks,
                GameServerProcessCode = "Artemis" + DateTime.Now.Ticks,
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
                    TeamBBots = 0,
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

			string serverAddress = ServerManager.GetServer(practiceGameInfo, teamInfo);
            if (serverAddress == null)
            {
                Log.Print(LogType.Error, "No available server for practice gamemode");
            }
            else
            {
                practiceGameInfo.GameServerAddress = "ws://" + serverAddress;
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
    }
}
