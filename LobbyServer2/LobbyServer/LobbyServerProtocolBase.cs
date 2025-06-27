﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Quest;
using CentralServer.LobbyServer.TrustWar;
using CentralServer.LobbyServer.Utils;
using CentralServer.Proxy;
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
using static EvoS.Framework.DataAccess.Daos.MiscDao;

namespace CentralServer.LobbyServer
{
    public class LobbyServerProtocolBase : WebSocketBehaviorBase<WebSocketMessage>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerProtocolBase));
        public long AccountId;
        public string UserName;
        public long SessionToken;
        public GameType SelectedGameType;
        public ushort SelectedSubTypeMask;
        public BotDifficulty AllyDifficulty;
        public BotDifficulty EnemyDifficulty;
        public bool SessionCleaned = false; // tracks clean up methods execution for reconnection

        protected ProxyConfiguration.Proxy Proxy = null;
        
        private static readonly Lazy<string> CachedPatchNotes = new(FetchGithubPatchNotes);

        protected override string GetConnContext()
        {
            return "C " + AccountId;
        }

        private IPAddress GetIpAddress()
        {
            var headerName = EvosConfiguration.GetClientIpHeader();
            if (!headerName.IsNullOrEmpty())
            {
                var headerValue = Context.Headers[headerName];
                if (headerValue is null)
                {
                    log.Error($"Expected header {headerName} not present!");
                }
                else if (!IPAddress.TryParse(headerValue, out var ipAddress))
                {
                    log.Error($"Header {headerName} has incorrect value {headerValue}!");
                }
                else
                {
                    return ipAddress;
                }
            }

            return Context.UserEndPoint.Address;
        }

        protected override void HandleOpen()
        {
            IPAddress clientIpAddress = GetIpAddress();
            log.Info($"Connecting from {clientIpAddress}");
            Proxy = LobbyServerUtils.DetectProxy(clientIpAddress);
            if (Proxy != null)
            {
                log.Info($"Detected proxy {Proxy.GetName()}");
            }
        }

        protected override WebSocketMessage DeserializeMessage(byte[] data, out int callbackId)
        {
            callbackId = 0;
            return (WebSocketMessage)EvosSerializer.Instance.Deserialize(new MemoryStream(data));
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
            if (!IsConnected)
            {
                log.Warn($"Attempted to send {message.GetType()} to a disconnected socket");
                return;
            }

            if (Proxy is not null && ProxyPatcher.Mapping.TryGetValue(message.GetType(), out var patcher))
            {
                message = patcher(message, Proxy);
            }

            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Send(stream.ToArray());
            LogMessage(">", message);
        }

        private void BroadcastImpl(WebSocketMessage message)
        {
            MemoryStream stream = new MemoryStream();
            EvosSerializer.Instance.Serialize(stream, message);
            Sessions.Broadcast(stream.ToArray());
            LogMessage(">>", message);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, string message)
        {
            response.Success = false;
            response.ErrorMessage = message;
            response.ResponseId = requestId;
            log.Info($"Sending error response: {message}");
            Send(response);
        }

        public void SendErrorResponse(WebSocketResponseMessage response, int requestId, Exception error = null)
        {
            response.Success = false;
            response.ErrorMessage = error?.Message;
            response.ResponseId = requestId;
            log.Info("Sending error response", error);
            Send(response);
        }

        public void SendLobbyServerReadyNotification()
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);

            FactionCompetitionNotification factionCompetitionNotification = new();

            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                TrustWarEntry trustWar = TrustWarManager.getTrustWarEntry();
                factionCompetitionNotification = new FactionCompetitionNotification()
                {
                    ActiveIndex = 1,
                    Scores = new Dictionary<int, long>() {
                        { 0, trustWar.Points[0] },
                        { 1, trustWar.Points[1] },
                        { 2, trustWar.Points[2] }
                    }
                };
            }


            LobbyServerReadyNotification notification = new LobbyServerReadyNotification
            {
                AccountData = account.CloneForClient(),
                AlertMissionData = new LobbyAlertMissionDataNotification(),
                CharacterDataList = account.CharacterData.Values.ToList(),
                CommerceURL = "http://127.0.0.1/AtlasCommerce",
                EnvironmentType = EnvironmentType.External,
                FactionCompetitionStatus = factionCompetitionNotification,
                FriendStatus = FriendManager.GetFriendStatusNotification(AccountId),
                GroupInfo = GroupManager.GetGroupInfo(AccountId),
                SeasonChapterQuests = QuestManager.GetSeasonQuestDataNotification(),
                ServerQueueConfiguration = GetServerQueueConfigurationUpdateNotification(),
                Status = GetLobbyStatusNotification(account)
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

        private LobbyStatusNotification GetLobbyStatusNotification(PersistedAccountData account)
        {
            return new LobbyStatusNotification
            {
                AllowRelogin = false,
                ClientAccessLevel = account.AccountComponent.IsDev() ? ClientAccessLevel.Admin : ClientAccessLevel.Full,
                ErrorReportRate = new TimeSpan(0, 3, 0),
                GameplayOverrides = GameConfig.GetGameplayOverrides(),
                HasPurchasedGame = true,
                PacificNow = DateTime.UtcNow, // TODO ?
                UtcNow = DateTime.UtcNow,
                ServerLockState = ServerLockState.Unlocked,
                ServerMessageOverrides = GetServerMessageOverrides()
            };
        }

        private static string FetchGithubPatchNotes()
        {
            if (LobbyConfiguration.GetPatchNotesCommitsUrl().IsNullOrEmpty())
            {
                return null;
            }

            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Evos/1.0)");
                var request = new HttpRequestMessage(HttpMethod.Get, LobbyConfiguration.GetPatchNotesCommitsUrl());
                var response = httpClient.Send(request);
                using var reader = new StreamReader(response.Content.ReadAsStream());
                string json = reader.ReadToEnd();
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

                return parsed.ToString();
            }
            catch (Exception e)
            {
                log.Info($"Could not get github commits {e.Message}");
            }

            return null;
        }

        private ServerMessageOverrides GetServerMessageOverrides()
        {
            string adminMessage = AdminMessageManager.PopAdminMessage(AccountId);
            if (adminMessage is not null)
            {
                log.Info($"Sending admin message: {adminMessage}");
            }

            return new ServerMessageOverrides
            {
                MOTDPopUpText = adminMessage ?? GetMotdPopUpText(), // Popup message when client connects to lobby
                MOTDText = GetMotdText(), // "alert" text
                ReleaseNotesHeader = LobbyConfiguration.GetPatchNotesHeader(),
                ReleaseNotesDescription = LobbyConfiguration.GetPatchNotesDescription(),
                ReleaseNotesText = CachedPatchNotes.Value ?? LobbyConfiguration.GetPatchNotesText()
            };
        }

        private static ServerMessage GetMotdText()
        {
            if (DB.Get().MiscDao.GetEntry(EvosServerMessageType.MessageOfTheDay.ToString()) is ServerMessageEntry msg
                && !msg.Message.IsEmpty())
            {
                return msg.Message;
            }
            return LobbyConfiguration.GetMOTDText();
        }

        private static ServerMessage GetMotdPopUpText()
        {
            if (DB.Get().MiscDao.GetEntry(EvosServerMessageType.MessageOfTheDayPopup.ToString()) is ServerMessageEntry msg
                && !msg.Message.IsEmpty())
            {
                return msg.Message.FillMissingLocalizations(); // otherwise is just won't show if there is no loc for the active language
            }
            return LobbyConfiguration.GetMOTDPopUpText();
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

    }
}
