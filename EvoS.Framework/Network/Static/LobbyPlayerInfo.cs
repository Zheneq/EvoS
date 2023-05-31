using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using Newtonsoft.Json;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(710)]
    public class LobbyPlayerInfo
    {
        public LobbyPlayerInfo Clone()
        {
            return (LobbyPlayerInfo)MemberwiseClone();
        }

        public bool ReplacedWithBots { get; set; }
        [JsonIgnore] public bool IsRemoteControlled => ControllingPlayerId != 0;
        [JsonIgnore] public bool IsSpectator => TeamId == Team.Spectator;
        [JsonIgnore] public CharacterType CharacterType => CharacterInfo?.CharacterType ?? CharacterType.None;
        [JsonIgnore] public bool IsReady => ReadyState == ReadyState.Ready || IsAIControlled || IsRemoteControlled;
        [JsonIgnore] public bool IsAIControlled => IsNPCBot || IsLoadTestBot;

        public string GetHandle()
        {
            // if (IsRemoteControlled)
            // {
            // 	return $"{StringUtil.TR_CharacterName(CharacterInfo.CharacterType.ToString())} ({Handle})";
            // }
            // if (IsNPCBot && !BotsMasqueradeAsHumans)
            // {
            // 	return StringUtil.TR_CharacterName(CharacterInfo.CharacterType.ToString());
            // }
            return Handle;
        }

        public static LobbyPlayerInfo FromServer(
            LobbyServerPlayerInfo serverInfo, 
            int maxPlayerLevel,
            MatchmakingQueueConfig queueConfig)
        {
            if (serverInfo == null)
            {
                return null;
            }

            List<LobbyCharacterInfo> list = null;
            if (serverInfo.RemoteCharacterInfos != null)
            {
                list = new List<LobbyCharacterInfo>();
                foreach (LobbyCharacterInfo lobbyCharacterInfo in serverInfo.RemoteCharacterInfos)
                {
                    list.Add(lobbyCharacterInfo.Clone());
                }
            }

            return new LobbyPlayerInfo
            {
                AccountId = serverInfo.AccountId,
                PlayerId = serverInfo.PlayerId,
                CustomGameVisualSlot = serverInfo.CustomGameVisualSlot,
                Handle = serverInfo.Handle,
                TitleID = serverInfo.TitleID,
                TitleLevel = serverInfo.TitleLevel,
                BannerID = serverInfo.BannerID,
                EmblemID = serverInfo.EmblemID,
                RibbonID = serverInfo.RibbonID,
                IsGameOwner = serverInfo.IsGameOwner,
                ReplacedWithBots = serverInfo.ReplacedWithBots,
                IsNPCBot = serverInfo.IsNPCBot,
                IsLoadTestBot = serverInfo.IsLoadTestBot,
                BotsMasqueradeAsHumans = queueConfig != null && queueConfig.BotsMasqueradeAsHumans,
                Difficulty = serverInfo.Difficulty,
                BotCanTaunt = serverInfo.BotCanTaunt,
                TeamId = serverInfo.TeamId,
                CharacterInfo = serverInfo.CharacterInfo?.Clone(),
                RemoteCharacterInfos = list,
                ReadyState = serverInfo.ReadyState,
                ControllingPlayerId = serverInfo.IsRemoteControlled ? serverInfo.ControllingPlayerInfo.PlayerId : 0,
                EffectiveClientAccessLevel = serverInfo.EffectiveClientAccessLevel,
                DisplayedStat = serverInfo.AccountLevel >= maxPlayerLevel
                    ? LocalizationPayload.Create("TotalSeasonLevelStatNumber", "Global",
                        LocalizationArg_Int32.Create(serverInfo.TotalLevel))
                    : LocalizationPayload.Create("LevelStatNumber", "Global",
                        LocalizationArg_Int32.Create(serverInfo.AccountLevel))
            };
        }

        public long AccountId;
        public int PlayerId;
        public int CustomGameVisualSlot;
        public string Handle;
        public int TitleID;
        public int TitleLevel;
        public int BannerID;
        public int EmblemID;
        public int RibbonID;
        public LocalizationPayload DisplayedStat;
        public bool IsGameOwner;
        public bool IsLoadTestBot;
        public bool IsNPCBot;
        public bool BotsMasqueradeAsHumans;
        public BotDifficulty Difficulty;
        public bool BotCanTaunt;
        public Team TeamId;
        public LobbyCharacterInfo CharacterInfo = new LobbyCharacterInfo();
        [EvosMessage(711)]
        public List<LobbyCharacterInfo> RemoteCharacterInfos = new List<LobbyCharacterInfo>();
        public ReadyState ReadyState;
        public int ControllingPlayerId;
        public ClientAccessLevel EffectiveClientAccessLevel;
    }
}