using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;
using Prometheus;
using WebSocketSharp;
using StreamReader = System.IO.StreamReader;

namespace CentralServer.LobbyServer.Matchmaking
{
    public class MatchmakingQueue
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakingQueue));
        private const string ConfigPath = @"Config/Matchmaking/";

        private readonly string EloKey;
        private readonly bool RankedMatchmaking;
        private MatchmakingConfigBundle Conf = new(); // TODO move to MatchmakerRanked
        private readonly Dictionary<string, Matchmaker> Matchmakers;
        
        private readonly ConcurrentDictionary<long, DateTime> QueuedGroups = new();
        public readonly LobbyMatchmakingQueueInfo MatchmakingQueueInfo;
        public GameType GameType => MatchmakingQueueInfo.GameType;
        private readonly string GameTypeString;

        private static readonly string[] LabelNames = { "queue","subType" };
        private static readonly Gauge MatchmakingTime = Metrics
            .CreateGauge(
                "evos_matchmaking_time_seconds",
                "Time spent in matchmaking routine last time.",
                LabelNames);
        private static readonly Gauge QueueSize = Metrics
            .CreateGauge(
                "evos_matchmaking_queue_size",
                "Number of people in the queue.",
                LabelNames);
        private static readonly Summary TimeInQueue = Metrics
            .CreateSummary(
                "evos_matchmaking_time_in_queue_seconds",
                "How long does it take to get a game.",
                LabelNames,
                new SummaryConfiguration
                {
                    Objectives = new List<QuantileEpsilonPair>
                    {
                        new(0.5, 0.01),
                        new(0.75, 0.01),
                        new(0.9, 0.01),
                        new(0.95, 0.01),
                        new(0.98, 0.01),
                        new(0.99, 0.01),
                    },
                    MaxAge = TimeSpan.FromHours(1)
                });
        private static readonly Histogram PredictedChances = Metrics
            .CreateHistogram(
                "evos_matchmaking_predicted_chances_percent",
                "How likely is one of the teams to win",
                LabelNames,
                new HistogramConfiguration
                {
                    Buckets = new[] { .51, .52, .53, .54, .55, .56, .57, .58, .59, .60, .65, .70, .75, .90 }
                });

        public IEnumerable<long> GetQueuedGroups()
        {
            return QueuedGroups.OrderBy(kv => kv.Value).Select(kv => kv.Key);
        }
        
        public IEnumerable<long> GetQueuedGroups(int subTypeIndex)
        {
            uint subTypeFlag = 1U << subTypeIndex;
            return GetQueuedGroups()
                .Where(groupId => (GroupManager.GetGroupSubTypeMask(groupId) & subTypeFlag) != 0);
        }
        
        public List<List<long>> GetQueuedGroupsBySubType()
        {
            var res = new List<List<long>>();
            for (int i = 0; i < MatchmakingQueueInfo.GameConfig.SubTypes.Count; i++)
            {
                res.Add(GetQueuedGroups(i).ToList());
            }

            return res;
        }
        
        public List<List<long>> GetQueuedPlayersBySubType()
        {
            var res = new List<List<long>>();
            for (int i = 0; i < MatchmakingQueueInfo.GameConfig.SubTypes.Count; i++)
            {
                res.Add(GetQueuedGroups(i)
                    .SelectMany(GroupManager.GetGroupMembers)
                    .ToList());
            }

            return res;
        }
        
        public bool GetQueueTime(long groupId, out DateTime time)
        {
            return QueuedGroups.TryGetValue(groupId, out time);
        }

        public MatchmakingConfiguration GetConf(string subType)
        {
            if (Conf.subTypes.TryGetValue(subType, out var config))
            {
                return config;
            }
            return Conf.Default;
        }

        public MatchmakingQueue(GameType gameType, bool isRanked)
        {
            GameTypeString = gameType.ToString();
            EloKey = GameTypeString;
            RankedMatchmaking = isRanked;
            MatchmakingQueueInfo = new LobbyMatchmakingQueueInfo()
            {
                QueueStatus = QueueStatus.Idle,
                QueuedPlayers = 0,
                ShowQueueSize = true,
                AverageWaitTime = TimeSpan.FromSeconds(0),
                GameConfig = new LobbyGameConfig()
                {
                    GameType = gameType,
                }
            };

            ReloadConfig();

            // TODO handle matchmakers more carefully
            Matchmakers = MatchmakingQueueInfo.GameConfig.SubTypes
                .ToDictionary(st => st.LocalizedName, MatchmakerFactory);
            
            Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
            {
                for (int i = 0; i < MatchmakingQueueInfo.GameConfig.SubTypes.Count; i++)
                {
                    GameSubType subType = MatchmakingQueueInfo.GameConfig.SubTypes[i];
                    M(QueueSize, subType).Set(GetPlayerCount(i));
                }

                M(QueueSize).Set(GetPlayerCount());
            });
            
            foreach (GameSubType subType in MatchmakingQueueInfo.GameConfig.SubTypes)
            {
                M(PredictedChances, subType).Publish();
            }
            M(PredictedChances).Publish();
        }

        private Matchmaker MatchmakerFactory(GameSubType st)
        {
            return RankedMatchmaking
                ? new MatchmakerRanked(GameType, st, EloKey, () => GetConf(st.LocalizedName))
                : st.Mods is not null && st.Mods.Contains(GameSubType.SubTypeMods.AntiSocial)
                    ? new MatchmakerSingleGroup(GameType, st)
                    : new MatchmakerFifo(GameType, st);
        }

        private void ReloadConfig()
        {
            MatchmakingQueueInfo.GameConfig.SubTypes = GameModeManager.GetGameTypeAvailabilities()[GameType].SubTypes;
            ReloadMatchmakingConfig(GameType);
        }

        private void ReloadMatchmakingConfig(GameType gameType)
        {
            if (!RankedMatchmaking)
            {
                return;
            }
            
            JsonReader reader = null;
            try
            {
                reader = new JsonTextReader(new StreamReader(ConfigPath + gameType + ".json"));
                Conf = new JsonSerializer().Deserialize<MatchmakingConfigBundle>(reader);
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
            UpdateQueueInfo();
            log.Info($"Added group {groupId} to {GameType} queue");
            log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");

            return MatchmakingQueueInfo;
        }

        public bool RemoveGroup(long groupId)
        {
            bool removed = QueuedGroups.TryRemove(groupId, out _);
            if (removed)
            {
                UpdateQueueInfo();
                GroupManager.OnLeaveQueue(groupId);
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

        public int GetPlayerCount(int subTypeIndex)
        {
            return GetQueuedGroups(subTypeIndex)
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
                UpdateQueueInfo();
                TryMatch();
            }

            SendQueueStatusNotifications();
        }
        
        public class ScoredMatchWithSubType : IComparable<ScoredMatchWithSubType>
        {
            public ScoredMatchWithSubType(Matchmaker.ScoredMatch scoredMatch, int subTypeIndex)
            {
                ScoredMatch = scoredMatch;
                SubTypeIndex = subTypeIndex;
            }

            public Matchmaker.ScoredMatch ScoredMatch { get; }
            public int SubTypeIndex { get; }
            public Matchmaker.Match Match => ScoredMatch.Match;
            public float Score => ScoredMatch.Score;
        
            public int CompareTo(ScoredMatchWithSubType other)
            {
                return Score.CompareTo(other.Score);
            }
        }

        private Dictionary<int, List<Matchmaker.MatchmakingGroup>> GetQueuedGroupsBySubtype()
        {
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype =
                new Dictionary<int, List<Matchmaker.MatchmakingGroup>>();

            for (int i = 0; i < MatchmakingQueueInfo.GameConfig.SubTypes.Count; i++)
            {
                List<Matchmaker.MatchmakingGroup> queuedGroups;
                lock (GroupManager.Lock)
                {
                    queuedGroups = GetQueuedGroups(i)
                        .Select(groupId =>
                        {
                            if (!GetQueueTime(groupId, out DateTime queueTime))
                            {
                                log.Error($"Cannon fetch queue time for group {groupId}");
                                queueTime = DateTime.UtcNow;
                            }

                            GroupInfo group = GroupManager.GetGroup(groupId);
                            if (group is null)
                            {
                                log.Error($"Group {groupId} is both queued and disbanded");
                                return null;
                            }

                            return new Matchmaker.MatchmakingGroup(group, queueTime);
                        })
                        .Where(g => g is not null)
                        .ToList();
                }
                queuedGroupsBySubtype[i] = queuedGroups;
            }

            return queuedGroupsBySubtype;
        }

        private List<ScoredMatchWithSubType> FindMatches(
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype)
        {
            DateTime matchmakingIterationStartTime = DateTime.UtcNow;
            List<ScoredMatchWithSubType> matches = new List<ScoredMatchWithSubType>();
            for (int i = 0; i < MatchmakingQueueInfo.GameConfig.SubTypes.Count; i++)
            {
                GameSubType subType = MatchmakingQueueInfo.GameConfig.SubTypes[i];
                using (M(MatchmakingTime, subType).NewTimer())
                {
                    int subTypeIndex = i;
                    List<ScoredMatchWithSubType> subQueueMatches = Matchmakers[subType.LocalizedName]
                        .GetMatchesRanked(queuedGroupsBySubtype[i], matchmakingIterationStartTime)
                        .Select(m => new ScoredMatchWithSubType(m, subTypeIndex))
                        .ToList();
                    matches.AddRange(subQueueMatches);
                    
                    if (subQueueMatches.Count > 0) {
                        string queueString = string.Join(
                            ", ",
                            queuedGroupsBySubtype[i]
                                .Select(
                                    g =>
                                        $"[{string.Join(
                                            ", ",
                                            GroupManager
                                                .GetGroupMembers(g.GroupID)
                                                .Select(LobbyServerUtils.GetHandle)
                                        )}] ({
                                        (matchmakingIterationStartTime - g.QueueTime).FormatMinutesSeconds()
                                    })"));
                        log.Info($"Queue snapshot {MatchmakingQueueInfo.GameType} {subType.LocalizedName}: {queueString}");
                    }
                }
            }

            return matches;
        }

        private void StartBestMatch(List<ScoredMatchWithSubType> matches)
        {
            foreach (var match in matches.OrderByDescending(m => m))
            {
                GameSubType subType = MatchmakingQueueInfo.GameConfig.SubTypes[match.SubTypeIndex];
                
                lock (GroupManager.Lock)
                {
                    HashSet<long> stillQueued = GetQueuedGroups(match.SubTypeIndex).ToHashSet();
                    if (match.Match.Groups.Any(g =>
                            !stillQueued.Contains(g.GroupID)
                            || !g.Is(GroupManager.GetGroup(g.GroupID))))
                    {
                        log.Info("One of the players in the best match "
                                 + $"left {MatchmakingQueueInfo.GameType} queue ({subType.LocalizedName}). "
                                 + "Retrying.");
                        continue;
                    }
                }
                
                StartMatch(match);

                DateTime now = DateTime.UtcNow;
                foreach (Matchmaker.MatchmakingGroup matchmakingGroup in match.Match.Groups)
                {
                    double timeInQueueSeconds = (now - matchmakingGroup.QueueTime).TotalSeconds;
                    for (int i = 0; i < matchmakingGroup.Players; i++)
                    {
                        M(TimeInQueue, subType).Observe(timeInQueueSeconds);
                    }
                }

                float prediction = Elo.GetPrediction(match.Match.TeamA.Elo, match.Match.TeamB.Elo);
                if (!float.IsNaN(prediction))
                {
                    M(PredictedChances, subType).Observe(MathF.Max(prediction, 1 - prediction));
                }

                break;
            }
        }

        private void TryMatch()
        {
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype = GetQueuedGroupsBySubtype();
            List<ScoredMatchWithSubType> matches = FindMatches(queuedGroupsBySubtype);
            StartBestMatch(matches);
        }

        private void StartMatch(ScoredMatchWithSubType match)
        {
            if (!CheckGameServerAvailable())
            {
                log.Warn("No available game server to start a match");
                return;
            }
            foreach (Matchmaker.MatchmakingGroup groupInfo in match.Match.Groups)
            {
                RemoveGroup(groupInfo.GroupID);
            }
            _ = MatchmakingManager.StartGameAsync(
                match.Match.TeamA.AccountIds, 
                match.Match.TeamB.AccountIds, 
                GameType,
                MatchmakingQueueInfo.GameConfig.SubTypes,
                match.SubTypeIndex)
                .LogError();
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

        public static string SelectMap(GameSubType gameSubType)
        {
            List<GameMapConfig> maps = gameSubType.GameMapConfigs.Where(x => x.IsActive).ToList();
            Random rand = new Random();
            int index = rand.Next(0, maps.Count);
            string selected = maps[index].Map;
            log.Info($"Selected {selected} out of {maps.Count} maps");
            return selected;
        }

        public void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType)
        {
            Elo.OnGameEnded(
                gameInfo,
                gameSummary,
                gameSubType,
                EloKey,
                GetConf(gameSubType.LocalizedName),
                DateTime.UtcNow,
                DB.Get().AccountDao.GetAccount,
                DB.Get().MatchHistoryDao.Find);
        }

        private void UpdateQueueInfo()
        {
            MatchmakingQueueInfo.QueuedPlayers = GetPlayerCount();
            MatchmakingQueueInfo.QueueStatus = ServerManager.IsAnyServerAvailable()
                ? QueueStatus.WaitingForHumans
                : QueueStatus.AllServersBusy;
        }

        private void SendQueueStatusNotifications()
        {
            List<long> queuedAccountIds = QueuedGroups.Keys.SelectMany(GroupManager.GetGroupMembers).ToList();
            var notify = new MatchmakingQueueStatusNotification
            {
                MatchmakingQueueInfo = MatchmakingQueueInfo
            };
            foreach (long accountId in queuedAccountIds)
            {
                SessionManager.GetClientConnection(accountId)?.Send(notify);
            }
        }

        // metrics helpers
        private TChild M<TChild>(Collector<TChild> metrics, GameSubType subType) where TChild : ChildBase
        {
            return metrics.WithLabels(GameTypeString, subType.LocalizedName);
        }

        private TChild M<TChild>(Collector<TChild> metrics) where TChild : ChildBase
        {
            return metrics.WithLabels(GameTypeString, "Total");
        }
    }
}
