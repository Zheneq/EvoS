using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;
using WebSocketSharp;

namespace CentralServer.LobbyServer.Matchmaking
{
    public class MatchmakingQueue
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingQueue));
        private const string ConfigPath = @"Config/Matchmaking/";

        private readonly string EloKey;
        private MatchmakingConfiguration Conf = new MatchmakingConfiguration();
        private readonly Dictionary<string, Matchmaker> Matchmakers;
        
        Dictionary<string, LobbyGameInfo> Games = new Dictionary<string, LobbyGameInfo>();
        private readonly ConcurrentDictionary<long, DateTime> QueuedGroups = new ConcurrentDictionary<long, DateTime>();
        public LobbyMatchmakingQueueInfo MatchmakingQueueInfo;
        public GameType GameType => MatchmakingQueueInfo.GameType;

        public List<long> GetQueuedGroups()
        {
            return QueuedGroups.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }
        public List<long> GetQueuedPlayers()
        {
            return QueuedGroups.Keys.SelectMany(g => GroupManager.GetGroup(g).Members).ToList();
        }
        public bool GetQueueTime(long groupId, out DateTime time)
        {
            return QueuedGroups.TryGetValue(groupId, out time);
        }
        
        private static int GameID = 0;

        public MatchmakingQueue(GameType gameType)
        {
            EloKey = gameType.ToString();
            MatchmakingQueueInfo = new LobbyMatchmakingQueueInfo()
            {
                QueueStatus = QueueStatus.Idle,
                QueuedPlayers = 0,
                ShowQueueSize = true,
                AverageWaitTime = TimeSpan.FromSeconds(60),
                GameConfig = new LobbyGameConfig()
                {
                    GameType = gameType,
                }
            };
            ReloadConfig();

            // TODO handle matchmakers more carefully
            Matchmakers = MatchmakingQueueInfo.GameConfig.SubTypes
                .ToDictionary(
                    st => st.LocalizedName,
                    st => new Matchmaker(GameType, st, EloKey, Conf));
        }

        private void ReloadConfig()
        {
            GameType gameType = MatchmakingQueueInfo.GameConfig.GameType;
            MatchmakingQueueInfo.GameConfig.SubTypes = GameModeManager.GetGameTypeAvailabilities()[gameType].SubTypes;

            ReloadMatchmakingConfig(gameType);
        }

        private void ReloadMatchmakingConfig(GameType gameType)
        {
            JsonReader reader = null;
            try
            {
                reader = new JsonTextReader(new System.IO.StreamReader(ConfigPath + gameType + ".json"));
                Conf = new JsonSerializer().Deserialize<MatchmakingConfiguration>(reader);
            }
            catch (Exception e)
            {
                log.Error($"Failed to reload matchmaking config", e);
            }
            finally
            {
                reader?.Close();
            }
        }

        public LobbyMatchmakingQueueInfo AddGroup(long groupId, out bool added)
        {
            added = QueuedGroups.TryAdd(groupId, DateTime.UtcNow);
            MatchmakingQueueInfo.QueuedPlayers = GetPlayerCount();
            MatchmakingQueueInfo.AverageWaitTime = TimeSpan.FromSeconds(0);
            MatchmakingQueueInfo.QueueStatus = ServerManager.IsAnyServerAvailable() ? QueueStatus.WaitingForHumans : QueueStatus.AllServersBusy;
            log.Info($"Added group {groupId} to {GameType} queue");
            log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");

            return MatchmakingQueueInfo;
        }

        public bool RemoveGroup(long groupId)
        {
            bool removed = QueuedGroups.TryRemove(groupId, out _);
            if (removed)
            {
                log.Info($"Removed group {groupId} from {GameType} queue");
                log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");
            }
            return removed;
        }

        public bool IsQueued(long groupId)
        {
            return QueuedGroups.ContainsKey(groupId);
        }

        public int GetPlayerCount()
        {
            return QueuedGroups.Keys
                .Select(GroupManager.GetGroup)
                .Sum(group => group?.Members.Count ?? 0);
        }

        public void Update()
        {
            log.Debug($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");

            // TODO UpdateSettings when file changes (and only then)
            ReloadConfig();

            if (MatchmakingManager.Enabled)
            {
                TryMatch();
            }
        }

        private void TryMatch()
        {
            foreach (GameSubType subType in MatchmakingQueueInfo.GameConfig.SubTypes)
            {
                lock (GroupManager.Lock)
                {
                    List<Matchmaker.MatchmakingGroup> queuedGroups = GetQueuedGroups()
                        .Select(groupId =>
                        {
                            if (!GetQueueTime(groupId, out DateTime queueTime))
                            {
                                log.Error($"Cannon fetch queue time for group {groupId}");
                            }
                            return new Matchmaker.MatchmakingGroup(groupId, queueTime);
                        })
                        .ToList();

                    List<Matchmaker.Match> matches = Matchmakers[subType.LocalizedName].GetMatchesRanked(queuedGroups, DateTime.UtcNow);
                    if (matches.Count > 0)
                    {
                        StartMatch(matches[0]);
                    }
                }
            }
        }

        private void StartMatch(Matchmaker.Match match)
        {
            if (!CheckGameServerAvailable())
            {
                log.Warn("No available game server to start a match");
                return;
            }
            foreach (Matchmaker.MatchmakingGroup groupInfo in match.Groups)
            {
                RemoveGroup(groupInfo.GroupID);
            }
            _ = MatchmakingManager.StartGameAsync(
                match.TeamA.AccountIds, 
                match.TeamB.AccountIds, 
                GameType,
                MatchmakingQueueInfo.GameConfig.SubTypes[0]);
        }
        
        public bool CheckGameServerAvailable()
        {
            if (ServerManager.IsAnyServerAvailable()) return true;

            // If there is no game server already connected, we check if we can launch one
            string gameServer = EvosConfiguration.GetGameServerExecutable();
            if (gameServer.IsNullOrEmpty()) return false;
            
            // TODO: this will start a new game server every time the queue is run, which can cause multiple server that aren't needed to start
            using (var process = new Process())
            {
                try
                {
                    process.StartInfo = new ProcessStartInfo(gameServer, EvosConfiguration.GetGameServerExecutableArgs());
                    process.Start();
                }
                catch(Exception e)
                {
                    log.Error("Failed to start a game server", e);
                }   
            }
            return false;
        }

        public LobbyGameInfo CreateGameInfo()
        {
            GameTypeAvailability gameAvailability = GameModeManager.GetGameTypeAvailabilities()[GameType];

            LobbyGameInfo gameInfo = new LobbyGameInfo
            {
                AcceptedPlayers = gameAvailability.TeamAPlayers + gameAvailability.TeamBPlayers,
                AcceptTimeout = GameType == GameType.Practice ? TimeSpan.FromSeconds(0) : TimeSpan.FromSeconds(5),
                ActiveHumanPlayers = 0,
                ActivePlayers = 0,
                ActiveSpectators = 0,
                CreateTimestamp = DateTime.UtcNow.Ticks,
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
                    Map = SelectMap(gameAvailability.SubTypes[0]),
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

        public static string SelectMap(GameSubType gameSubType)
        {
            List<GameMapConfig> maps = gameSubType.GameMapConfigs.Where(x => x.IsActive).ToList();
            Random rand = new Random();
            int index = rand.Next(0, maps.Count);
            string selected = maps[index].Map;
            log.Info($"Selected {selected} out of {maps.Count} maps");
            return selected;
        }

        public void SetGameStatus(string roomName, GameStatus gameStatus)
        {
            Games[roomName].GameStatus = gameStatus;
        }

        public void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary)
        {
            Elo.OnGameEnded(
                gameInfo,
                gameSummary,
                EloKey,
                Conf,
                DateTime.UtcNow,
                DB.Get().AccountDao.GetAccount,
                DB.Get().MatchHistoryDao.Find);
        }
    }
}
