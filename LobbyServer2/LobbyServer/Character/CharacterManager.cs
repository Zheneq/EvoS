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
    }
}
