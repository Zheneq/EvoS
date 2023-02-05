using System.IO;

namespace EvoS.Framework
{
    public class EvosStoreConfiguration
    {
        private static EvosStoreConfiguration Instance = null;
        public bool FreeVfx = true;
        public bool FreeBanners = true;
        public bool FreeTaunts = true;
        public bool FreeAbilitMods = true;
        public bool FreeSkins = true;
        public bool FreeEmojis = true;
        public bool FreeTitles = true;
        public bool FreeLoadingScreenBackground = true;
        public bool FreeOvercons = true;
        public bool FreeAllCharacters = true;
        public int StartingCharactersLevel = 20;

        private static EvosStoreConfiguration GetInstance()
        {
            if (Instance == null)
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .Build();

                Instance = deserializer.Deserialize<EvosStoreConfiguration>(File.ReadAllText("Config/storeSettings.yaml"));
            }

            return Instance;
        }

        public static bool AreVfxFree()
        {
            return GetInstance().FreeVfx;
        }

        public static bool AreBannersFree()
        {
            return GetInstance().FreeBanners;
        }

        public static bool AreTauntsFree()
        {
            return GetInstance().FreeTaunts;
        }

        public static bool AreAbilitysFree()
        {
            return GetInstance().FreeAbilitMods;
        }

        public static bool AreSkinsFree()
        {
            return GetInstance().FreeSkins;
        }

        public static bool AreEmojisFree()
        {
            return GetInstance().FreeEmojis;
        }

        public static bool AreTitlesFree()
        {
            return GetInstance().FreeTitles;
        }

        public static bool AreLoadingScreenBackgroundFree()
        {
            return GetInstance().FreeLoadingScreenBackground;
        }

        public static bool AreOverconsFree()
        {
            return GetInstance().FreeOvercons;
        }

        public static bool AreAllCharactersForFree()
        {
            return GetInstance().FreeAllCharacters;
        }

        public static int GetStartingCharactersLevel()
        {
            return GetInstance().StartingCharactersLevel;
        }
    }
}
