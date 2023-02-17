using System;
using System.Collections.Generic;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(552)]
    public class LobbyCharacterInfo
    {
        public LobbyCharacterInfo Clone()
        {
            return (LobbyCharacterInfo) base.MemberwiseClone();
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

        public static CharacterModInfo RemoveDisabledMods(CharacterModInfo LastMods, CharacterType characterType)
        {
            foreach (var Character in GetChacterAbilityConfigOverrides())
            {
                if (Character.Key == characterType)
                {
                    for (int index = 0; index < Character.Value.AbilityConfigs.Length; index++)
                    {
                        if (Character.Value.GetAbilityConfig(index) is AbilityConfigOverride)
                        {
                            foreach (var Ability in Character.Value.GetAbilityConfig(index).AbilityModConfigs)
                            {
                                if (Ability.Value.AbilityIndex == 0 && Ability.Value.AbilityModIndex == LastMods.ModForAbility0) 
                                {
                                    LastMods.ModForAbility0 = 0;
                                }
                                if (Ability.Value.AbilityIndex == 1 && Ability.Value.AbilityModIndex == LastMods.ModForAbility1)
                                {
                                    LastMods.ModForAbility1 = 0;
                                }
                                if (Ability.Value.AbilityIndex == 2 && Ability.Value.AbilityModIndex == LastMods.ModForAbility2)
                                {
                                    LastMods.ModForAbility2 = 0;
                                }
                                if (Ability.Value.AbilityIndex == 3 && Ability.Value.AbilityModIndex == LastMods.ModForAbility3)
                                {
                                    LastMods.ModForAbility3 = 0;
                                }
                                if (Ability.Value.AbilityIndex == 4 && Ability.Value.AbilityModIndex == LastMods.ModForAbility4)
                                {
                                    LastMods.ModForAbility4 = 0;
                                }
                            }  
                        }
                    }
                }
            }
            return LastMods;
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
