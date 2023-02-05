using System;
using System.Collections.Generic;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(527, typeof(CharacterComponent))]
    public class CharacterComponent : ICloneable
    {
        public CharacterComponent()
        {
            this.Skins = new List<PlayerSkinData>();
            this.Taunts = new List<PlayerTauntData>();
            this.Mods = new List<PlayerModData>();
            this.AbilityVfxSwaps = new List<PlayerAbilityVfxSwapData>();
            this.LastSkin = default(CharacterVisualInfo);
            this.LastCards = default(CharacterCardInfo);
            this.LastMods = default(CharacterModInfo);
            this.LastRankedMods = default(CharacterModInfo);
            this.LastAbilityVfxSwaps = default(CharacterAbilityVfxSwapInfo);
            this.CharacterLoadouts = new List<CharacterLoadout>();
            this.CharacterLoadoutsRanked = new List<CharacterLoadout>();
            this.NumCharacterLoadouts = 0;
            this.LastSelectedLoadout = -1;
            this.LastSelectedRankedLoadout = -1;
        }

        public void UnlockSkinsAndTaunts(CharacterType character)
        {
            if (EvosStoreConfiguration.AreTauntsFree())
            {
                Unlocked = true;
                Taunts = new List<PlayerTauntData>();
                for (int i = 0; i < 9; i++)
                {
                    Taunts.Add(new PlayerTauntData() { Unlocked = true });
                }
            }
            UnlockSkins(character);
        }

        public void UnlockSkins(CharacterType characterType)
        {
            if (EvosStoreConfiguration.AreSkinsFree())
            {
                switch (characterType)
                {
                    case CharacterType.BattleMonk:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0);
                        break;
                    case CharacterType.BazookaGirl:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 2, 0); UnlockSkinPatternColor(0, 3, 0);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8); UnlockSkinPatternColor(2, 0, 9);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7);
                        break;
                    case CharacterType.DigitalSorceress:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8);
                        break;
                    case CharacterType.Gremlins:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8);
                        break;
                    case CharacterType.NanoSmith:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8);
                        break;
                    case CharacterType.RageBeast:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8); UnlockSkinPatternColor(1, 0, 9);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        break;
                    case CharacterType.RobotAnimal:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5); UnlockSkinPatternColor(1, 1, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8);
                        break;
                    case CharacterType.Scoundrel:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15); UnlockSkinPatternColor(0, 0, 16); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4);
                        break;
                    case CharacterType.Sniper:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7);
                        UnlockSkinPatternColor(4, 0, 0); UnlockSkinPatternColor(4, 0, 1); UnlockSkinPatternColor(4, 0, 2); UnlockSkinPatternColor(4, 0, 3); UnlockSkinPatternColor(4, 0, 4); UnlockSkinPatternColor(4, 0, 5); UnlockSkinPatternColor(4, 0, 6); UnlockSkinPatternColor(4, 0, 7); UnlockSkinPatternColor(4, 0, 8); UnlockSkinPatternColor(4, 0, 9);
                        break;
                    case CharacterType.SpaceMarine:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5); UnlockSkinPatternColor(0, 1, 6); UnlockSkinPatternColor(0, 1, 7);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        break;
                    case CharacterType.Spark:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5); UnlockSkinPatternColor(0, 2, 0);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7);
                        UnlockSkinPatternColor(4, 0, 0); UnlockSkinPatternColor(4, 0, 1); UnlockSkinPatternColor(4, 0, 2); UnlockSkinPatternColor(4, 0, 3); UnlockSkinPatternColor(4, 0, 4); UnlockSkinPatternColor(4, 0, 5); UnlockSkinPatternColor(4, 0, 6);
                        break;
                    case CharacterType.TeleportingNinja:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        break;
                    case CharacterType.Thief:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        UnlockSkinPatternColor(4, 0, 0); UnlockSkinPatternColor(4, 0, 1); UnlockSkinPatternColor(4, 0, 2); UnlockSkinPatternColor(4, 0, 3); UnlockSkinPatternColor(4, 0, 4); UnlockSkinPatternColor(4, 0, 5); UnlockSkinPatternColor(4, 0, 6); UnlockSkinPatternColor(4, 0, 7); UnlockSkinPatternColor(4, 0, 8);
                        break;
                    case CharacterType.Tracker:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 1, 0); UnlockSkinPatternColor(0, 1, 1); UnlockSkinPatternColor(0, 1, 2); UnlockSkinPatternColor(0, 1, 3); UnlockSkinPatternColor(0, 1, 4); UnlockSkinPatternColor(0, 1, 5);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 1, 0); UnlockSkinPatternColor(1, 1, 1); UnlockSkinPatternColor(1, 1, 2); UnlockSkinPatternColor(1, 1, 3); UnlockSkinPatternColor(1, 1, 4); UnlockSkinPatternColor(1, 1, 5);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        break;
                    case CharacterType.Trickster:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8); UnlockSkinPatternColor(2, 0, 9);
                        break;
                    case CharacterType.Rampart:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8); UnlockSkinPatternColor(3, 0, 9);
                        break;
                    case CharacterType.Claymore:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15); UnlockSkinPatternColor(0, 0, 16);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6); UnlockSkinPatternColor(3, 0, 7); UnlockSkinPatternColor(3, 0, 8); UnlockSkinPatternColor(3, 0, 9);
                        UnlockSkinPatternColor(4, 0, 0); UnlockSkinPatternColor(4, 0, 1); UnlockSkinPatternColor(4, 0, 2); UnlockSkinPatternColor(4, 0, 3); UnlockSkinPatternColor(4, 0, 4); UnlockSkinPatternColor(4, 0, 5); UnlockSkinPatternColor(4, 0, 6);
                        break;
                    case CharacterType.Blaster:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8); UnlockSkinPatternColor(1, 0, 9); UnlockSkinPatternColor(1, 0, 10);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6);
                        break;
                    case CharacterType.FishMan:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        break;
                    case CharacterType.Exo:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8); UnlockSkinPatternColor(2, 0, 9); UnlockSkinPatternColor(2, 0, 10); UnlockSkinPatternColor(2, 0, 11);
                        break;
                    case CharacterType.Soldier:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        break;
                    case CharacterType.Martyr:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        break;
                    case CharacterType.Sensei:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8); UnlockSkinPatternColor(1, 0, 9);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5); UnlockSkinPatternColor(2, 0, 6); UnlockSkinPatternColor(2, 0, 7); UnlockSkinPatternColor(2, 0, 8);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5); UnlockSkinPatternColor(3, 0, 6);
                        break;
                    case CharacterType.PendingWillFill:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2);
                        break;
                    case CharacterType.Manta:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5);
                        UnlockSkinPatternColor(3, 0, 0); UnlockSkinPatternColor(3, 0, 1); UnlockSkinPatternColor(3, 0, 2); UnlockSkinPatternColor(3, 0, 3); UnlockSkinPatternColor(3, 0, 4); UnlockSkinPatternColor(3, 0, 5);
                        break;
                    case CharacterType.Valkyrie:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        break;
                    case CharacterType.Archer:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        break;
                    case CharacterType.Samurai:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13); UnlockSkinPatternColor(0, 0, 14); UnlockSkinPatternColor(0, 0, 15);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        break;
                    case CharacterType.Gryd:
                        UnlockSkinPatternColor(0, 0, 0);
                        break;
                    case CharacterType.Cleric:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        UnlockSkinPatternColor(2, 0, 0); UnlockSkinPatternColor(2, 0, 1); UnlockSkinPatternColor(2, 0, 2); UnlockSkinPatternColor(2, 0, 3); UnlockSkinPatternColor(2, 0, 4); UnlockSkinPatternColor(2, 0, 5);
                        break;
                    case CharacterType.Neko:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        break;
                    case CharacterType.Scamp:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5);
                        break;
                    case CharacterType.Dino:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8); UnlockSkinPatternColor(0, 0, 9); UnlockSkinPatternColor(0, 0, 10); UnlockSkinPatternColor(0, 0, 11); UnlockSkinPatternColor(0, 0, 12); UnlockSkinPatternColor(0, 0, 13);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6); UnlockSkinPatternColor(1, 0, 7); UnlockSkinPatternColor(1, 0, 8);
                        break;
                    case CharacterType.Iceborg:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        break;
                    case CharacterType.Fireborg:
                        UnlockSkinPatternColor(0, 0, 0); UnlockSkinPatternColor(0, 0, 1); UnlockSkinPatternColor(0, 0, 2); UnlockSkinPatternColor(0, 0, 3); UnlockSkinPatternColor(0, 0, 4); UnlockSkinPatternColor(0, 0, 5); UnlockSkinPatternColor(0, 0, 6); UnlockSkinPatternColor(0, 0, 7); UnlockSkinPatternColor(0, 0, 8);
                        UnlockSkinPatternColor(1, 0, 0); UnlockSkinPatternColor(1, 0, 1); UnlockSkinPatternColor(1, 0, 2); UnlockSkinPatternColor(1, 0, 3); UnlockSkinPatternColor(1, 0, 4); UnlockSkinPatternColor(1, 0, 5); UnlockSkinPatternColor(1, 0, 6);
                        break;
                }
            }
        }

        private void UnlockSkinPatternColor(int skinID, int patternID, int colorID)
        {
            GetSkin(skinID).GetPattern(patternID).GetColor(colorID);
        }

        private PlayerSkinData GetSkin(int index)
        {
            while (Skins.Count <= index)
            {
                Skins.Add(new PlayerSkinData() { Unlocked = true });
            }
            
            return Skins[index];
        }

        public void UnlockVFX(CharacterType characterType)
        {
            if (EvosStoreConfiguration.AreVfxFree())
            {
                switch (characterType)
                {
                    case CharacterType.BattleMonk:
                        UnlockVfx(200, 0);
                        break;
                    case CharacterType.BazookaGirl:
                        UnlockVfx(300, 0); UnlockVfx(300, 2);
                        break;
                    case CharacterType.DigitalSorceress:
                        UnlockVfx(400, 0); UnlockVfx(400, 1); UnlockVfx(400, 2); UnlockVfx(400, 3); UnlockVfx(400, 4);
                        UnlockVfx(401, 0);
                        break;
                    case CharacterType.Gremlins:
                        UnlockVfx(500, 0);
                        break;
                    case CharacterType.NanoSmith:
                        UnlockVfx(600, 0); UnlockVfx(600, 3);
                        break;
                    case CharacterType.RageBeast:
                        UnlockVfx(700, 0);
                        break;
                    case CharacterType.RobotAnimal:
                        UnlockVfx(800, 0); UnlockVfx(800, 4);
                        break;
                    case CharacterType.Scoundrel:
                        UnlockVfx(900, 0); UnlockVfx(900, 1);
                        break;
                    case CharacterType.Sniper:
                        UnlockVfx(1000, 0); UnlockVfx(1000, 4);
                        break;
                    case CharacterType.SpaceMarine:
                        UnlockVfx(1100, 0);
                        break;
                    case CharacterType.Spark:
                        UnlockVfx(1200, 0);
                        break;
                    case CharacterType.TeleportingNinja:
                        UnlockVfx(1300, 0);
                        break;
                    case CharacterType.Thief:
                        UnlockVfx(1400, 0);
                        break;
                    case CharacterType.Tracker:
                        UnlockVfx(1500, 0); UnlockVfx(1500, 1);
                        break;
                    case CharacterType.Trickster:
                        UnlockVfx(1600, 0); UnlockVfx(1600, 1);
                        break;
                    case CharacterType.Rampart:
                        UnlockVfx(1800, 0); UnlockVfx(1801, 3);
                        break;
                    case CharacterType.Claymore:
                        UnlockVfx(1900, 0);
                        break;
                    case CharacterType.Blaster:
                        UnlockVfx(2000, 0);
                        break;
                    case CharacterType.FishMan:
                        UnlockVfx(2100, 0);
                        break;
                    case CharacterType.Exo:
                        UnlockVfx(2200, 0);
                        break;
                    case CharacterType.Soldier:
                        UnlockVfx(2300, 0); UnlockVfx(2301, 4);
                        break;
                    case CharacterType.Martyr:
                        UnlockVfx(2400, 0);
                        break;
                    case CharacterType.Sensei:
                        UnlockVfx(2500, 0);
                        break;
                    case CharacterType.Manta:
                        UnlockVfx(2700, 0);
                        break;
                    case CharacterType.Valkyrie:
                        UnlockVfx(2800, 0);
                        break;
                    case CharacterType.Archer:
                        UnlockVfx(2900, 0); UnlockVfx(2901, 0);
                        break;
                    case CharacterType.Samurai:
                        UnlockVfx(3200, 0);
                        break;
                    case CharacterType.Cleric:
                        UnlockVfx(3400, 0);
                        break;
                    case CharacterType.Neko:
                        UnlockVfx(3500, 0);
                        break;
                    case CharacterType.Scamp:
                        UnlockVfx(3600, 0);
                        break;
                    case CharacterType.Dino:
                        UnlockVfx(3500, 0);
                        break;
                    case CharacterType.Iceborg:
                        UnlockVfx(3900, 0);
                        break;
                    case CharacterType.Fireborg:
                        UnlockVfx(4000, 0);
                        break;
                }
            }
        }

        private void UnlockVfx(int VfxId, int AbilityId)
        {
            AbilityVfxSwaps.Add(new PlayerAbilityVfxSwapData()
            {
                AbilityId = AbilityId,
                AbilityVfxSwapID = VfxId
            });
        }

        public bool Unlocked { get; set; }

        public CharacterVisualInfo LastSkin { get; set; }

        public CharacterCardInfo LastCards { get; set; }

        public CharacterModInfo LastMods { get; set; }

        public CharacterModInfo LastRankedMods { get; set; }

        public CharacterAbilityVfxSwapInfo LastAbilityVfxSwaps { get; set; }

        public List<CharacterLoadout> CharacterLoadouts { get; set; }

        public List<CharacterLoadout> CharacterLoadoutsRanked { get; set; }

        public int LastSelectedLoadout { get; set; }

        public int LastSelectedRankedLoadout { get; set; }

        public int NumCharacterLoadouts { get; set; }

        [EvosMessage(531)]
        public List<PlayerSkinData> Skins { get; set; }

        [EvosMessage(528)]
        public List<PlayerTauntData> Taunts { get; set; }

        [EvosMessage(77)]
        public List<PlayerModData> Mods { get; set; }

        [EvosMessage(548)]
        public List<PlayerAbilityVfxSwapData> AbilityVfxSwaps { get; set; }

        public int ResetSelectionVersion { get; set; }

        public object Clone()
        {
            return base.MemberwiseClone();
        }
    }
}
