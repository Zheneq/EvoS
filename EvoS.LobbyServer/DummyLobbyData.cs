using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Game;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.LobbyServer.Utils;

namespace EvoS.LobbyServer
{
    public class DummyLobbyData
    {
        public static List<PersistedCharacterData> CreateCharacterDataList()
        {
            var data = new List<PersistedCharacterData>();

            foreach (CharacterType characterType in Enum.GetValues(typeof(CharacterType)))
            {
                var characterData = new PersistedCharacterData(characterType)
                {
                    CharacterComponent = {
                        Unlocked = true,
                        LastSelectedLoadout = 0,
                        CharacterLoadouts = new List<CharacterLoadout>(){
                            PlayerUtils.GetLoadout(0, characterType)
                        },
                        Skins = new List<PlayerSkinData>()
                        {
                            new PlayerSkinData() {
                                Unlocked = true,
                                Patterns = new List<PlayerPatternData>()
                                {
                                    new PlayerPatternData()
                                    {
                                        Colors = new List<PlayerColorData>()
                                        {
                                            new PlayerColorData() { Unlocked = true},
                                            new PlayerColorData() { Unlocked = true},
                                            new PlayerColorData() { Unlocked = true},
                                            new PlayerColorData() { Unlocked = true}
                                        },
                                        Unlocked = true,
                                    },
                                    new PlayerPatternData()
                                    {
                                        Colors = new List<PlayerColorData>()
                                        {
                                            new PlayerColorData() { Unlocked = true},
                                            new PlayerColorData() { Unlocked = true}
                                        },
                                        Unlocked = true,
                                    }
                                }
                            }
                        }
                    }
                };
                data.Add(characterData);
            }

            return data;
        }

        public static ServerQueueConfigurationUpdateNotification CreateServerQueueConfiguration()
        {
            return new ServerQueueConfigurationUpdateNotification
            {
                FreeRotationAdditions = new Dictionary<CharacterType, RequirementCollection>(),
                GameTypeAvailabilies = new Dictionary<GameType, GameTypeAvailability>
                {
                    {
                        GameType.Practice, new GameTypeAvailability
                        {
                            QueueableGroupSizes = new Dictionary<int, RequirementCollection> {{1, null}},
                            IsActive = true,
                            TeamAPlayers = 1,
                            TeamBBots = 2,
                            SubTypes = CreatePracticeGameSubTypes()
                        }
                    },
                    /*{
                        GameType.Coop, new GameTypeAvailability
                        {
                            IsActive = false
                        }
                    },
                    {
                        GameType.Ranked, new GameTypeAvailability
                        {
                            IsActive = false
                        }
                    },*/
                    {
                        GameType.PvP, new GameTypeAvailability
                        {
                            QueueableGroupSizes = new Dictionary<int, RequirementCollection>
                            {
                                {1, null},
                                {2, null},
                                {3, null},
                                {4, null},
                            },
                            IsActive = true,
                            TeamAPlayers = 4,
                            TeamBPlayers = 4,
                            BlockedExperienceAlternativeGameType = GameType.NewPlayerPvP,
                            BlockedExperienceEntries = new List<MatchmakingQueueConfig.QueueEntryExperience> {0},
                            GameLeavingPenalty = new GameLeavingPenalty
                            {
                                PointsGainedForLeaving = 2,
                                PointsForgivenForRejoining = 1,
                                PointsForgivenPerCompleteGameFinished = 1
                            },
                            Requirements = new RequirementCollection(),
                            SubTypes = new List<GameSubType>
                            {
                                new GameSubType
                                {
                                    LocalizedName = "GenericPvP@SubTypes",
                                    GameMapConfigs = new List<GameMapConfig>
                                    {
                                        new GameMapConfig
                                        {
                                            IsActive = true,
                                            Map = "CargoShip_Deathmatch"
                                        }
                                    },
                                    MaxMatchesGrantingXP = new Rate(),
                                    TeamAPlayers = -1,
                                    TeamBPlayers = -1,
                                    TeamABots = -1,
                                    TeamBBots = -1,
                                    PersistedStatBucket = PersistedStatBucket.Deathmatch_Unranked,
                                    LoadoutSelectionTimeoutOverride = TimeSpan.Zero,
                                }
                            }
                        }
                    },
                    /*{
                        GameType.Custom, new GameTypeAvailability
                        {
                            IsActive = false
                        }
                    }*/
                },
                TierInstanceNames = new List<LocalizationPayload>(),
                AllowBadges = true,
                NewPlayerPvPQueueDuration = 0
            };
        }

