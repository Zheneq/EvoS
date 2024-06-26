using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.TrustWar;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Http;
using static EvoS.Framework.DataAccess.Daos.AdminMessageDao;

namespace CentralServer.ApiServer
{

    public static class StatusController
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusController));

        private static readonly string MAP_UNKNOWN = "UNKNOWN";

        public static IResult GetStatus()
        {
            List<BridgeServerProtocol> servers = ServerManager.GetServers();
            List<BridgeServer.Game> games = GameManager.GetGames();
            Status status = new Status
            {
                players = SessionManager.GetOnlinePlayers()
                    .Concat(games.SelectMany(g => g.GetPlayers()))
                    .Distinct()
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
                    .Concat(games
                        .Where(g => g.GameInfo?.GameConfig is { GameType: GameType.Custom } && g.Server is null)
                        .Select(Server.OfCustomGame))
                    .ToList(),
                games = games
                    .Select(Game.Of)
                    .ToList()
            };
            return Results.Json(status);
        }

        public static IResult GetSimpleStatus()
        {
            List<BridgeServerProtocol> servers = ServerManager.GetServers();
            List<BridgeServer.Game> games = GameManager.GetGames();
            HashSet<long> players = SessionManager.GetOnlinePlayers();
            long[] factionsData = Array.Empty<long>();
            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                factionsData = TrustWarManager.getTrustWarEntry().Points;
            }

            Status status = new Status
            {
                players = players
                    .Concat(games.SelectMany(g => g.GetPlayers()))
                    .Distinct()
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
                    .Concat(games
                        .Where(g => g.GameInfo?.GameConfig is { GameType: GameType.Custom } && g.Server is null)
                        .Select(Server.OfCustomGame))
                    .ToList(),
                games = games
                    .Select(g => Game.Of(g, true))
                    .ToList(),
                factionsEnabled = LobbyConfiguration.IsTrustWarEnabled(),
                factionsData = factionsData
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
            public bool factionsEnabled { get; set; }
            public long[] factionsData { get; set; }
        }

        public struct Player
        {
            public long accountId { get; set; }
            public string handle { get; set; }
            public int bannerBg { get; set; }
            public int bannerFg { get; set; }
            public string status { get; set; }
            public int titleId { get; set; }
            public TrustWarManager.PlayerTrustWarDetails factionData { get; set; }

            private static readonly Player BotPlayer = new Player
            {
                handle = "Bot",
                bannerBg = 95,
                bannerFg = 65,
                titleId = -1,
                status = FriendManager.Status_InGame,
            };

            public static Player Of(PersistedAccountData acc)
            {
                if (acc is null)
                {
                    return BotPlayer;
                }
                return new Player
                {
                    accountId = acc.AccountId,
                    handle = acc.Handle,
                    bannerBg = acc.AccountComponent.SelectedBackgroundBannerID == -1 ? 95 : acc.AccountComponent.SelectedBackgroundBannerID, // if no Banner is set default to 95
                    bannerFg = acc.AccountComponent.SelectedForegroundBannerID == -1 ? 65 : acc.AccountComponent.SelectedForegroundBannerID, // if no Foreground Banner is set default to 65
                    titleId = acc.AccountComponent.SelectedTitleID,
                    status = FriendManager.GetStatusString(SessionManager.GetClientConnection(acc.AccountId)),
                    factionData = TrustWarManager.PlayerTrustWarDetails.Of(acc),
                };
            }
        }

        public struct ActivePlayer
        {
            public long accountId { get; set; }
            public string handle { get; set; }
            public int bannerBg { get; set; }
            public int bannerFg { get; set; }
            public string status { get; set; }
            public int titleId { get; set; }
            public TrustWarManager.PlayerTrustWarDetails factionData { get; set; }
            public bool locked { get; set; }
            public DateTime lockedUntil { get; set; }
            public string lockedReason { get; set; }
            public string adminMessage { get; set; }

            public static ActivePlayer Of(PersistedAccountData acc)
            {
                AdminMessage adminMessage = DB.Get().AdminMessageDao.FindPending(acc.AccountId);

                return new ActivePlayer
                {
                    accountId = acc.AccountId,
                    handle = acc.Handle,
                    bannerBg = acc.AccountComponent.SelectedBackgroundBannerID == -1 ? 95 : acc.AccountComponent.SelectedBackgroundBannerID, // if no Banner is set default to 95
                    bannerFg = acc.AccountComponent.SelectedForegroundBannerID == -1 ? 65 : acc.AccountComponent.SelectedForegroundBannerID, // if no Foreground Banner is set default to 65
                    titleId = acc.AccountComponent.SelectedTitleID,
                    status = FriendManager.GetStatusString(SessionManager.GetClientConnection(acc.AccountId)),
                    factionData = TrustWarManager.PlayerTrustWarDetails.Of(acc),
                    locked = acc.AdminComponent.Locked,
                    lockedUntil = acc.AdminComponent.LockedUntil.ToUniversalTime(),
                    lockedReason = acc.AdminComponent.AdminActions?.FindLast(x => x.ActionType == AdminComponent.AdminActionType.Lock)?.Description ?? "",
                    adminMessage = adminMessage?.message
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
                    accountIds = new List<long> { accountId },
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
                    groupIds = q.GetQueuedGroupsAsList()
                };
            }

            public static Queue OfPlayers(MatchmakingQueue q)
            {
                return new Queue
                {
                    type = q.GameType.ToString(),
                    groupIds = q.GetQueuedPlayersAsList()
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

            public static Server OfCustomGame(BridgeServer.Game g)
            {
                return new Server
                {
                    id = g.ProcessCode,
                    name = "Custom game"
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
            public string gameType { get; set; }
            public string gameSubType { get; set; }

            public static Game Of(BridgeServer.Game s)
            {
                return Of(s, false);
            }

            public static Game Of(BridgeServer.Game s, bool hideTeamSensitiveData)
            {
                bool hide = hideTeamSensitiveData && s.GameStatus < GameStatus.LoadoutSelecting;
                return new Game
                {
                    id = s.GameInfo?.GameServerProcessCode,
                    ts = s.GameInfo != null ? $"{new DateTime(s.GameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}" : null,
                    map = hide ? MAP_UNKNOWN : s.GameInfo?.GameConfig.Map ?? MAP_UNKNOWN,
                    server = s.ProcessCode,
                    teamA = s.TeamInfo.TeamAPlayerInfo.Select(p => GamePlayer.Of(p, hide)).ToList(),
                    teamB = s.TeamInfo.TeamBPlayerInfo.Select(p => GamePlayer.Of(p, hide)).ToList(),
                    status = s.GameStatus.ToString(),
                    turn = s.GameSummary?.NumOfTurns ?? s.GameMetrics.CurrentTurn,
                    teamAScore = s.GameSummary?.TeamAPoints ?? s.GameMetrics.TeamAPoints,
                    teamBScore = s.GameSummary?.TeamBPoints ?? s.GameMetrics.TeamBPoints,
                    gameType = s.GameInfo?.GameConfig.GameType.ToString(),
                    gameSubType = s.GameSubType?.LocalizedName
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