using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static EvoS.Framework.Network.Static.GameSubType;

namespace CentralServer.LobbyServer.Stats
{
    public class StatsApi
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatsApi));
        private static StatsApi _instance;
        private readonly StatsConfiguration conf;
        private static HttpClient client;

        public StatsApi()
        {
            conf = LobbyConfiguration.GetStatsConfiguration();
            if (!conf.Enabled)
            {
                log.Info("StatsApi is not enabled");
                return;
            }
            if (!conf.ApiUrl.StartsWith("http"))
            {
                log.Info("StatsApi is not configured correctly");
                conf.Enabled = false;
                return;
            }
            log.Info("StatsApi Enabled");
            // Trim the end / if its there
            conf.ApiUrl = conf.ApiUrl.TrimEnd('/');
            client = new HttpClient();
            if (conf.ApiKey != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", conf.ApiKey);
            }
        }

        public static StatsApi Get()
        {
            return _instance ??= new StatsApi();
        }

        public static PersistedAccountData GetMentorStatus(PersistedAccountData account)
        {
            account.Mentor = false;
            if (Get().conf.Enabled == true)
            {
                try
                {
                    HttpResponseMessage responseMentor = client.GetAsync($"{Get().conf.ApiUrl}/discords?filters[playername][$eq]={Uri.EscapeDataString(account.Handle)}").Result;
                    responseMentor.EnsureSuccessStatusCode();
                    string responseBody = responseMentor.Content.ReadAsStringAsync().Result;
                    JObject json = JObject.Parse(responseBody);
                    JArray dataArray = (JArray)json["data"];
                    if (dataArray != null && dataArray.Count > 0)
                    {
                        bool mentor = dataArray[0]["attributes"]["mentor"].Value<bool>();
                        if (mentor)
                        {
                            account.Mentor = true;
                            log.Info($"Enabling Mentor status for {account.Handle}");
                        }
                    }
                }
                catch { }
            }
            return account;
        }

        public async Task ParseStats(LobbyGameInfo gameInfo, string serverName, string serverVersion, LobbyGameSummary gameSummary)
        {
            if (Get().conf.Enabled != true)
            {
                return;
            }

            try
            {
                if (gameSummary.GameResult != GameResult.TeamAWon
                    && gameSummary.GameResult != GameResult.TeamBWon)
                {
                    return;
                }

                string map = Maps.GetMapName[gameInfo.GameConfig.Map];
                string gameType = gameInfo.GameConfig.GameType.ToString();

                if (gameInfo.GameConfig.SubTypes != null)
                {
                    foreach (GameSubType subType in gameInfo.GameConfig.SubTypes)
                    {
                        if (subType.Mods != null && subType.Mods.Contains(SubTypeMods.RankedFreelancerSelection))
                        {
                            gameType = "Tournament";
                        }
                    }
                }

                string teamWin = gameSummary.GameResult == GameResult.TeamAWon ? "TeamA" : "TeamB";
                Guid guid = Guid.NewGuid();
                string guidString = guid.ToString("N");
                string numericString = string.Concat(guidString.Select(c => ((int)c).ToString("D3")));
                // Take the first 19 numbers
                string first19Numbers = new(numericString.Take(19).ToArray());
                string gameId = first19Numbers; // Bot will change this later to messageId from discord so linking in launcher works but for now we need an unique id we have GameServerProcessCode to change all this
                string score = $"{gameSummary.TeamAPoints}-{gameSummary.TeamBPoints}";
                string turns = gameSummary.NumOfTurns.ToString();
                string GameServerProcessCode = gameInfo.GameServerProcessCode;

                var gameData = new
                {
                    data = new
                    {
                        date = DateTime.UtcNow.ToString("o"), // ISO 8601 format
                        gameid = gameId,
                        teamwin = teamWin,
                        turns,
                        score,
                        map,
                        gametype = gameType,
                        server = serverName,
                        version = serverVersion,
                        GameServerProcessCode,
                        channelid = "" // bot will set this we dont have it,
                    }
                };

                string json = JsonConvert.SerializeObject(gameData);
                StringContent content = new(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync($"{Get().conf.ApiUrl}/games", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonConvert.DeserializeObject<JObject>(responseBody);
                    string id = responseJson["data"]["id"].ToString();

                    // Post player stats
                    foreach (PlayerGameSummary player in gameSummary.PlayerGameSummaryList)
                    {
                        string handle;
                        if (player.AccountId != 0)
                        {
                            PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
                            handle = account?.Handle ?? "UNKNOWN";
                        }
                        else
                        {
                            handle = player.CharacterName;
                        }

                        bool Deadliest = false;
                        bool Supportiest = false;
                        bool Tankiest = false;
                        bool MostDecorated = false;

                        BadgeAndParticipantInfo badges = gameSummary.BadgeAndParticipantsInfo
                            .FirstOrDefault(p => p.ActorIndex == player.ActorIndex);

                        if (badges != null && badges.TopParticipationEarned != null)
                        {
                            Deadliest = badges.TopParticipationEarned.Contains(TopParticipantSlot.Deadliest);
                            Supportiest = badges.TopParticipationEarned.Contains(TopParticipantSlot.Supportiest);
                            Tankiest = badges.TopParticipationEarned.Contains(TopParticipantSlot.Tankiest);
                            MostDecorated = badges.TopParticipationEarned.Contains(TopParticipantSlot.MostDecorated);
                        }

                        var playerGameData = new
                        {
                            data = new
                            {
                                game_id = gameId,
                                user = handle,
                                character = player.CharacterName,
                                takedowns = player.NumAssists,
                                deaths = player.NumDeaths,
                                deathblows = player.NumKills,
                                damage = player.TotalPlayerDamage,
                                healing = player.GetTotalHealingFromAbility() + player.TotalPlayerAbsorb,
                                damage_received = player.TotalPlayerDamageReceived,
                                team = player.IsInTeamA() ? "TeamA" : "TeamB",
                                game = id,
                                gametype = gameType,
                                // Additional stats
                                player.TotalHealingReceived,
                                player.TotalPlayerAbsorb,
                                player.PowerupsCollected,
                                player.DamageAvoidedByEvades,
                                player.MyIncomingDamageReducedByCover,
                                player.MyOutgoingExtraDamageFromEmpowered,
                                player.MyOutgoingReducedDamageFromWeakened,
                                player.MovementDeniedByMe,
                                player.EnemiesSightedPerTurn,
                                player.DashCatalystUsed,
                                player.DashCatalystName,
                                player.CombatCatalystUsed,
                                player.CombatCatalystName,
                                player.PrepCatalystUsed,
                                player.PrepCatalystName,
                                Deadliest,
                                Supportiest,
                                Tankiest,
                                MostDecorated
                            }
                        };

                        string jsonPlayer = JsonConvert.SerializeObject(playerGameData);
                        StringContent contentPlayer = new(jsonPlayer, Encoding.UTF8, "application/json");

                        HttpResponseMessage responsePlayer = await client.PostAsync($"{Get().conf.ApiUrl}/stats", contentPlayer);

                        if (responsePlayer.IsSuccessStatusCode)
                        {
                            string responseBodyAdvanced = await responsePlayer.Content.ReadAsStringAsync();
                            var responseJsonAdvanced = JsonConvert.DeserializeObject<JObject>(responseBodyAdvanced);
                            string idAdvanced = responseJsonAdvanced["data"]["id"].ToString();
                            foreach (AbilityGameSummary abilityGameSummary in player.AbilityGameSummaryList)
                            {
                                var playerAbilityData = new
                                {
                                    data = new
                                    {
                                        stat = idAdvanced,
                                        abilityGameSummary.AbilityClassName,
                                        abilityGameSummary.AbilityName,
                                        abilityGameSummary.ActionType,
                                        abilityGameSummary.CastCount,
                                        abilityGameSummary.ModName,
                                        abilityGameSummary.TauntCount,
                                        abilityGameSummary.TotalAbsorb,
                                        abilityGameSummary.TotalDamage,
                                        abilityGameSummary.TotalEnergyGainOnSelf,
                                        abilityGameSummary.TotalEnergyGainToOthers,
                                        abilityGameSummary.TotalEnergyLossToOthers,
                                        abilityGameSummary.TotalHealing,
                                        abilityGameSummary.TotalPotentialAbsorb,
                                        abilityGameSummary.TotalTargetsHit
                                    }
                                };
                                string jsonAbility = JsonConvert.SerializeObject(playerAbilityData);
                                StringContent contentAbility = new(jsonAbility, Encoding.UTF8, "application/json");

                                HttpResponseMessage responseAbility = await client.PostAsync($"{Get().conf.ApiUrl}/advancedstats", contentAbility);
                                string responseBodyAdvanced1 = await responseAbility.Content.ReadAsStringAsync();

                                if (!responseAbility.IsSuccessStatusCode)
                                {
                                    log.Error($"Failed to post player ability data. Status code: {responseAbility.StatusCode}. Reason: {responseAbility.ReasonPhrase}");
                                }
                            }

                        }
                        else
                        {
                            log.Error($"Failed to post player stats data. Status code: {responsePlayer.StatusCode}. Reason: {responsePlayer.ReasonPhrase}");
                        }
                    }

                }
                else
                {
                    log.Error($"Failed to post game data. Status code: {response.StatusCode}. Reason: {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to post game data: {e.Message}");
            }
        }
    }
}