using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class LobbyServerPlayerInfo : LobbyPlayerCommonInfo
    {
        public LobbyServerPlayerInfo Clone()
        {
            return (LobbyServerPlayerInfo)base.MemberwiseClone();
        }

        public int AccountLevel;
        public int TotalLevel;
        public int NumWins;
        public float AccMatchmakingElo;
        public int AccMatchmakingCount;
        public Dictionary<CharacterType, float> CharMatchmakingElo;
        public Dictionary<CharacterType, int> CharMatchmakingCount;
        public float UsedMatchmakingElo;
        public int RankedTier;
        public float RankedPoints;
        public string MatchmakingEloKey;
        public List<int> ProxyPlayerIds = new List<int>();
        public long GroupIdAtStartOfMatch;
        public int GroupSizeAtStartOfMatch;
        public bool GroupLeader;
        public ClientAccessLevel EffectiveClientAccessLevel;
        public int RankedSortKarma;
        
        public static LobbyServerPlayerInfo Of(PersistedAccountData account) {
            return new LobbyServerPlayerInfo
            {
                AccountId = account.AccountId,
                BannerID = account.AccountComponent.SelectedBackgroundBannerID == -1
                    ? 95
                    : account.AccountComponent.SelectedBackgroundBannerID, // patch for existing users: default is 95  TODO patch account itself
                BotCanTaunt = false,
                CharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter]),
                ControllingPlayerId = 0,
                EffectiveClientAccessLevel = account.AccountComponent.AppliedEntitlements.ContainsKey("DEVELOPER_ACCESS")
                    ? ClientAccessLevel.Admin
                    : ClientAccessLevel.Full,
                EmblemID = account.AccountComponent.SelectedForegroundBannerID == -1
                    ? 65
                    : account.AccountComponent.SelectedForegroundBannerID, // patch for existing users: default is 65 
                Handle = account.Handle,
                IsGameOwner = true,
                IsLoadTestBot = false,
                IsNPCBot = false,
                PlayerId = 0,
                ReadyState = ReadyState.Unknown,
                ReplacedWithBots = false,
                RibbonID = account.AccountComponent.SelectedRibbonID,
                TitleID = account.AccountComponent.SelectedTitleID,
                TitleLevel = 1
            };
        }
    }

}
