using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace CentralServer.LobbyServer.Gamemode
{
    class GameModeManager
    {
        private const string ConfigPath = @"Config/GameSubTypes/";
        private static readonly Dictionary<GameType, string> ConfigFiles = new Dictionary<GameType, string>()
        {
            { GameType.PvP, "DeathMatch.json" },
            { GameType.Custom, "Custom.json" },
            { GameType.Coop, "Coop.json" }
        };

        private static Dictionary<string, bool> LocalizedNameToBans = new Dictionary<string, bool>();

        public static bool GetBans(string name)
        {
            if (LocalizedNameToBans.TryGetValue(name, out bool value))
            {
                return value;
            }
            return false;
        }


        public static Dictionary<GameType, GameTypeAvailability> GetGameTypeAvailabilities()
        {
            Dictionary<GameType, GameTypeAvailability> gameTypes = new Dictionary<GameType, GameTypeAvailability>();

            gameTypes.Add(GameType.Practice, GetPracticeGameTypeAvailability());
            gameTypes.Add(GameType.Coop, GetCoopGameTypeAvailability());
            gameTypes.Add(GameType.PvP, GetPvPGameTypeAvailability());
            gameTypes.Add(GameType.Ranked, GetRankedGameTypeAvailability());
            gameTypes.Add(GameType.Custom, GetCustomGameTypeAvailability());

            return gameTypes;
        }

        private static GameTypeAvailability GetPracticeGameTypeAvailability()
        {
            GameTypeAvailability type = new GameTypeAvailability();
            type.MaxWillFillPerTeam = 4;
            type.IsActive = LobbyConfiguration.GetGameTypePracticeAvailable();
            type.QueueableGroupSizes = new Dictionary<int, RequirementCollection> { { 1, null } };
            type.TeamAPlayers = 1;
            type.TeamBBots = 2;

            type.SubTypes = new List<GameSubType>()
            {
                new GameSubType {
                    LocalizedName = "GenericPractice@SubTypes",
                    GameMapConfigs = new List<GameMapConfig>{ new GameMapConfig(Maps.Skyway_Deathmatch, true) },
                    RewardBucket = GameBalanceVars.GameRewardBucketType.NoRewards,
                    PersistedStatBucket = PersistedStatBucket.DoNotPersist,
                    TeamAPlayers = 1,
                    TeamABots = 0,
                    TeamBPlayers = 0,
                    TeamBBots = 2,
                    Mods = new List<GameSubType.SubTypeMods>
                    {
                        GameSubType.SubTypeMods.AllowPlayingLockedCharacters,
                        GameSubType.SubTypeMods.HumansHaveFirstSlots
                    },
                    TeamComposition = new TeamCompositionRules
                    {
                        Rules = new Dictionary<TeamCompositionRules.SlotTypes, FreelancerSet>
                        {
                            {
                                TeamCompositionRules.SlotTypes.A1, new FreelancerSet
                                {
                                    Roles = new List<CharacterRole>
                                    {
                                        CharacterRole.Tank,
                                        CharacterRole.Assassin,
                                        CharacterRole.Support
                                    }
                                }
                            }, {
                                TeamCompositionRules.SlotTypes.B1, new FreelancerSet{Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            }, {
                                TeamCompositionRules.SlotTypes.B2, new FreelancerSet{Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            }
                        }
                    }
                }
            };

            return type;
        }

        private static GameTypeAvailability GetCoopGameTypeAvailability()
        {
            return new GameTypeAvailability
            {
                IsActive = LobbyConfiguration.GetGameTypeCoopAvailable(),
                MaxWillFillPerTeam = 4,
                SubTypes = LoadGameSubTypes(ConfigFiles[GameType.Coop])
            };
        }

        private static GameTypeAvailability GetPvPGameTypeAvailability()
        {
            return new GameTypeAvailability
            {
                IsActive = LobbyConfiguration.GetGameTypePvPAvailable(),
                MaxWillFillPerTeam = 4,
                SubTypes = LoadGameSubTypes(ConfigFiles[GameType.PvP])
            };
        }

        /// <summary>
        /// Reads a json file that represents a GameSubType
        /// </summary>
        /// <param name="filename">File name inside the folder 'Config/GameSubTypes/'</param>
        /// <returns>List of GameSubTypes loaded from the file</returns>
        private static List<GameSubType> LoadGameSubTypes(string filename)
        {
            // We need to exract bans cause its has [NonSerialized] cause client cant have it, then map it based of LocalizedName witch should be unique for all game modes we have
            string jsonString = new StreamReader(ConfigPath + filename).ReadToEnd();
            JArray jsonArray = JArray.Parse(jsonString);

            foreach (JObject item in jsonArray)
            {
                JToken localizedNameToken = item["LocalizedName"];
                JToken bansToken = item["Bans"];

                if (localizedNameToken != null && bansToken != null &&
                    localizedNameToken.Type == JTokenType.String &&
                    bansToken.Type == JTokenType.Boolean)
                {
                    string localizedName = localizedNameToken.ToString();
                    bool bans = (bool)bansToken;

                    LocalizedNameToBans[localizedName] = bans;  // Update the static dictionary
                }
            }

            // TODO: this always read from file, it could be stored in a cache
            JsonReader reader = new JsonTextReader(new StreamReader(ConfigPath + filename));
            try
            {
                return new JsonSerializer().Deserialize<List<GameSubType>>(reader);
            }
            finally { reader.Close(); }
        }

        private static GameTypeAvailability GetRankedGameTypeAvailability()
        {
            GameTypeAvailability type = new GameTypeAvailability();
            type.IsActive = LobbyConfiguration.GetGameTypeRankedAvailable();
            type.MaxWillFillPerTeam = 0;
            type.SubTypes = new List<GameSubType>();
            return type;
        }

        private static GameTypeAvailability GetCustomGameTypeAvailability()
        {
            return new GameTypeAvailability
            {
                IsActive = LobbyConfiguration.GetGameTypeCustomAvailable(),
                MaxWillFillPerTeam = 5,
                SubTypes = LoadGameSubTypes(ConfigFiles[GameType.Custom])
            };
        }

    }
}
