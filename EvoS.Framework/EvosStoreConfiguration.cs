using System.IO;

namespace EvoS.Framework
{
    public class EvosStoreConfiguration
    {
        private static EvosStoreConfiguration Instance = null;
        public bool vfxForFree = true;
        public bool BannersForFree = true;
        public bool tauntsForFree = true;
        public bool abilityModsForFree = true;
        public bool skinsForFree = true;
        public bool emojisForFree = true;
        public bool titlesForFree = true;
        public bool loadingScreenBackgroundForFree = true;
        public bool overconsForFree = true;
        public bool allCharactersForFree = true;
        public int charactersLevels = 20;

        private static EvosStoreConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .Build();

                Instance = deserializer.Deserialize<EvosStoreConfiguration>(File.ReadAllText("storeSettings.yaml"));
            }

            return Instance;
        }

        public static bool IsVfxFree()
        {
            return GetInstance().vfxForFree;
        }

        public static bool IsBannersFree()
        {
            return GetInstance().BannersForFree;
        }

        public static bool IstauntsFree()
        {
            return GetInstance().tauntsForFree;
        }

        public static bool IsAbilitysFree()
        {
            return GetInstance().abilityModsForFree;
        }

        public static bool IsSkinsFree()
        {
            return GetInstance().skinsForFree;
        }

        public static bool IsEmojisFree()
        {
            return GetInstance().emojisForFree;
        }

        public static bool IsTitlesFree()
        {
            return GetInstance().titlesForFree;
        }

        public static bool IsLoadingScreenBackgroundFree()
        {
            return GetInstance().loadingScreenBackgroundForFree;
        }

        public static bool IsOverconsFree()
        {
            return GetInstance().overconsForFree;
        }

        public static bool IsAllCharactersForFree()
        {
            return GetInstance().allCharactersForFree;
        }

        public static int GetCharactersLevels()
        {
            return GetInstance().charactersLevels;
        }
    }
}
