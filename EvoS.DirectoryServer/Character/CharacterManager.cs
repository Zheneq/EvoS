using System.Collections.Generic;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;

namespace EvoS.DirectoryServer.Character
{
    public class CharacterManager
    {
        public static Dictionary<CharacterType, PersistedCharacterData> GetPersistedCharacterData(long accountId)
        {
            Dictionary<CharacterType, PersistedCharacterData> characterDatas = new Dictionary<CharacterType, PersistedCharacterData>();

            foreach (CharacterConfig characterConfig in CharacterConfigs.Characters.Values)
            {
                PersistedCharacterData persistedCharacterData = new PersistedCharacterData(characterConfig.CharacterType)
                {
                    CharacterComponent = GetCharacterComponent(accountId, characterConfig.CharacterType),
                    CharacterType = characterConfig.CharacterType,
                    ExperienceComponent = new ExperienceComponent { Level = 1 }
                };
                characterDatas.Add(characterConfig.CharacterType, persistedCharacterData);
            }

            return characterDatas;
        }

        public static CharacterComponent GetCharacterComponent(long accountId, CharacterType characterType)
        {
            CharacterComponent characterComponent = new CharacterComponent()
            {
                Unlocked = true,
                LastSelectedLoadout = 0,
                Skins = GetPlayerSkinDataList(characterType),
                CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        new CharacterModInfo() { ModForAbility0 = 0, ModForAbility1 = 0, ModForAbility2 = 0, ModForAbility3 = 0, ModForAbility4 = 0 },
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                }
            };

            return characterComponent;
        }

        public static List<PersistedCharacterData> GetCharacterDataList(long accountId)
        {
            List<PersistedCharacterData> characterDataList = new List<PersistedCharacterData>();

            foreach (PersistedCharacterData persistedCharacterData in GetPersistedCharacterData(accountId).Values)
            {
                characterDataList.Add(persistedCharacterData);
            }

            return characterDataList;
        }

        public static LobbyCharacterInfo GetCharacterInfo(long accountId, CharacterType characterType)
        {
            CharacterComponent characterComponent = GetCharacterComponent(accountId, characterType);
            LobbyCharacterInfo characterInfo = new LobbyCharacterInfo()
            {
                CharacterAbilityVfxSwaps = characterComponent.LastAbilityVfxSwaps,
                CharacterCards = characterComponent.LastCards,
                CharacterLevel = 1,
                CharacterLoadouts = characterComponent.CharacterLoadouts,
                CharacterMatches = 0,
                CharacterMods = LobbyCharacterInfo.RemoveDisabledMods(characterComponent.LastMods, characterType),
                CharacterSkin = characterComponent.LastSkin,
                CharacterTaunts = characterComponent.Taunts,
                CharacterType = characterType,
            };
            return characterInfo;
        }

        private static List<PlayerSkinData> GetPlayerSkinDataList(CharacterType characterType)
        {
            List<PlayerSkinData> skins = new List<PlayerSkinData>();
            SkinHelper hlp = new SkinHelper();

            if (hlp.HasSkins(characterType))
            {
                Dictionary<int, Dictionary<int, List<int>>> skindata = hlp.GetSkins(characterType);
                
                for (int skinId = 0; skinId < skindata.Count; skinId++)
                {
                    PlayerSkinData playerSkinData = new PlayerSkinData();
                    playerSkinData.Unlocked = true;
                    playerSkinData.Patterns = new List<PlayerPatternData>();

                    Dictionary<int, List<int>> patterndata = null;
                    if (!skindata.TryGetValue(skinId, out patterndata))
                    {
                        playerSkinData.Patterns.Add(new PlayerPatternData { Unlocked = true, Colors = new List<PlayerColorData> { new PlayerColorData { Unlocked = true } } });
                        continue;
                    }
                    for (int patternId = 0; patternId < patterndata.Count; patternId++)
                    {
                        PlayerPatternData playerPatternData = new PlayerPatternData();
                        playerPatternData.Unlocked = true;
                        playerPatternData.Colors = new List<PlayerColorData>();

                        List<int> colordata = null;
                        if (!patterndata.TryGetValue(patternId, out colordata))
                        {
                            playerPatternData.Colors.Add(new PlayerColorData { Unlocked = true });
                            continue;
                        }

                        foreach(int colorID in colordata)
                        {
                            PlayerColorData playerColorData = new PlayerColorData();
                            playerColorData.Unlocked = true;
                            playerPatternData.Colors.Add(playerColorData);
                        }

                        playerSkinData.Patterns.Add(playerPatternData);
                    }
                    skins.Add(playerSkinData);
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    PlayerSkinData item = new PlayerSkinData()
                    {
                        Unlocked = true,
                        Patterns = new List<PlayerPatternData>
                    {
                        new PlayerPatternData
                        {
                            Colors = new List<PlayerColorData>
                            {
                                new PlayerColorData
                                {
                                    Unlocked = true
                                }
                            },
                            Unlocked = true
                        }
                    }
                    };
                    skins.Add(item);
                }
            }

            
            return skins;
        }
    }
}
