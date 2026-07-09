using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Unity;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    public class LobbyServerPlayerInfo : LobbyPlayerCommonInfo
    {
        public int AccountLevel;
        public int TotalLevel;
        public int NumWins;
        public float AccMatchmakingElo;
        public int AccMatchmakingCount;
        public Dictionary<CharacterType, float> CharMatchmakingElo;
        public Dictionary<CharacterType, int> CharMatchmakingCount = new();
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

        public LobbyServerPlayerInfo Clone()
        {
            return (LobbyServerPlayerInfo)MemberwiseClone();
        }
        
        public static LobbyServerPlayerInfo Of(PersistedAccountData account, CharacterType characterType = CharacterType.None) {
            if (characterType == CharacterType.None)
            {
                characterType = account.AccountComponent.LastCharacter;
            }
            return new LobbyServerPlayerInfo
            {
                AccountId = account.AccountId,
                BannerID = account.AccountComponent.SelectedBackgroundBannerID == -1
                    ? 95
                    : account.AccountComponent.SelectedBackgroundBannerID, // patch for existing users: default is 95  TODO patch account itself
                BotCanTaunt = false,
                CharacterInfo = LobbyCharacterInfo.Of(account.CharacterData[characterType]),
                ControllingPlayerId = 0,
                EffectiveClientAccessLevel = AccessUtils.GetClientAccessLevel(account),
                EmblemID = account.AccountComponent.SelectedForegroundBannerID == -1
                    ? 65
                    : account.AccountComponent.SelectedForegroundBannerID, // patch for existing users: default is 65 
                Handle = account.Handle,
                IsGameOwner = false,
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

        // rogues
        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            AccountLevel = reader.ReadInt32();
            NumWins = reader.ReadInt32();
            AccMatchmakingCount = reader.ReadInt32();
            int num = reader.ReadInt32();
            CharMatchmakingCount = new Dictionary<CharacterType, int>(num);
            for (int i = 0; i < num; i++)
            {
                CharacterType key = (CharacterType)reader.ReadInt16();
                int value = reader.ReadInt32();
                CharMatchmakingCount[key] = value;
            }
            num = reader.ReadInt32();
            ProxyPlayerIds = new List<int>(num);
            for (int i = 0; i < num; i++)
            {
                ProxyPlayerIds.Add(reader.ReadInt32());
            }
            GroupIdAtStartOfMatch = reader.ReadInt64();
            GroupSizeAtStartOfMatch = reader.ReadInt32();
            GroupLeader = reader.ReadBoolean();
            EffectiveClientAccessLevel = (ClientAccessLevel)reader.ReadInt16();
        }

        // rogues
        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            writer.Write(AccountLevel);
            writer.Write(NumWins);
            writer.Write(AccMatchmakingCount);
            int count = CharMatchmakingCount.Count;
            writer.Write(count);
            foreach (KeyValuePair<CharacterType, int> keyValuePair in CharMatchmakingCount)
            {
                writer.Write((short)keyValuePair.Key);
                writer.Write(keyValuePair.Value);
            }
            count = ProxyPlayerIds.Count;
            writer.Write(count);
            foreach (int num in ProxyPlayerIds)
            {
                writer.Write(num);
            }
            writer.Write(GroupIdAtStartOfMatch);
            writer.Write(GroupSizeAtStartOfMatch);
            writer.Write(GroupLeader);
            writer.Write((short)EffectiveClientAccessLevel);
        }
    }
}
