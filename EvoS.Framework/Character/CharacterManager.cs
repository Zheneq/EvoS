using System;
using System.Collections.Generic;
using System.IO;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using log4net;
using Newtonsoft.Json;

namespace EvoS.DirectoryServer.Character
{
    public class CharacterManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CharacterManager));
        
        private static readonly Lazy<Dictionary<CharacterType, CharacterModInfo>> DEFAULT_EQUIP = new Lazy<Dictionary<CharacterType, CharacterModInfo>>(() =>
            JsonConvert.DeserializeObject<Dictionary<CharacterType, CharacterModInfo>>(File.ReadAllText("Config/defaultEquip.json")));
        
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

            // Client looks up character data for CharacterType.None, e.g. the ranked draft loadout panel
            // gets loadout updates before it is set up for a character
            characterDatas.Add(CharacterType.None, new PersistedCharacterData(CharacterType.None));

            return characterDatas;
        }

        public static CharacterComponent GetCharacterComponent(long accountId, CharacterType characterType)
        {
            CharacterModInfo mods = GetDefaultMods(characterType);
            CharacterComponent characterComponent = new CharacterComponent()
            {
                Unlocked = true,
                LastSelectedLoadout = 0,
                Skins = GetPlayerSkinDataList(characterType),
                LastMods = mods,
                LastRankedMods = mods,
                CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        mods,
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                }
            };

            return characterComponent;
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

        public static CharacterModInfo GetDefaultMods(CharacterType characterType)
        {
            try
            {
                if (DEFAULT_EQUIP.Value.TryGetValue(characterType, out CharacterModInfo info))
                {
                    return info;
                }
                else
                {
                    log.Error($"Failed to find default loadout for {characterType}");
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to find default loadout for {characterType}", e);
            }
            return new CharacterModInfo();
        }
    }
}
