using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Quest;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocolBase : WebSocketBehaviorBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocolBase));
        private Dictionary<Type, EvosMessageDelegate<WebSocketMessage>> messageHandlers = new Dictionary<Type, EvosMessageDelegate<WebSocketMessage>>();
        public long AccountId;
        public long SessionToken;
        public string UserName;
        
        public GameType SelectedGameType;
        public ushort SelectedSubTypeMask;
        public BotDifficulty AllyDifficulty;
        public BotDifficulty EnemyDifficulty;

        protected override string GetConnContext()
        {
            return "C " + AccountId;
        }

        protected override void HandleMessage(MessageEventArgs e)
        {
            MemoryStream stream = new MemoryStream(e.RawData);
            WebSocketMessage deserialized = null;

            try
            {
                deserialized = (WebSocketMessage)EvosSerializer.Instance.Deserialize(stream);
            }
            catch (NullReferenceException nullEx)
            {
                log.Error("No message handler registered for data: " + BitConverter.ToString(e.RawData));
            }

            if (deserialized != null)
            {
                EvosMessageDelegate<WebSocketMessage> handler = GetHandler(deserialized.GetType());
                if (handler != null)
                {
                    log.Debug($"< {deserialized.GetType().Name} {DefaultJsonSerializer.Serialize(deserialized)}");
                    handler.Invoke(deserialized);
                }
                else
                {
                    log.Error("No handler for " + deserialized.GetType().Name + "\n" + DefaultJsonSerializer.Serialize(deserialized));
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
                log.Error("No handler found for type " + type.Name);
                return null;
            }
        }

        public void Send(WebSocketMessage message)
        {
            Wrap(SendImpl, message);
        }

        public void Broadcast(WebSocketMessage message)
        {
            Wrap(BroadcastImpl, message);
        }

        private void SendImpl(WebSocketMessage message)
        {
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Send(stream.ToArray());
            log.Debug($"> {message.GetType().Name} {DefaultJsonSerializer.Serialize(message)}");
        }

        private void BroadcastImpl(WebSocketMessage message)
        {
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Sessions.Broadcast(stream.ToArray());
            log.Debug($">> {message.GetType().Name} {DefaultJsonSerializer.Serialize(message)}");
        }


        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, string message)
        {
            response.Success = false;
            response.ErrorMessage = message;
            response.ResponseId = requestId;
            log.Error(message);
            Send(response);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, Exception error)
        {
            response.Success = false;
            response.ErrorMessage = error.Message;
            response.ResponseId = requestId;
            log.Error(error.Message);
            Console.WriteLine(error);
            Send(response);
        }

        public void SendLobbyServerReadyNotification()
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
            LobbyServerReadyNotification notification = new LobbyServerReadyNotification
            {
                AccountData = account.CloneForClient(),
                AlertMissionData = new LobbyAlertMissionDataNotification(),
                CharacterDataList = account.CharacterData.Values.ToList(),
                CommerceURL = "http://127.0.0.1/AtlasCommerce",
                EnvironmentType = EnvironmentType.External,
                FactionCompetitionStatus = new FactionCompetitionNotification(),
                FriendStatus = FriendManager.GetFriendStatusNotification(AccountId),
                GroupInfo = GroupManager.GetGroupInfo(AccountId),
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
                FreeRotationAdditions = new Dictionary<CharacterType, RequirementCollection>(),
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
            string PatchNotesText = ConfigManager.PatchNotesText;

            try
            {
                using WebClient wc = new WebClient();
                wc.Headers.Set("User-Agent", "AtlasReactor");
                string json = wc.DownloadString(EvosConfiguration.GetGitHubCommits());
                JArray array = JArray.Parse(json);
                StringBuilder parsed = new StringBuilder();
                foreach (JObject obj in array)
                {
                    string sha = obj["sha"].ToString();
                    string author = obj["commit"]["author"]["name"].ToString();
                    string message = obj["commit"]["message"].ToString();
                    List<string> parts = message.Split('\n').ToList();
                    string title = parts[0];
                    parts.RemoveAt(0);
                    message = String.Join('\n', parts);
                    parsed.AppendLine($"<size=20>[{sha.Substring(0, 7)}] <color=#ff66ff>{author}</color></size>");
                    parsed.AppendLine($"<size=30><b>{title}</b></size>");
                    parsed.AppendLine($"{message}\n\n\n");
                }
                PatchNotesText = parsed.ToString();
            }
            catch (Exception e)
            {
                log.Info($"Could not get github commits {e.Message}");
            }

            return new ServerMessageOverrides
            {
                MOTDPopUpText = ConfigManager.MOTDPopUpText, // Popup message when client connects to lobby
                MOTDText = ConfigManager.MOTDText, // "alert" text
                ReleaseNotesHeader = ConfigManager.PatchNotesHeader,
                ReleaseNotesDescription = ConfigManager.PatchNotesDescription,
                ReleaseNotesText = PatchNotesText, // ConfigManager.PatchNotesText,
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
                CharacterAbilityConfigOverrides = CharacterManager.GetChacterAbilityConfigOverrides(),
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
            AllyDifficulty = difficulty;
        }
        protected void SetEnemyDifficulty(BotDifficulty difficulty)
        {
            EnemyDifficulty = difficulty;
        }
        protected void SetLastSelectedLoadout(int lastSelectedLoadout)
        {
            log.Debug("last selected loadout changed to " + lastSelectedLoadout);
        }

    }
}
