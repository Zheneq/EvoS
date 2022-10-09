using CentralServer.LobbyServer.Account;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Quest;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CentralServer.BridgeServer;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocolBase : WebSocketBehavior
    {
        private Dictionary<Type, EvosMessageDelegate<WebSocketMessage>> messageHandlers = new Dictionary<Type, EvosMessageDelegate<WebSocketMessage>>();
        public long AccountId;
        public long SessionToken;
        public string UserName;
        
        public GameType SelectedGameType;
        public ushort SelectedSubTypeMask;

        public BridgeServerProtocol CurrentServer { get; set; }

        protected override void OnMessage(MessageEventArgs e)
        {
            MemoryStream stream = new MemoryStream(e.RawData);
            WebSocketMessage deserialized = null;

            try
            {
                deserialized = (WebSocketMessage)EvosSerializer.Instance.Deserialize(stream);
            }
            catch (NullReferenceException nullEx)
            {
                Log.Print(LogType.Error, "No message handler registered for data:\n" + BitConverter.ToString(e.RawData));
            }

            if (deserialized != null)
            {
                EvosMessageDelegate<WebSocketMessage> handler = GetHandler(deserialized.GetType());
                if (handler != null)
                {
                    #if DEBUG
                    Log.Print(LogType.Network, "Received " + deserialized.GetType().Name);
                    #endif
                    handler.Invoke(deserialized);
                }
                else
                {
                    Log.Print(LogType.Error, "No handler for " + deserialized.GetType().Name + "\n" + Newtonsoft.Json.JsonConvert.SerializeObject(deserialized, Newtonsoft.Json.Formatting.Indented));
                }
            }
        }

        public void RegisterHandler<T>(EvosMessageDelegate<T> handler) where T : WebSocketMessage
        {
            messageHandlers.Add(typeof(T), msg => { handler((T)msg); });
        }

        private EvosMessageDelegate<WebSocketMessage> GetHandler(Type type)
        {
            try
            {
                return messageHandlers[type];
            }
            catch (KeyNotFoundException e)
            {
                Log.Print(LogType.Lobby, "No handler found for type " + type.Name);
                return null;
            }
        }

        public void Send(WebSocketMessage message)
        {
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            this.Send(stream.ToArray());
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, string message)
        {
            response.Success = false;
            response.ErrorMessage = message;
            response.ResponseId = requestId;
            Log.Print(LogType.Error, message);
            Send(response);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, Exception error)
        {
            response.Success = false;
            response.ErrorMessage = error.Message;
            response.ResponseId = requestId;
            Log.Print(LogType.Error, error.Message);
            Console.WriteLine(error);
            Send(response);
        }

        public void SendLobbyServerReadyNotification()
        {
            LobbyServerReadyNotification notification = new LobbyServerReadyNotification()
            {
                AccountData = AccountManager.GetPersistedAccountData(this.AccountId),
                AlertMissionData = new LobbyAlertMissionDataNotification(),
                CharacterDataList = CharacterManager.GetCharacterDataList(this.AccountId),
                CommerceURL = "http://127.0.0.1/AtlasCommerce",
                EnvironmentType = EnvironmentType.External,
                FactionCompetitionStatus = new FactionCompetitionNotification(),
                FriendStatus = FriendManager.GetFriendStatusNotification(this.AccountId),
                GroupInfo = GroupManager.GetGroupInfo(this.AccountId),
                SeasonChapterQuests = QuestManager.GetSeasonQuestDataNotification(),
                ServerQueueConfiguration = GetServerQueueConfigurationUpdateNotification(),
                Status = GetLobbyStatusNotification()
            };

            Send(notification);
        }

        private ServerQueueConfigurationUpdateNotification GetServerQueueConfigurationUpdateNotification()
        {
            return new ServerQueueConfigurationUpdateNotification
            {
                FreeRotationAdditions = new Dictionary<CharacterType, EvoS.Framework.Network.Static.RequirementCollection>(),
                GameTypeAvailabilies = GameModeManager.GetGameTypeAvailabilities(),
                TierInstanceNames = new List<LocalizationPayload>(),
                AllowBadges = true,
                NewPlayerPvPQueueDuration = 0
            };
        }

        private LobbyStatusNotification GetLobbyStatusNotification()
        {
            return new LobbyStatusNotification
            {
                AllowRelogin = false,
                ClientAccessLevel = ClientAccessLevel.Full,
                ErrorReportRate = new TimeSpan(0, 3, 0),
                GameplayOverrides = GetGameplayOverrides(),
                HasPurchasedGame = true,
                PacificNow = DateTime.UtcNow, // TODO ?
                UtcNow = DateTime.UtcNow,
                ServerLockState = ServerLockState.Unlocked,
                ServerMessageOverrides = GetServerMessageOverrides()
            };
        }

        private ServerMessageOverrides GetServerMessageOverrides()
        {
            return new ServerMessageOverrides
            {
                MOTDPopUpText = Config.ConfigManager.MOTDPopUpText, // Popup message when client connects to lobby
                MOTDText = Config.ConfigManager.MOTDText, // "alert" text
                ReleaseNotesHeader = Config.ConfigManager.PatchNotesHeader,
                ReleaseNotesDescription = Config.ConfigManager.PatchNotesDescription,
                ReleaseNotesText = Config.ConfigManager.PatchNotesText,
            };
        }

        public LobbyGameplayOverrides GetGameplayOverrides()
        {
            return new LobbyGameplayOverrides
            {
                AllowReconnectingToGameInstantly = true,
                AllowSpectators = false,
                AllowSpectatorsOutsideCustom = false,
                CharacterConfigs = CharacterConfigs.Characters,
                //CharacterSkinConfigOverrides = null TODO: maybe can be used to unlock all skins
                //EnableAllMods = true,
                EnableAllAbilityVfxSwaps = true,
                EnableCards = true,
                EnableClientPerformanceCollecting = false,
                EnableDiscord = false,
                EnableDiscordSdk = false,
                EnableEventBonus = false,
                EnableFacebook = false,
                EnableHiddenCharacters = false,
                EnableMods = true,
                EnableSeasons = false,
                EnableShop = true,
                EnableQuests = false,
                EnableSteamAchievements = false,
                EnableTaunts = true
            };
        }

        

        protected void SetGameType(GameType gameType)
        {
            SelectedGameType = gameType;
        }

        protected void SetAllyDifficulty(BotDifficulty difficulty)
        {
            // TODO
        }
        protected void SetCharacterAbilityVfxSwaps(CharacterAbilityVfxSwapInfo characterAbilityVfxSwapInfo)
        {
            SessionManager.GetPlayerInfo(this.AccountId).CharacterInfo.CharacterAbilityVfxSwaps = characterAbilityVfxSwapInfo;
        }
        protected void SetCharacterCards(CharacterCardInfo characterCardInfo)
        {
            SessionManager.GetPlayerInfo(this.AccountId).CharacterInfo.CharacterCards = characterCardInfo;
        }
        protected void SetCharacterLoadoutChanges(CharacterLoadoutUpdate characterLoadoutUpdate)
        {
            // TODO
            Log.Print(LogType.Debug, "SetCharacterLoadoutChanges not implemented!");
        }
        protected void SetCharacterMods(CharacterModInfo characterModInfo)
        {
            SessionManager.GetPlayerInfo(this.AccountId).CharacterInfo.CharacterMods = characterModInfo;
        }
        protected void SetCharacterSkin(CharacterVisualInfo characterVisualInfo)
        {
            SessionManager.GetPlayerInfo(this.AccountId).CharacterInfo.CharacterSkin = characterVisualInfo;
            Log.Print(LogType.Debug, characterVisualInfo.ToString());
        }
        protected void SetCharacterType(CharacterType characterType)
        {
            SessionManager.GetPlayerInfo(this.AccountId).CharacterInfo = CharacterManager.GetCharacterInfo(this.AccountId, characterType);

        }
        protected void SetContextualReadyState(ContextualReadyState contextualReadyState)
        {
            Log.Print(LogType.Debug, "SetContextualReadyState");
            MatchmakingManager.StartPractice(this);
        }
        protected void SetEnemyDifficulty(BotDifficulty difficulty)
        {
            // TODO
        }
        protected void SetLastSelectedLoadout(int lastSelectedLoadout)
        {
            Log.Print(LogType.Debug, "last selected loadout changed to " + lastSelectedLoadout);
        }

    }
}
