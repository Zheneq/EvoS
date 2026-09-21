using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Gamemode;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Quest;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.TrustWar;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Exceptions;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;
using Newtonsoft.Json.Linq;
using static EvoS.Framework.DataAccess.Daos.MiscDao;
using static EvoS.Framework.Misc.GameUtils;

namespace CentralServer.LobbyServer.Login;

public class LoginModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LoginModule));
    private static readonly Lazy<string> CachedPatchNotes = new(FetchGithubPatchNotes);
    private readonly IClientConnection _conn;

    public LoginModule(IClientConnection conn) { _conn = conn; }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<RegisterGameClientRequest>(HandleRegisterGame);
    }

    private void HandleRegisterGame(RegisterGameClientRequest request)
    {
        if (request == null)
        {
            SendError(new RegisterGameClientResponse(), 0, Messages.LoginFailed);
            _conn.CloseConnection();
            return;
        }

        try
        {
            SessionManager.OnPlayerConnect(_conn, request);

            log.Info(string.Format(Messages.LoginSuccess, _conn.UserName));
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(request.SessionInfo.AccountId);
            RegisterGameClientResponse response = new RegisterGameClientResponse
            {
                AuthInfo = request.AuthInfo, // Send original, if some data is missing on a new instance the game fails
                SessionInfo = sessionInfo,
                ResponseId = request.RequestId
            };

            // Overwrite the values we need
            response.AuthInfo.Password = null;
            response.AuthInfo.AccountId = _conn.AccountId;
            response.AuthInfo.Handle = sessionInfo.Handle;
            response.AuthInfo.TicketData = new SessionTicketData
            {
                AccountID = _conn.AccountId,
                SessionToken = sessionInfo.SessionToken,
                ReconnectionSessionToken = sessionInfo.ReconnectSessionToken
            }.ToStringWithSignature();

            _conn.Send(response);
            SendLobbyServerReadyNotification();

            // Send 'Connected to lobby server' notification to chat
            foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
            {
                LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                if (player != null && !player.IsInGame())
                {
                    player.SendSystemMessage($"<link=name>{sessionInfo.Handle}</link> connected to lobby server");
                }
            }

            DB.Get().UserMetadataDao.UpsertLastSession(_conn.AccountId, _conn.ProxyName, sessionInfo.BuildVersionInfo);
        }
        catch (RegisterGameException e)
        {
            SendError(new RegisterGameClientResponse(), request.RequestId, error: e);
            _conn.CloseConnection();
            return;
        }
        catch (Exception e)
        {
            SendError(new RegisterGameClientResponse(), request.RequestId);
            log.Error("Exception while registering game client", e);
            _conn.CloseConnection();
            return;
        }
        _conn.BroadcastRefreshFriendList();
    }

    private void SendLobbyServerReadyNotification()
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);

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
            FriendStatus = FriendManager.GetFriendStatusNotification(_conn.AccountId),
            GroupInfo = GroupManager.GetGroupInfo(_conn.AccountId),
            SeasonChapterQuests = QuestManager.GetSeasonQuestDataNotification(),
            ServerQueueConfiguration = GetServerQueueConfigurationUpdateNotification(),
            Status = GetLobbyStatusNotification(account)
        };

        _conn.Send(notification);
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
            ClientAccessLevel = AccessUtils.GetClientAccessLevel(account),
            ErrorReportRate = new TimeSpan(0, 3, 0),
            GameplayOverrides = GameConfig.GetGameplayOverrides(),
            HasPurchasedGame = true,
            PacificNow = DateTime.UtcNow, // TODO ?
            UtcNow = DateTime.UtcNow,
            ServerLockState = ServerLockState.Unlocked,
            ServerMessageOverrides = GetServerMessageOverrides()
        };
    }

    private ServerMessageOverrides GetServerMessageOverrides()
    {
        string adminMessage = AdminMessageManager.PopAdminMessage(_conn.AccountId);
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

    private void SendError(WebSocketResponseMessage response, int requestId,
        string message = null, Exception error = null)
    {
        response.Success = false;
        response.ErrorMessage = message ?? error?.Message;
        response.ResponseId = requestId;
        if (message != null) log.Info($"Sending error response: {message}");
        else log.Info("Sending error response", error);
        _conn.Send(response);
    }
}
