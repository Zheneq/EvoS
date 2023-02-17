using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace CentralServer.LobbyServer.Character
{
    public class CharacterManager
    {
        public static LobbyServerPlayerInfo GetPunchingDummyPlayerInfo()
        {
            return new LobbyServerPlayerInfo
            {
                AccountId = 0,
                BannerID = -1,
                BotCanTaunt = true,
                CharacterInfo = new LobbyCharacterInfo
                {
                    CharacterType = CharacterType.PunchingDummy,
                    CharacterAbilityVfxSwaps = new CharacterAbilityVfxSwapInfo(),
                    CharacterCards = new CharacterCardInfo(),
                    CharacterLevel = 1,
                    CharacterLoadouts = new List<CharacterLoadout>
                    {
                        new CharacterLoadout(new CharacterModInfo(), new CharacterAbilityVfxSwapInfo(), "default")
                    },
                    CharacterMatches = 0,
                    CharacterMods = new CharacterModInfo(),
                    CharacterSkin = new CharacterVisualInfo(),
                    CharacterTaunts = new List<PlayerTauntData>()
                },
                ControllingPlayerId = 0,
                Difficulty = BotDifficulty.Stupid,
                EmblemID = -1,
                Handle = "PunchingDummy",
                IsGameOwner = false,
                IsLoadTestBot = false,
                IsNPCBot = true,
                PlayerId = 0,
                ReadyState = ReadyState.Ready,
                RibbonID = -1,
                TeamId = Team.TeamB,
                TitleID = -1,
            };
        }

        // Update LobbyCharacterInfo.RemoveDisabledMods if a mod is being added here
        public static Dictionary<CharacterType, CharacterAbilityConfigOverride> GetChacterAbilityConfigOverrides()
        {
            Dictionary<CharacterType, CharacterAbilityConfigOverride> overrides = new Dictionary<CharacterType,CharacterAbilityConfigOverride>();

            // Disable Phaedra's "AfterShock" mod
            CharacterAbilityConfigOverride MantaAbilityConfigOverride = new CharacterAbilityConfigOverride(CharacterType.Manta);
            MantaAbilityConfigOverride.AbilityConfigs[4] = new AbilityConfigOverride(CharacterType.Manta, 4)
            {
                AbilityModConfigs = new Dictionary<int, AbilityModConfigOverride>()
                {
                    {
                        1,
                        new AbilityModConfigOverride
                        {
                            AbilityIndex = 4,
                            AbilityModIndex = 1,
                            Allowed = false,
                            CharacterType = CharacterType.Manta
                        }
                    }
                }
            };
            overrides.Add(CharacterType.Manta, MantaAbilityConfigOverride);

            // Disable Titus' "Single Minded" mod
            CharacterAbilityConfigOverride ClaymoreAbilityConfigOverride = new CharacterAbilityConfigOverride(CharacterType.Claymore);
            ClaymoreAbilityConfigOverride.AbilityConfigs[1] = new AbilityConfigOverride(CharacterType.Claymore, 1)
            {
                AbilityModConfigs = new Dictionary<int, AbilityModConfigOverride>()
                {
                    {
                        3,
                        new AbilityModConfigOverride
                        {
                            AbilityIndex = 1,
                            AbilityModIndex = 3,
                            Allowed = false,
                            CharacterType = CharacterType.Claymore
                        }
                    }
                }
            };
            overrides.Add(CharacterType.Claymore, ClaymoreAbilityConfigOverride);


            return overrides;
        }
    }
}
