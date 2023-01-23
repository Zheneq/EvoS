using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(710)]
    public class LobbyPlayerInfo
    {
        public LobbyPlayerInfo Clone()
        {
            return (LobbyPlayerInfo) base.MemberwiseClone();
        }

        public bool ReplacedWithBots { get; set; }

        public static LobbyPlayerInfo FromServer(LobbyServerPlayerInfo serverInfo,
            MatchmakingQueueConfig queueConfig)
        {
            LobbyPlayerInfo lobbyPlayerInfo = null;
            if (serverInfo != null)
            {
                List<LobbyCharacterInfo> list = null;
                if (serverInfo.RemoteCharacterInfos != null)
                {
                    list = new List<LobbyCharacterInfo>();
                    foreach (LobbyCharacterInfo lobbyCharacterInfo in serverInfo.RemoteCharacterInfos)
                    {
                        list.Add(lobbyCharacterInfo.Clone());
                    }
                }

                lobbyPlayerInfo = new LobbyPlayerInfo
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
                    BotsMasqueradeAsHumans = (queueConfig != null && queueConfig.BotsMasqueradeAsHumans),
                    Difficulty = serverInfo.Difficulty,
                    BotCanTaunt = serverInfo.BotCanTaunt,
                    TeamId = serverInfo.TeamId,
                    CharacterInfo = ((serverInfo.CharacterInfo == null) ? null : serverInfo.CharacterInfo.Clone()),
                    RemoteCharacterInfos = list,
                    ReadyState = serverInfo.ReadyState,
                    ControllingPlayerId =
                        ((!serverInfo.IsRemoteControlled) ? 0 : serverInfo.ControllingPlayerInfo.PlayerId),
                    EffectiveClientAccessLevel = serverInfo.EffectiveClientAccessLevel
                };
            }

            return lobbyPlayerInfo;
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
