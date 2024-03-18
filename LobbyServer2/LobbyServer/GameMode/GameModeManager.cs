using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace CentralServer.LobbyServer.Gamemode
{
    class GameModeManager
    {
        private const string ConfigPath = @"Config/GameSubTypes/";
        private static readonly Dictionary<GameType, List<string>> ConfigFiles = new Dictionary<GameType, List<string>>()
        {
            { GameType.PvP, new List<string> { "DeathMatch.json" } },
            { GameType.Custom, new List<string> { "Custom.json", "CustomTournament.json" } }
        };

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
            GameTypeAvailability type = new GameTypeAvailability();
            type.IsActive = LobbyConfiguration.GetGameTypeCoopAvailable();
            type.MaxWillFillPerTeam = 0;
            type.SubTypes = new List<GameSubType>() {
                new GameSubType() {
                    TeamAPlayers = 4,
                    TeamABots = 0,
                    TeamBPlayers = 0,
                    TeamBBots = 4,

                    DuplicationRule = FreelancerDuplicationRuleTypes.noneInTeam,
                    GameMapConfigs = GameMapConfig.GetDeatmatchMaps(),
                    InstructionsToDisplay = GameSubType.GameLoadScreenInstructions.Default,

                }
            };
            return type;
        }

        private static GameTypeAvailability GetPvPGameTypeAvailability()
        {
            GameTypeAvailability type = new GameTypeAvailability();
            type.IsActive = LobbyConfiguration.GetGameTypePvPAvailable();
            type.MaxWillFillPerTeam = 4;
            List<GameSubType> subTypes = new List<GameSubType>();

            foreach (string file in ConfigFiles[GameType.PvP])
            {
                subTypes.Add(LoadGameSubType(file));
            }

            type.SubTypes = subTypes;
            return type;
        }

        /// <summary>
        /// Reads a json file that represents a GameSubType
        /// </summary>
        /// <param name="filename">File name inside the folder 'Config/GameSubTypes/'</param>
        /// <returns>A GameSubType loaded from the file</returns>
        private static GameSubType LoadGameSubType(string filename)
        {
            // TODO: this always read from file, it could be stored in a cache
            JsonReader reader = new JsonTextReader(new StreamReader(ConfigPath + filename));
            try
            {
                return new JsonSerializer().Deserialize<GameSubType>(reader);
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
            GameTypeAvailability type = new GameTypeAvailability();
            type.IsActive = LobbyConfiguration.GetGameTypeCustomAvailable();
            type.MaxWillFillPerTeam = 5;
            List<GameSubType> subTypes = new List<GameSubType>();

            foreach (string file in ConfigFiles[GameType.Custom])
            {
                subTypes.Add(LoadGameSubType(file));
            }

            type.SubTypes = subTypes;
            return type;
        }

    }
}
