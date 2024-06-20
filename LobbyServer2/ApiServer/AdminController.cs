using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer.Account;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CentralServer.ApiServer
{
    public static class AdminController
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AdminController));

        public static event Action<long, PauseQueueModel> OnAdminPauseQueue = delegate { };
        public static event Action<long, PendingShutdownModel> OnAdminScheduleShutdown = delegate { };

        public class PauseQueueModel
        {
            public bool Paused { get; set; }
        }

        public static IResult PauseQueue([FromBody] PauseQueueModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }
            MatchmakingManager.Enabled = !data.Paused;
            CustomGameManager.Enabled = !data.Paused;
            OnAdminPauseQueue(adminAccountId, data);
            return Results.Ok();
        }

        public class PendingShutdownModel
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public CentralServer.PendingShutdownType Type { get; set; }
        }

        public static IResult ScheduleShutdown([FromBody] PendingShutdownModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }

            log.Info($"{adminHandle} updated pending shutdown state: {data.Type}");
            CentralServer.PendingShutdown = data.Type;
            OnAdminScheduleShutdown(adminAccountId, data);
            return Results.Ok();
        }

        public class BroadcastModel
        {
            public string Msg { get; set; }
        }

        public static IResult Broadcast([FromBody] BroadcastModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }
            log.Info($"Broadcast {adminHandle}: {data.Msg}");
            if (data.Msg.IsNullOrEmpty())
            {
                return Results.BadRequest();
            }
            SessionManager.Broadcast(data.Msg);
            return Results.Ok();
        }

        public class ServerMessageModel
        {
            public ServerMessage Msg { get; set; }
            public string Severity { get; set; }

            public static ServerMessageModel Of(MiscDao.ServerMessageEntry e)
            {
                return e is not null
                    ? new ServerMessageModel
                    {
                        Msg = e.Message,
                        Severity = e.Severity.ToString(),
                    }
                    : new ServerMessageModel
                    {
                        Msg = new ServerMessage(),
                        Severity = EvosServerMessageSeverity.Warning.ToString(),
                    };
            }
        }

        public static IResult SetMotd([FromBody] ServerMessageModel data, ClaimsPrincipal user, string type)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }

            if (!Enum.TryParse(type, true, out EvosServerMessageType messageType))
            {
                return Results.NotFound();
            }

            if (!Enum.TryParse(data.Severity, true, out EvosServerMessageSeverity messageSeverity))
            {
                return Results.BadRequest();
            }

            log.Info($"MOTD {type} {adminHandle}: [{data.Severity}] {data.Msg.EN}");
            DB.Get().MiscDao.SaveEntry(new MiscDao.ServerMessageEntry
            {
                _id = messageType.ToString(),
                Message = data.Msg,
                Severity = messageSeverity
            });
            return Results.Ok();
        }

        public static IResult GetMotd(string type)
        {
            if (!Enum.TryParse(type, true, out EvosServerMessageType messageType))
            {
                return Results.NotFound();
            }

            ServerMessageModel motd = ServerMessageModel.Of(
                DB.Get().MiscDao.GetEntry(messageType.ToString()) as MiscDao.ServerMessageEntry);
            return Results.Json(motd);
        }

        public struct PlayerDetails
        {
            public StatusController.Player player { get; set; }
            public DateTime? bannedUntil { get; set; }
            public DateTime? mutedUntil { get; set; }

            public static PlayerDetails Of(PersistedAccountData acc)
            {
                return new PlayerDetails
                {
                    player = StatusController.Player.Of(acc),
                    bannedUntil = acc.AdminComponent.Locked ? acc.AdminComponent.LockedUntil : null,
                    mutedUntil = acc.AdminComponent.Muted ? acc.AdminComponent.MutedUntil : null,
                };
            }
        }

        public static IResult GetUser(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                return Results.NotFound();
            }

            return Results.Json(PlayerDetails.Of(account));
        }

        public struct SearchResults
        {
            public List<StatusController.Player> players { get; set; }

            public static SearchResults Of(PersistedAccountData acc)
            {
                return new SearchResults
                {
                    players = new List<StatusController.Player> { StatusController.Player.Of(acc) },
                };
            }

            public static SearchResults Of(List<PersistedAccountData> accounts)
            {
                return new SearchResults
                {
                    players = accounts.Select(StatusController.Player.Of).ToList(),
                };
            }
        }

        public static IResult FindUser(string query)
        {
            query = query.ToLower();
            int delimiter = query.IndexOf('#');
            if (delimiter >= 0)
            {
                query = query.Substring(0, delimiter);
            }

            LoginDao.LoginEntry loginEntry = DB.Get().LoginDao.Find(query.ToLower());
            if (loginEntry != null)
            {
                PersistedAccountData acc = DB.Get().AccountDao.GetAccount(loginEntry.AccountId);
                if (acc != null)
                {
                    return Results.Json(SearchResults.Of(acc));
                }
            }

            List<LoginDao.LoginEntry> loginEntries = DB.Get().LoginDao.FindRegex(query);
            List<PersistedAccountData> accounts = loginEntries
                .Select(x => DB.Get().AccountDao.GetAccount(x.AccountId))
                .Where(x => x != null)
                .ToList();
            if (!accounts.IsNullOrEmpty())
            {
                return Results.Json(SearchResults.Of(accounts));
            }

            return Results.NotFound();
        }

        public class PenaltyInfo
        {
            public long accountId { get; set; }
            public int durationMinutes { get; set; }
            public string description { get; set; }
        }

        public static IResult MuteUser([FromBody] PenaltyInfo data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle)
                || !Validate(data, out error, out PersistedAccountData account))
            {
                return error;
            }

            string logString = data.durationMinutes > 0
                ? $"MUTE {account.Handle} for {TimeSpan.FromMinutes(data.durationMinutes)}"
                : $"UNMUTE {account.Handle}";
            log.Info($"API {logString} by {adminHandle} ({adminAccountId}): {data.description}");
            bool success = AdminManager.Get().Mute(data.accountId, TimeSpan.FromMinutes(data.durationMinutes), adminHandle, data.description);
            return success ? Results.Ok() : Results.Problem();
        }

        public static IResult BanUser([FromBody] PenaltyInfo data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle)
                || !Validate(data, out error, out PersistedAccountData account))
            {
                return error;
            }

            string logString = data.durationMinutes > 0
                ? $"BAN {account.Handle} for {TimeSpan.FromMinutes(data.durationMinutes)}"
                : $"UNBAN {account.Handle}";
            log.Info($"API {logString} by {adminHandle} ({adminAccountId}): {data.description}");
            bool success = AdminManager.Get().Ban(data.accountId, TimeSpan.FromMinutes(data.durationMinutes), adminHandle, data.description);
            return success ? Results.Ok() : Results.Problem();
        }

        public static IResult SendAdminMessage([FromBody] PenaltyInfo data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }
            log.Info($"API ADMIN MESSAGE by {adminHandle} ({adminAccountId}) " +
                     $"to {LobbyServerUtils.GetHandle(data.accountId)} ({data.accountId}): {data.description}");
            bool success = AdminManager.Get().SendAdminMessage(data.accountId, adminAccountId, data.description);
            return success ? Results.Ok() : Results.Problem();
        }


        public class AdminMessagesResponseModel
        {
            public List<AdminMessageEntryModel> entries { get; set; }
        }

        public class AdminMessageEntryModel
        {
            public long From { get; set; }
            public string FromHandle { get; set; }
            public string Text { get; set; }
            public DateTime SentAt { get; set; }
            public DateTime ViewedAt { get; set; }

            public static AdminMessageEntryModel Of(AdminMessageDao.AdminMessage e)
            {
                return new AdminMessageEntryModel
                {
                    From = e.adminAccountId,
                    FromHandle = LobbyServerUtils.GetHandle(e.adminAccountId),
                    Text = e.message,
                    SentAt = e.createdAt,
                    ViewedAt = e.viewedAt,
                };
            }
        }

        public static IResult GetAdminMessages(long accountId, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Error($"Cannot view admin messages: account {accountId} not found");
                return Results.NotFound();
            }
            return Results.Json(new AdminMessagesResponseModel
            {
                entries = AdminMessageManager.GetAdminMessages(accountId)
                    .Select(AdminMessageEntryModel.Of)
                    .ToList()
            });
        }

        public class AccountIdModel
        {
            public long accountId { get; set; }
        }

        public static IResult GenerateTempPassword([FromBody] AccountIdModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }
            log.Info($"API ADMIN GEN TEMP PW by {adminHandle} ({adminAccountId}) " +
                     $"to {LobbyServerUtils.GetHandle(data.accountId)} ({data.accountId})");
            string tempPassword = LoginManager.GenerateTempPassword(data.accountId);
            return tempPassword.IsNullOrEmpty()
                ? Results.Problem()
                : Results.Ok(new RegistrationCodeResponseModel { code = tempPassword });
        }

        public class RegistrationCodeRequestModel
        {
            public string issueFor { get; set; }
        }

        public class RegistrationCodeResponseModel
        {
            public string code { get; set; }
        }

        public static IResult IssueRegistrationCode([FromBody] RegistrationCodeRequestModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }

            if (!LoginManager.IsAllowedUsername(data.issueFor))
            {
                return Results.BadRequest(new ApiServer.ErrorResponseModel { message = "Invalid username" });
            }

            if (DB.Get().LoginDao.Find(data.issueFor.ToLower()) is not null)
            {
                return Results.Conflict(new ApiServer.ErrorResponseModel { message = "Username already in use" });
            }

            log.Info($"API ISSUE by {adminHandle} ({adminAccountId}): {data.issueFor}");
            string code = Guid.NewGuid().ToString();
            DB.Get().RegistrationCodeDao.Save(new RegistrationCodeDao.RegistrationCodeEntry
            {
                Code = code,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = EvosConfiguration.GetRegistrationCodeLifetime().Ticks > 0
                    ? DateTime.UtcNow.Add(EvosConfiguration.GetRegistrationCodeLifetime())
                    : DateTime.MaxValue,
                IssuedBy = adminAccountId,
                IssuedTo = data.issueFor.Trim().ToLower(),
                UsedBy = 0
            });
            return Results.Ok(new RegistrationCodeResponseModel { code = code });
        }

        public class RegistrationCodesResponseModel
        {
            public List<RegistrationCodeEntryModel> entries { get; set; }
        }

        public class RegistrationCodeEntryModel
        {
            public string Code { get; set; }
            public long IssuedBy { get; set; }
            public string IssuedByHandle { get; set; }
            public long IssuedTo { get; set; }
            public string IssuedToHandle { get; set; }
            public DateTime IssuedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime UsedAt { get; set; }

            public static RegistrationCodeEntryModel Of(RegistrationCodeDao.RegistrationCodeEntry e)
            {
                return new RegistrationCodeEntryModel
                {
                    Code = e.Code,
                    IssuedBy = e.IssuedBy,
                    IssuedByHandle = LobbyServerUtils.GetHandle(e.IssuedBy),
                    IssuedTo = e.UsedBy,
                    IssuedToHandle = e.UsedBy == 0 ? e.IssuedTo : LobbyServerUtils.GetHandle(e.UsedBy),
                    IssuedAt = e.IssuedAt,
                    ExpiresAt = e.ExpiresAt,
                    UsedAt = e.UsedAt,
                };
            }
        }

        public static IResult GetRegistrationCodes(ClaimsPrincipal user, int limit = 0, int offset = 0, int before = 0)
        {
            if (offset > 0 && before >= 0)
            {
                return Results.BadRequest("You cannot specify both offset and issue time limit.");
            }

            if (!ValidateAdmin(user, out IResult error, out _, out _))
            {
                return error;
            }

            if (limit <= 0 || limit > RegistrationCodeDao.LIMIT)
            {
                limit = RegistrationCodeDao.LIMIT;
            }

            RegistrationCodeDao dao = DB.Get().RegistrationCodeDao;
            List<RegistrationCodeEntryModel> entries = (before > 0
                    ? dao.FindBefore(limit, DateTimeOffset.FromUnixTimeSeconds(before).UtcDateTime)
                    : dao.FindAll(limit, offset))
                .Select(RegistrationCodeEntryModel.Of)
                .ToList();
            return Results.Ok(new RegistrationCodesResponseModel { entries = entries });
        }

        private static bool ValidateAdmin(
            ClaimsPrincipal user,
            out IResult error,
            out long adminAccountId,
            out string adminHandle)
        {
            error = null;
            adminAccountId = 0;
            adminHandle = user.FindFirstValue(ClaimTypes.Name);
            if (adminHandle == null || !long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out adminAccountId))
            {
                error = Results.Unauthorized();
                return false;
            }

            return true;
        }

        private static bool Validate(
            PenaltyInfo data,
            out IResult error,
            out PersistedAccountData account)
        {
            error = null;
            account = null;
            if (data.durationMinutes < 0)
            {
                error = Results.BadRequest();
                return false;
            }
            account = DB.Get().AccountDao.GetAccount(data.accountId);
            if (account == null)
            {
                error = Results.NotFound();
                return false;
            }

            return true;
        }

        public class ShutdownModel
        {
            public string processCode { get; set; }
        }


        public static IResult ShutDownServer([FromBody] ShutdownModel data, ClaimsPrincipal user)
        {
            if (!ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
            {
                return error;
            }

            if (data.processCode == null)
            {
                return Results.BadRequest();
            }

            List<Game> games = GameManager.GetGames();
            if (games == null)
            {
                return Results.NotFound();
            }
            else
            {
                foreach (Game game in games)
                {
                    if (game.ProcessCode == data.processCode)
                    {
                        if (game.GameStatus == GameStatus.Started)
                        {
                            game.Server.AdminShutdown(GameResult.TieGame);
                        }
                        else
                        {
                            game.Server.Shutdown();
                        }
                        return Results.Ok();
                    }
                }
            }
            return Results.NotFound();
        }
    }
}