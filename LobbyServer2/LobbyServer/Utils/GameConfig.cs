using CentralServer.LobbyServer.Character;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Utils;

public class GameConfig
{
    public static LobbyGameplayOverrides GetGameplayOverrides()
    {
        return new LobbyGameplayOverrides
        {
            AllowReconnectingToGameInstantly = true,
            AllowSpectators = false,
            AllowSpectatorsOutsideCustom = false,
            CharacterConfigs = CharacterConfigs.Characters,
            CharacterAbilityConfigOverrides = LobbyCharacterInfo.GetChacterAbilityConfigOverrides(),
            //CharacterSkinConfigOverrides = null TODO: maybe can be used to unlock all skins
            EnableAllMods = true,
            EnableAllAbilityVfxSwaps = true,
            EnableCards = true,
            EnableClientPerformanceCollecting = false,
            EnableDiscord = false,
            EnableDiscordSdk = false,
            EnableEventBonus = false,
            EnableFacebook = false,
            EnableHiddenCharacters = false,
            EnableMods = true,
            EnableSeasons = true,
            EnableShop = true,
            EnableQuests = false,
            EnableSteamAchievements = false,
            EnableTaunts = true,
            CardConfigOverrides =
            {
                { CardType.Cleanse_Prep, new CardConfigOverride { CardType = CardType.Cleanse_Prep, Allowed = false } },
                { CardType.TurtleTech, new CardConfigOverride { CardType = CardType.TurtleTech, Allowed = false } },
                { CardType.SecondWind, new CardConfigOverride { CardType = CardType.SecondWind, Allowed = false } },
                { CardType.ReduceCooldown, new CardConfigOverride { CardType = CardType.ReduceCooldown, Allowed = false } },
            }
        };
    }
}