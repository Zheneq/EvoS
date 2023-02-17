using System;
using System.Collections.Generic;
using System.Linq;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(552)]
    public class LobbyCharacterInfo
    {
        public LobbyCharacterInfo Clone()
        {
            return (LobbyCharacterInfo)base.MemberwiseClone();
        }

        public CharacterType CharacterType;

        public CharacterVisualInfo CharacterSkin = default(CharacterVisualInfo);

        public CharacterCardInfo CharacterCards = default(CharacterCardInfo);

        public CharacterModInfo CharacterMods = default(CharacterModInfo);

        public CharacterAbilityVfxSwapInfo CharacterAbilityVfxSwaps = default(CharacterAbilityVfxSwapInfo);

        [EvosMessage(528)] public List<PlayerTauntData> CharacterTaunts = new List<PlayerTauntData>();

        [EvosMessage(544)] public List<CharacterLoadout> CharacterLoadouts = new List<CharacterLoadout>();

        public int CharacterMatches;

        public int CharacterLevel;

        public static LobbyCharacterInfo Of(PersistedCharacterData data)
        {
            CharacterComponent cc = data.CharacterComponent;
            return new LobbyCharacterInfo
            {
                CharacterType = data.CharacterType,
                CharacterSkin = cc.LastSkin,
                CharacterCards = cc.LastCards,
                CharacterMods = RemoveDisabledMods(cc.LastMods, data.CharacterType),
                CharacterAbilityVfxSwaps = cc.LastAbilityVfxSwaps,
                CharacterTaunts = cc.Taunts,
                CharacterLoadouts = cc.CharacterLoadouts,
                CharacterMatches = data.ExperienceComponent.Matches,
                CharacterLevel = data.ExperienceComponent.Level
            };
        }

        public static CharacterModInfo RemoveDisabledMods(CharacterModInfo lastMods, CharacterType characterType)
        {
            var characterAbilityConfigOverrides = GetChacterAbilityConfigOverrides();
            var abilityConfigs = characterAbilityConfigOverrides
                .Where(c => c.Key == characterType)
                .SelectMany(c => c.Value.AbilityConfigs)
                .OfType<AbilityConfigOverride>();

            foreach (var abilityConfig in abilityConfigs)
            {
                foreach (var abilityModConfig in abilityConfig.AbilityModConfigs)
                {
                    switch (abilityModConfig.Value.AbilityIndex)
                    {
                        case 0 when abilityModConfig.Value.AbilityModIndex == lastMods.ModForAbility0:
                            lastMods.ModForAbility0 = 0;
                            break;
                        case 1 when abilityModConfig.Value.AbilityModIndex == lastMods.ModForAbility1:
                            lastMods.ModForAbility1 = 0;
                            break;
                        case 2 when abilityModConfig.Value.AbilityModIndex == lastMods.ModForAbility2:
                            lastMods.ModForAbility2 = 0;
                            break;
                        case 3 when abilityModConfig.Value.AbilityModIndex == lastMods.ModForAbility3:
                            lastMods.ModForAbility3 = 0;
                            break;
                        case 4 when abilityModConfig.Value.AbilityModIndex == lastMods.ModForAbility4:
                            lastMods.ModForAbility4 = 0;
                            break;
                    }
                }
            }
            return lastMods;
        }

        public static Dictionary<CharacterType, CharacterAbilityConfigOverride> GetChacterAbilityConfigOverrides()
        {
            Dictionary<CharacterType, CharacterAbilityConfigOverride> overrides = new Dictionary<CharacterType, CharacterAbilityConfigOverride>();

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
