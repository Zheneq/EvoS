using System.Collections.Generic;

namespace CentralServer.LobbyServer.Character
{
    public class CharacterConfigs
    {

        public static Dictionary<CharacterType, CharacterConfig> Characters = new Dictionary<CharacterType, CharacterConfig>()
        {
            {
                CharacterType.Archer,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.Archer,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.BattleMonk,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.BattleMonk,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.BazookaGirl,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.BazookaGirl,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Blaster,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Blaster,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Claymore,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Claymore,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Cleric,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.Cleric,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.DigitalSorceress,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.DigitalSorceress,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Dino,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Dino,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Exo,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Exo,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Fireborg,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Fireborg,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.FishMan,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.FishMan,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Gremlins,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Gremlins,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Gryd,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Gryd,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = true
                }
            },
            {
                CharacterType.Iceborg,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Iceborg,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Manta,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Manta,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Martyr,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.Martyr,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.NanoSmith,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.NanoSmith,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Neko,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Neko,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.RageBeast,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.RageBeast,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Rampart,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Rampart,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.RobotAnimal,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.RobotAnimal,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Samurai,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Samurai,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Scamp,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Scamp,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Scoundrel,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Scoundrel,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Sensei,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.Sensei,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Sniper,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Sniper,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Soldier,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Soldier,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.SpaceMarine,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.SpaceMarine,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Spark,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Support,
                    CharacterType = CharacterType.Spark,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.TeleportingNinja,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.TeleportingNinja,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Thief,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Thief,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Tracker,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Tracker,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Trickster,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.Trickster,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.Valkyrie,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.Tank,
                    CharacterType = CharacterType.Valkyrie,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            },
            {
                CharacterType.PunchingDummy,
                new CharacterConfig()
                {
                    AllowForBots = true,
                    AllowForPlayers = false,
                    CharacterRole = CharacterRole.Assassin,
                    CharacterType = CharacterType.PunchingDummy,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = true
                }
            },
            {
                CharacterType.PendingWillFill,
                new CharacterConfig()
                {
                    AllowForBots = false,
                    AllowForPlayers = true,
                    CharacterRole = CharacterRole.None,
                    CharacterType = CharacterType.PendingWillFill,
                    Difficulty = 1,
                    GameTypesProhibitedFrom = new List<GameType>(),
                    IsHidden = false
                }
            }
        };
    }
}
