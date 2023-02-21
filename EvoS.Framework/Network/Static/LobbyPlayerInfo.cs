using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;

namespace EvoS.Framework.Network.Static
{
    [Serializable]
    [EvosMessage(710)]
    public class LobbyPlayerInfo
    {
        public LobbyPlayerInfo Clone()
        {
            return (LobbyPlayerInfo)base.MemberwiseClone();
        }

        public bool ReplacedWithBots { get; set; }

        public static LobbyPlayerInfo FromServer(LobbyServerPlayerInfo serverInfo, int maxPlayerLevel,
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

                // Refetch data from DB was not updated otherwise
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(serverInfo.AccountId);

                lobbyPlayerInfo = new LobbyPlayerInfo
                {
                    AccountId = account.AccountId,
                    PlayerId = serverInfo.PlayerId,
                    CustomGameVisualSlot = serverInfo.CustomGameVisualSlot,
                    Handle = account.Handle,
                    TitleID = account.AccountComponent.SelectedTitleID,
                    TitleLevel = serverInfo.TitleLevel,
                    BannerID = account.AccountComponent.SelectedBackgroundBannerID,
                    EmblemID = account.AccountComponent.SelectedForegroundBannerID,
                    RibbonID = serverInfo.RibbonID,
                    IsGameOwner = serverInfo.IsGameOwner,
                    ReplacedWithBots = serverInfo.ReplacedWithBots,
                    IsNPCBot = serverInfo.IsNPCBot,
                    IsLoadTestBot = serverInfo.IsLoadTestBot,
                    BotsMasqueradeAsHumans = (queueConfig != null && queueConfig.BotsMasqueradeAsHumans),
                    Difficulty = serverInfo.Difficulty,
                    BotCanTaunt = serverInfo.BotCanTaunt,
                    TeamId = serverInfo.TeamId,
                    CharacterInfo = ((serverInfo.CharacterInfo == null) ? null : LobbyCharacterInfo.Of(account.CharacterData[account.AccountComponent.LastCharacter])),
                    RemoteCharacterInfos = list,
                    ReadyState = serverInfo.ReadyState,
                    ControllingPlayerId =
                        ((!serverInfo.IsRemoteControlled) ? 0 : serverInfo.ControllingPlayerInfo.PlayerId),
                    EffectiveClientAccessLevel = serverInfo.EffectiveClientAccessLevel
                };
                if (serverInfo.AccountLevel >= maxPlayerLevel)
                {
                    //                    lobbyPlayerInfo.DisplayedStat = LocalizationPayload.Create("TotalSeasonLevelStatNumber", "Global",
                    //                        new LocalizationArg[]
                    //                        {
                    //                            LocalizationArg_Int32.Create(serverInfo.TotalLevel)
                    //                        });
                }
                else
                {
                    //                    lobbyPlayerInfo.DisplayedStat = LocalizationPayload.Create("LevelStatNumber", "Global",
                    //                        new LocalizationArg[]
                    //                        {
                    //                            LocalizationArg_Int32.Create(serverInfo.AccountLevel)
                    //                        });
                }
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