        private static List<GameSubType> CreatePracticeGameSubTypes()
        {
            return new List<GameSubType>
            {
                new GameSubType
                {
                    LocalizedName = "GenericPractice@SubTypes",
                    GameMapConfigs = new List<GameMapConfig>
                    {
                        new GameMapConfig
                        {
                            IsActive = true,
                            Map = "VR_Practice"
                        }
                    },
                    Mods = new List<GameSubType.SubTypeMods>
                    {
                        GameSubType.SubTypeMods.AllowPlayingLockedCharacters,
                        GameSubType.SubTypeMods.HumansHaveFirstSlots
                    },
                    RewardBucket = GameBalanceVars.GameRewardBucketType.NoRewards,
                    PersistedStatBucket = PersistedStatBucket.DoNotPersist,
                    TeamABots = -1,
                    TeamBBots = 2,
                    TeamAPlayers = -1,
                    TeamBPlayers = -1,
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
                            },
                            {
                                TeamCompositionRules.SlotTypes.A2, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.A4, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.A3, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.TeamB, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            }
                        }
                    }
                }
            };
        }

        public static LobbyCharacterInfo CreateLobbyCharacterInfo(CharacterType characterType)
        {
            return new LobbyCharacterInfo
            {
                CharacterType = characterType,
                CharacterSkin = new CharacterVisualInfo(0,0,0),
                CharacterCards = new CharacterCardInfo().Reset(),
                CharacterMods = new CharacterModInfo().Reset(),
                CharacterAbilityVfxSwaps = new CharacterAbilityVfxSwapInfo(),
                CharacterTaunts = new List<PlayerTauntData>(),
                CharacterLoadouts = CreateCharacterLoadouts()
            };
        }

        private static List<CharacterLoadout> CreateCharacterLoadouts()
        {
            return new List<CharacterLoadout>
            {
                new CharacterLoadout(new CharacterModInfo().Reset(), new CharacterAbilityVfxSwapInfo(), "Loadout #1"),
                new CharacterLoadout(new CharacterModInfo().Reset(), new CharacterAbilityVfxSwapInfo(), "Loadout #2")
            };
        }

        public static Dictionary<CharacterType, CharacterConfig> CreateCharacterConfigs()
        {
            var data = new Dictionary<CharacterType, CharacterConfig>();

            foreach (CharacterType characterType in Enum.GetValues(typeof(CharacterType)))
            {
                data.Add(characterType, new CharacterConfig
                {
                    CharacterType = characterType,
                    Difficulty = 1,
                    CharacterRole = CharacterRole.Assassin,
                    IsHidden = false,
                    AllowForBots = true,
                    AllowForPlayers = true,
                    IsHiddenFromFreeRotationUntil = DateTime.UnixEpoch
                });
            }

            return data;
        }

        public static GameAssignmentNotification CreatePracticeGameNotification(ClientConnection connection)
        {
            return new GameAssignmentNotification
            {
                Observer = false,
                Reconnection = false,
                GameInfo = CreatePracticeGameLobbyInfo(),
                GameResult = GameResult.NoResult,
                GameplayOverrides = CreateLobbyGameplayOverrides(),
                PlayerInfo = CreateLobbyPlayerInfo(connection)
            };
        }

        public static LobbyGameplayOverrides CreateLobbyGameplayOverrides()
        {
            return new LobbyGameplayOverrides
            {
                CharacterConfigs = CreateCharacterConfigs(),
                EnableHiddenCharacters = true,
                EnableAllMods = true,
                EnableQuests = false,
                EnableSeasons = false,
            };
        }

        public static LobbyGameInfo CreatePracticeGameLobbyInfo()
        {
            return new LobbyGameInfo
            {
                AcceptTimeout = TimeSpan.Zero,
                GameResult = GameResult.NoResult,
                GameServerAddress = null,
                GameServerHost = null,
                GameStatus = GameStatus.Launching,
                GameServerProcessCode = "",
                MonitorServerProcessCode = "",
                LoadoutSelectTimeout = TimeSpan.FromMinutes(1),
                SelectSubPhaseBan1Timeout = TimeSpan.FromMinutes(1),
                SelectSubPhaseBan2Timeout = TimeSpan.FromSeconds(30),
                SelectSubPhaseFreelancerSelectTimeout = TimeSpan.FromSeconds(30),
                SelectSubPhaseTradeTimeout = TimeSpan.FromSeconds(30),
                GameConfig = CreatePracticeGameConfig()
            };
        }

        public static LobbyGameConfig CreatePracticeGameConfig()
        {
            return new LobbyGameConfig
            {
                GameOptionFlags = GameOptionFlag.AutoLaunch | GameOptionFlag.NoInputIdleDisconnect |
                                  GameOptionFlag.EnableTeamAIOutput,
                GameType = GameType.Practice,
                InstanceSubTypeBit = 1,
                IsActive =  true,
                ResolveTimeoutLimit = 160,
                TeamAPlayers = 1,
                Map = "VR_Practice",
                SubTypes = CreatePracticeGameSubTypes()
            };
        }

        public static LobbyPlayerInfo CreateLobbyPlayerInfo(ClientConnection connection)
        {
            return new LobbyPlayerInfo
            {
                PlayerId = 0, // TODO
                CharacterInfo = CreateLobbyCharacterInfo(connection.SelectedCharacter),
                Handle = connection.UserName,
                AccountId = connection.AccountId,
                IsGameOwner = true,
                EffectiveClientAccessLevel = ClientAccessLevel.Admin
            };
        }

        public static LobbyServerPlayerInfo CreateLobbyServerPlayerInfo(ClientConnection connection)
        {
            // TODO
            return new LobbyServerPlayerInfo
            {
                TeamId = Team.TeamA,
                PlayerId = 0, // TODO
                CharacterInfo = CreateLobbyCharacterInfo(connection.SelectedCharacter),
                Handle = connection.UserName,
                AccountId = connection.AccountId,
                IsGameOwner = true,
                EffectiveClientAccessLevel = ClientAccessLevel.Admin
            };
        }

        public static GameInfoNotification CreatePracticeGameInfoNotification(ClientConnection connection)
        {
            LobbyPlayerInfo PlayerInfo = CreateLobbyPlayerInfo(connection);
            LobbyServerPlayerInfo ServerPlayerInfo = CreateLobbyServerPlayerInfo(connection);
            LobbyGameInfo GameInfo = CreatePracticeGameLobbyInfo();
            LobbyTeamInfo TeamInfo = new LobbyTeamInfo
            {
                TeamPlayerInfo = new List<LobbyPlayerInfo>
                    {
                       PlayerInfo
                    }
            };
            LobbyServerTeamInfo ServerTeamInfo = new LobbyServerTeamInfo()
            {
                TeamPlayerInfo = new List<LobbyServerPlayerInfo>
                    {
                        ServerPlayerInfo
                    }
            };

            // TODO we are not always supposed to be in the same process
            GameManagerHolder.CreateMatch(GameInfo, ServerTeamInfo);

            var response = new GameInfoNotification
            {
                GameInfo = GameInfo,
                PlayerInfo = PlayerInfo,
                TeamInfo = TeamInfo
            };
            response.GameInfo.GameServerAddress = "ws://127.0.0.1:6061";
            response.GameInfo.GameStatus = GameStatus.Launched;
            response.GameInfo.GameServerHost = "Practice Game Host";

            return response;
        }

        private static List<GameSubType> CreatePvPGameSubTypes()
        {
            return new List<GameSubType>
            {
                new GameSubType
                {
                    LocalizedName = "GenericPvP@SubTypes",
                    GameMapConfigs = new List<GameMapConfig>
                    {
                        new GameMapConfig
                        {
                            IsActive = true,
                            Map = "VR_Practice"
                        }
                    },
                    Mods = new List<GameSubType.SubTypeMods>
                    {
                        GameSubType.SubTypeMods.AllowPlayingLockedCharacters,
                        GameSubType.SubTypeMods.HumansHaveFirstSlots
                    },
                    RewardBucket = GameBalanceVars.GameRewardBucketType.NoRewards,
                    PersistedStatBucket = PersistedStatBucket.DoNotPersist,
                    TeamABots = -1,
                    TeamBBots = 2,
                    TeamAPlayers = -1,
                    TeamBPlayers = -1,
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
                            },
                            {
                                TeamCompositionRules.SlotTypes.A2, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.A4, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.A3, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            },
                            {
                                TeamCompositionRules.SlotTypes.TeamB, new FreelancerSet
                                    {Types = new List<CharacterType> {CharacterType.PunchingDummy}}
                            }
                        }
                    }
                }
            };
        }
    }
}
