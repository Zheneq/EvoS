using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Http;

namespace CentralServer.ApiServer
{

    public static class StatusController
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusController));
        
        private static readonly string MAP_UNKNOWN = "UNKNOWN";
        
        public static IResult GetStatus()
        {
            List<BridgeServerProtocol> servers = ServerManager.GetServers();
            Status status = new Status
            {
                players = SessionManager.GetOnlinePlayers()
                    .Select(id => DB.Get().AccountDao.GetAccount(id))
                    .Select(Player.Of)
                    .ToList(),
                groups = GroupManager.GetGroups()
                    .Select(Group.Of)
                    .ToList(),
                queues = MatchmakingManager.GetQueues()
                    .Select(Queue.Of)
                    .ToList(),
                servers = servers
                    .Select(Server.Of)
                    .ToList(),
                games = servers
                    .Where(s => !s.IsAvailable())
                    .Select(Game.Of)
                    .ToList()
            };
            return Results.Json(status);
        }
        
        public static IResult GetSimpleStatus()
        {
            List<BridgeServerProtocol> servers = ServerManager.GetServers();
            HashSet<long> players = SessionManager.GetOnlinePlayers();
            Status status = new Status
            {
                players = players
                    .Select(id => DB.Get().AccountDao.GetAccount(id))
                    .Select(Player.Of)
                    .ToList(),
                groups = players
                    .Select(Group.OfPlayer)
                    .ToList(),
                queues = MatchmakingManager.GetQueues()
                    .Select(Queue.OfPlayers)
                    .ToList(),
                servers = servers
                    .Where(s => !s.IsAvailable())
                    .Select(Server.Of)
                    .ToList(),
                games = servers
                    .Where(s => !s.IsAvailable())
                    .Select(g => Game.Of(g, true))
                    .ToList()
            };
            return Results.Json(status);
        }

        public struct Status
        {
            public List<Player> players { get; set; }
            public List<Group> groups { get; set; }
            public List<Queue> queues { get; set; }
            public List<Server> servers { get; set; }
            public List<Game> games { get; set; }
        }

        public struct Player
        {
            public long accountId { get; set; }
            public string handle { get; set; }
            public int bannerBg { get; set; }
            public int bannerFg { get; set; }
            public string status { get; set; }

            public static Player Of(PersistedAccountData acc)
            {
                return new Player
                {
                    accountId = acc.AccountId,
                    handle = acc.Handle,
                    bannerBg = acc.AccountComponent.SelectedBackgroundBannerID == -1  ? 95 : acc.AccountComponent.SelectedBackgroundBannerID, // if no Banner is set default to 95
                    bannerFg = acc.AccountComponent.SelectedForegroundBannerID == -1  ? 65 : acc.AccountComponent.SelectedForegroundBannerID, // if no Foreground Banner is set default to 65
                    status = FriendManager.GetStatusString(SessionManager.GetClientConnection(acc.AccountId)),
                };
            }
        }

        public struct Group
        {
            public long groupId { get; set; }
            public List<long> accountIds { get; set; }

            public static Group Of(GroupInfo g)
            {
                return new Group
                {
                    accountIds = g.Members,
                    groupId = g.GroupId
                };
            }

            public static Group OfPlayer(long accountId)
            {
                return new Group
                {
                    accountIds = new List<long>{ accountId },
                    groupId = accountId
                };
            }
        }

        public struct Queue
        {
            public string type { get; set; }
            public List<long> groupIds { get; set; }

            public static Queue Of(MatchmakingQueue q)
            {
                return new Queue
                {
                    type = q.GameType.ToString(),
                    groupIds = q.GetQueuedGroups()
                };
            }

            public static Queue OfPlayers(MatchmakingQueue q)
            {
                return new Queue
                {
                    type = q.GameType.ToString(),
                    groupIds = q.GetQueuedPlayers()
                };
            }
        }

        public struct Server
        {
            public string id { get; set; }
            public string name { get; set; }

            public static Server Of(BridgeServerProtocol s)
            {
                return new Server
                {
                    id = s.ProcessCode,
                    name = s.Name
                };
            }
        }
        
        public struct Game
        {
            public string id { get; set; }
            public string ts { get; set; }
            public string server { get; set; }
            public List<GamePlayer> teamA { get; set; }
            public List<GamePlayer> teamB { get; set; }
            public string map { get; set; }
            public string status { get; set; }
            public int turn { get; set; }
            public int teamAScore { get; set; }
            public int teamBScore { get; set; }

            public static Game Of(BridgeServerProtocol s)
            {
                return Of(s, false);
            }

            public static Game Of(BridgeServerProtocol s, bool hideTeamSensitiveData)
            {
                bool hide = hideTeamSensitiveData && s.ServerGameStatus < GameStatus.LoadoutSelecting;
                return new Game
                {
                    id = s.GetGameInfo?.GameServerProcessCode,
                    ts = s.GameInfo != null ? $"{new DateTime(s.GameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}" : null,
                    map = hide ? MAP_UNKNOWN : s.GetGameInfo?.GameConfig.Map ?? MAP_UNKNOWN,
                    server = s.ProcessCode,
                    teamA = s.GetTeamInfo.TeamAPlayerInfo.Select(p => GamePlayer.Of(p, hide)).ToList(),
                    teamB = s.GetTeamInfo.TeamBPlayerInfo.Select(p => GamePlayer.Of(p, hide)).ToList(),
                    status = s.ServerGameStatus.ToString(),
                    turn = s.GameSummary?.NumOfTurns ?? s.GameMetrics.CurrentTurn,
                    teamAScore = s.GameSummary?.TeamAPoints ?? s.GameMetrics.TeamAPoints,
                    teamBScore = s.GameSummary?.TeamBPoints ?? s.GameMetrics.TeamBPoints,
                };
            }
        }
        
        public struct GamePlayer
        {
            public long accountId { get; set; }
            public string characterType { get; set; }

            public static GamePlayer Of(LobbyServerPlayerInfo playerInfo, bool hideTeamSensitiveData)
            {
                return new GamePlayer
                {
                    accountId = playerInfo.AccountId,
                    characterType = (hideTeamSensitiveData ? CharacterType.None : playerInfo.CharacterType).ToString()
                };
            }
        }
    }
}