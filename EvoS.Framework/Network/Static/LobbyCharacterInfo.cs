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

        // Incase players still had those mods selected after being removed or used a not so legal way to set a mod
        public static CharacterModInfo RemoveDisabledMods(CharacterModInfo LastMods, CharacterType characterType)
        {
            // Remove "Single Minded" mod 
            if (characterType.Equals(CharacterType.Claymore) || LastMods.ModForAbility1 == 3) LastMods.ModForAbility1 = 0;
            // Remove "AfterShock" mod
            if (characterType.Equals(CharacterType.Manta) || LastMods.ModForAbility4 == 1) LastMods.ModForAbility4 = 0;
            return LastMods;
        }
    }
}
