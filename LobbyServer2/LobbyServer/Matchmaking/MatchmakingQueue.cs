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

        private readonly bool RankedMatchmaking;
        private MatchmakingConfigBundle Conf = new(); // TODO move to MatchmakerRanked
        private readonly Dictionary<string, Matchmaker> Matchmakers;
        private readonly Dictionary<string, AsymmetricSubTypeDescriptor> AsymmetricDescriptors = new();
        
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

        public IEnumerable<long> GetRawQueuedGroups()
        {
            return QueuedGroups.OrderBy(kv => kv.Value).Select(kv => kv.Key);
        }
        
        public IEnumerable<long> GetRawQueuedGroups(int subTypeIndex)
        {
            uint subTypeFlag = 1U << subTypeIndex;
            return GetRawQueuedGroups()
                .Where(groupId => (GroupManager.GetGroupSubTypeMask(groupId) & subTypeFlag) != 0);
        }

        public GameSubType GetSubType(int subTypeIndex) => MatchmakingQueueInfo.GameConfig.SubTypes[subTypeIndex];

        public int SubTypeCount => MatchmakingQueueInfo.GameConfig.SubTypes.Count;
        
        public List<List<long>> GetRawQueuedGroupsBySubType()
        {
            var res = new List<List<long>>();
            for (int i = 0; i < SubTypeCount; i++)
            {
                res.Add(GetRawQueuedGroups(i).ToList());
            }

            return res;
        }
        
        public List<List<long>> GetRawQueuedPlayersBySubType()
        {
            var res = new List<List<long>>();
            for (int i = 0; i < SubTypeCount; i++)
            {
                res.Add(GetRawQueuedGroups(i)
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
                for (int i = 0; i < SubTypeCount; i++)
                {
                    GameSubType subType = GetSubType(i);
                    M(QueueSize, subType).Set(GetRawPlayerCount(i));
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
                ? new MatchmakerRanked(GameType, st, () => GetConf(st.LocalizedName))
                : st.Mods is not null && st.Mods.Contains(GameSubType.SubTypeMods.AntiSocial)
                    ? new MatchmakerSingleGroup(GameType, st)
                    : new MatchmakerFifo(GameType, st);
        }

        public void RegisterAsymmetricSubType(
            string localizedName,
            string baseSubTypeName,
            int numControlledCharacters,
            TimeSpan turnTime)
        {
            List<GameSubType> subTypes = MatchmakingQueueInfo.GameConfig.SubTypes;
            int baseIndex = subTypes.FindIndex(st => st.LocalizedName == baseSubTypeName);
            if (baseIndex < 0)
            {
                log.Error($"Cannot register asymmetric subtype {localizedName}: base subtype '{baseSubTypeName}' not found");
                return;
            }
            // The first descriptor registered for a given base owns the active matchmaker pool.
            bool hasPrimary = AsymmetricDescriptors.Values.Any(d => d.BaseSubTypeIndex == baseIndex && !d.SkipMatchmaking);

            var descriptor = new AsymmetricSubTypeDescriptor
            {
                LocalizedName = localizedName,
                BaseSubTypeName = baseSubTypeName,
                NumControlledCharacters = numControlledCharacters,
                BaseSubTypeIndex = baseIndex,
                SkipMatchmaking = hasPrimary,
                TurnTime = turnTime,
            };
            GameSubType advertised = descriptor.CreateAdvertisedSubType(subTypes[baseIndex]);
            subTypes.Add(advertised);
            descriptor.SubTypeIndex = subTypes.Count - 1;

            Matchmakers[advertised.LocalizedName] = MatchmakerFactory(advertised);
            AsymmetricDescriptors[advertised.LocalizedName] = descriptor;
            log.Info($"Registered asymmetric subtype '{advertised.LocalizedName}' "
                     + $"(N={descriptor.NumControlledCharacters}, skip mm={descriptor.SkipMatchmaking}) "
                     + $"derived from '{descriptor.BaseSubTypeName}'");
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
            var groupInfo = GroupManager.GetGroup(groupId);
            GroupManager.UpdateSelectedSubTypes(groupInfo, false);
            log.Info($"Selected sub type mask for group {groupId}: {DebugFormatSubTypeMask(groupInfo.SubTypeMask)}");

            // TODO also check QueueRequirement
            
            added = QueuedGroups.TryAdd(groupId, DateTime.UtcNow);
            UpdateQueueInfo();
            if (added)
            {
                log.Info($"Added group {groupId} to {GameType} queue "
                         + $"with subqueues {DebugFormatSubTypeMask(groupInfo.SubTypeMask)}");
                log.Info($"{GetPlayerCount()} players in {GameType} queue ({QueuedGroups.Count} groups)");
            }
            else
            {
                log.Error($"Failed to add group {groupId} to {GameType} queue");
                GroupManager.UpdateSelectedSubTypes(groupInfo);
            }

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

        public int GetRawPlayerCount(int subTypeIndex)
        {
            return GetRawQueuedGroups(subTypeIndex)
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

        private Dictionary<int, List<Matchmaker.MatchmakingGroup>> GetAndConvertQueuedGroups()
        {
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype =
                new Dictionary<int, List<Matchmaker.MatchmakingGroup>>();

            lock (GroupManager.Lock)
            {
                for (int i = 0; i < SubTypeCount; i++)
                {
                    GameSubType subType = GetSubType(i);

                    List<Matchmaker.MatchmakingGroup> queuedGroups;
                    if (AsymmetricDescriptors.TryGetValue(subType.LocalizedName, out var descriptor))
                    {
                        if (descriptor.SkipMatchmaking)
                        {
                            queuedGroupsBySubtype[i] = [];
                            continue;
                        }

                        queuedGroups = [];
                        foreach (AsymmetricSubTypeDescriptor relDesc in GetDescriptorsForBase(descriptor.BaseSubTypeIndex))
                        {
                            queuedGroups.AddRange(GetAndConvertRawQueuedGroups(relDesc.SubTypeIndex, relDesc.NumControlledCharacters));
                        }
                        queuedGroups.AddRange(GetAndConvertRawQueuedGroups(descriptor.BaseSubTypeIndex, 1));
                    }
                    else
                    {
                        queuedGroups = GetAndConvertRawQueuedGroups(i, 1);
                    }
                    queuedGroupsBySubtype[i] = queuedGroups;
                }
            }

            return queuedGroupsBySubtype;
        }

        // TODO This logic has to be in sync with GetAndConvertQueuedGroups. Unite them.
        private List<long> GetEffectiveQueuedGroups(int subTypeIndex)
        {
            GameSubType subType = GetSubType(subTypeIndex);

            List<long> queuedGroups;
            if (AsymmetricDescriptors.TryGetValue(subType.LocalizedName, out var descriptor))
            {
                if (descriptor.SkipMatchmaking)
                {
                    return [];
                }

                queuedGroups = [];
                foreach (AsymmetricSubTypeDescriptor relDesc in GetDescriptorsForBase(descriptor.BaseSubTypeIndex))
                {
                    queuedGroups.AddRange(GetRawQueuedGroups(relDesc.SubTypeIndex));
                }
                queuedGroups.AddRange(GetRawQueuedGroups(descriptor.BaseSubTypeIndex));
            }
            else
            {
                queuedGroups = GetRawQueuedGroups(subTypeIndex).ToList();
            }
            return queuedGroups;
        }

        private IEnumerable<AsymmetricSubTypeDescriptor> GetDescriptorsForBase(int baseSubTypeIndex)
        {
            return AsymmetricDescriptors.Values.Where(d => d.BaseSubTypeIndex == baseSubTypeIndex);
        }

        private List<Matchmaker.MatchmakingGroup> GetAndConvertRawQueuedGroups(int subTypeIndex, int numControlledCharacters)
        {
            string eloKey = Elo.GetEloKey(GameType, GetSubType(subTypeIndex));
            return GetRawQueuedGroups(subTypeIndex)
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

                    return new Matchmaker.MatchmakingGroup(
                        group.GroupId,
                        group.Members
                            .Select(id => new QueuePlayerData(id, eloKey, subTypeIndex, numControlledCharacters))
                            .ToList(),
                        queueTime);
                })
                .Where(g => g is not null)
                .ToList();
        }

        private List<ScoredMatchWithSubType> FindMatches(
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype)
        {
            DateTime matchmakingIterationStartTime = DateTime.UtcNow;
            List<ScoredMatchWithSubType> matches = new List<ScoredMatchWithSubType>();
            bool hasBaseTypeMatched = false;
            for (int i = 0; i < SubTypeCount; i++)
            {
                GameSubType subType = GetSubType(i);

                // If any base subtype formed a match, skip all asymmetric variants
                if (AsymmetricDescriptors.TryGetValue(subType.LocalizedName, out _) && hasBaseTypeMatched)
                {
                    continue;
                }

                using (M(MatchmakingTime, subType).NewTimer())
                {
                    int subTypeIndex = i;
                    List<ScoredMatchWithSubType> subQueueMatches = Matchmakers[subType.LocalizedName]
                        .GetMatchesRanked(queuedGroupsBySubtype[i], matchmakingIterationStartTime)
                        .Select(m => new ScoredMatchWithSubType(m, subTypeIndex))
                        .ToList();
                    matches.AddRange(subQueueMatches);

                    if (subQueueMatches.Count > 0)
                    {
                        if (!AsymmetricDescriptors.ContainsKey(subType.LocalizedName))
                        {
                            hasBaseTypeMatched = true;
                        }
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
                GameSubType subType = GetSubType(match.SubTypeIndex);
                
                lock (GroupManager.Lock)
                {
                    HashSet<long> stillQueued = GetEffectiveQueuedGroups(match.SubTypeIndex).ToHashSet();
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
            Dictionary<int, List<Matchmaker.MatchmakingGroup>> queuedGroupsBySubtype = GetAndConvertQueuedGroups();
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

            var subTypeIndex = match.SubTypeIndex;
            if (AsymmetricDescriptors.TryGetValue(GetSubType(match.SubTypeIndex).LocalizedName, out var descriptor))
            {
                subTypeIndex = descriptor.BaseSubTypeIndex;
            }
            
            _ = MatchmakingManager.StartGameAsync(
                match.Match.TeamA.MatchPlayerDataList,
                match.Match.TeamB.MatchPlayerDataList,
                GameType,
                MatchmakingQueueInfo.GameConfig.SubTypes,
                subTypeIndex)
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

        public void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary, GameSubType gameSubType, List<MatchPlayerData> players)
        {
            Elo.OnGameEnded(
                gameInfo,
                gameSummary,
                players,
                GetConf(gameSubType.LocalizedName),
                DateTime.UtcNow,
                DB.Get().AccountDao.GetAccount,
                DB.Get().MatchHistoryDao.Find,
                DB.Get().AccountDao.UpdateExperienceComponent);
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

        public ushort FilterSubTypeMask(GroupInfo groupInfo, ushort selectedSubTypeMask)
        {
            ushort mask = FilterSubTypeMaskForGroup(groupInfo, selectedSubTypeMask);
            mask = FilterSubTypeMaskForEligibility(groupInfo, mask);

            if (mask == 0)
            {
                mask = groupInfo.IsSolo()
                    ? (ushort)1
                    : GetFallbackSubTypeMaskAllowedForGroups(selectedSubTypeMask);

                log.Info($"No valid subqueues selected for group {groupInfo.GroupId}, "
                         + $"falling back to subtype mask {DebugFormatSubTypeMask(mask)}");
            }
            
            return mask;
        }
        
        public ushort FilterSubTypeMaskForGroup(GroupInfo groupInfo, ushort selectedSubTypeMask)
        {
            if (groupInfo.IsSolo())
            {
                return selectedSubTypeMask;
            }

            ushort allowedSubTypeMask = GetSubTypeMaskAllowedForGroups();
            log.Info($"Selected for group {groupInfo.GroupId}: {DebugFormatSubTypeMask(selectedSubTypeMask)}, "
                     + $"allowed for groups: {DebugFormatSubTypeMask(allowedSubTypeMask)}");

            return (ushort)(allowedSubTypeMask & selectedSubTypeMask);
        }

        // TODO generalize into queue requirement
        public ushort FilterSubTypeMaskForEligibility(GroupInfo groupInfo, ushort selectedSubTypeMask)
        {
            ushort mask = selectedSubTypeMask;
            List<GameSubType> subTypes = MatchmakingQueueInfo.GameConfig.SubTypes;
            for (int i = 0; i < subTypes.Count; i++)
            {
                if ((mask & (1u << i)) != 0
                    && AsymmetricDescriptors.TryGetValue(subTypes[i].LocalizedName, out var descriptor)
                    && descriptor != null)
                {
                    var account = DB.Get().AccountDao.GetAccount(groupInfo.Leader);
                    if (account == null || !account.AccountComponent.IsVipOrHigher())
                    {
                        log.Info($"{account?.AccountId}/{account?.Handle} attempted to queue for asymmetric");
                        mask &= (ushort)~(1u << i);
                    }
                }
            }

            return mask;
        }

        private ushort GetSubTypeMaskAllowedForGroups()
        {
            ushort allowedSubTypeMask = 0;

            List<GameSubType> subTypes = MatchmakingQueueInfo.GameConfig.SubTypes;
            for (int i = 0; i < subTypes.Count; i++)
            {
                if (!subTypes[i].Mods.Contains(GameSubType.SubTypeMods.NotAllowedForGroups))
                {
                    allowedSubTypeMask |= (ushort)(1u << i);
                }
            }

            return allowedSubTypeMask;
        }

        private ushort GetFallbackSubTypeMaskAllowedForGroups(ushort selectedSubTypeMask)
        {
            List<GameSubType> subTypes = MatchmakingQueueInfo.GameConfig.SubTypes;
            
            bool useAntiSocial = false;
            bool isAntiSocial = true;
            if (GameType == GameType.Coop)
            {
                // we need to keep the selected value of AI teammates flag in Coop
                useAntiSocial = true;
                for (int i = 0; i < subTypes.Count; i++)
                {
                    if ((selectedSubTypeMask & (1u << i)) != 0)
                    {
                        isAntiSocial = subTypes[i].Mods.Contains(GameSubType.SubTypeMods.AntiSocial);
                        break;
                    }
                }
            }

            // find first valid subtype
            for (int i = 0; i < subTypes.Count; i++)
            {
                var mods = subTypes[i].Mods;
                bool isAllowedForGroups = !mods.Contains(GameSubType.SubTypeMods.NotAllowedForGroups);
                bool isCorrectAntiSocial =
                    !useAntiSocial || isAntiSocial == mods.Contains(GameSubType.SubTypeMods.AntiSocial);
                if (isAllowedForGroups && isCorrectAntiSocial)
                {
                    return (ushort)(1u << i);
                }
            }

            return 0;
        }

        private List<GameSubType> GetSubTypesFromMask(ushort mask)
        {
            return MatchmakingQueueInfo.GameConfig.SubTypes
                .Where((t, i) => (mask & (1u << i)) != 0)
                .ToList();
        }

        public string DebugFormatSubTypeMask(ushort subTypeMask)
        {
            return $"<{subTypeMask}> "
                   + string.Join(", ", GetSubTypesFromMask(subTypeMask).Select(st => st.LocalizedName));
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
