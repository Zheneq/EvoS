using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using log4net;
using Microsoft.AspNetCore.Http;

namespace CentralServer.ApiServer;

public static class ModerationController
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ModerationController));

    public class ChatMessageModel
    {
        public string Message { get; set; }
        public long SenderId { get; set; }
        public string SenderHandle { get; set; }
        public string Game { get; set; }
        public DateTime Time { get; set; }
        public string Character { get; set; }
        public string Team { get; set; }
        public bool IsMuted { get; set; }
        public List<long> Recipients { get; set; }
        public List<long> BlockedRecipients { get; set; }
        public string Type { get; set; }

        public static ChatMessageModel From(ChatHistoryDao.Entry entry)
        {
            return new ChatMessageModel
            {
                Message = entry.message,
                SenderId = entry.sender,
                SenderHandle = LobbyServerUtils.GetHandle(entry.sender),
                Game = entry.game,
                Time = entry.time,
                Character = entry.characterType.ToString(),
                Team = entry.senderTeam.ToString(),
                IsMuted = entry.isMuted,
                Recipients = entry.recipients,
                BlockedRecipients = entry.blockedRecipients,
                Type = entry.consoleMessageType.ToString()
            };
        }
    }

    public class ChatHistoryResponseModel
    {
        public List<ChatMessageModel> Messages { get; set; }
    }

    public static IResult GetChatHistory(
        long accountId,
        long? after,
        long? before,
        bool? includeBlocked,
        bool? includeGeneral,
        int? limit,
        ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        if (before is null == after is null)
        {
            return Results.BadRequest(new { message = "Either specify 'before' or 'after'" });
        }
        
        bool finalIncludeBlocked = includeBlocked ?? false;
        bool finalIncludeGeneral = includeGeneral ?? false;
        int finalLimit = limit ?? 100;

        DateTime time = DateTimeOffset.FromUnixTimeSeconds(after ?? (long)before).UtcDateTime;
        List<ChatHistoryDao.Entry> messages = DB.Get()
            .ChatHistoryDao.GetRelevantMessages(
                accountId,
                finalIncludeBlocked,
                finalIncludeGeneral,
                after is not null,
                time,
                finalLimit);

        return Results.Ok(
            new ChatHistoryResponseModel
            {
                Messages = messages.Select(ChatMessageModel.From).ToList()
            });
    }
    
    public class UserFeedbackResponseModel
    {
        public List<UserFeedbackModel> Feedback { get; set; }
    }

    public class UserFeedbackModel
    {
        public long AccountId { get; set; }
        public DateTime Time { get; set; }
        public string Context { get; set; }
        public string Message { get; set; }
        public string Reason { get; set; }
        public long ReportedPlayerAccountId { get; set; }
        public string ReportedPlayerHandle { get; set; }

        public static UserFeedbackModel From(UserFeedbackDao.UserFeedback feedback)
        {
            return new UserFeedbackModel
            {
                AccountId = feedback.accountId,
                Time = feedback.time,
                Context = feedback.context,
                Message = feedback.message,
                Reason = feedback.reason.ToString(),
                ReportedPlayerAccountId = feedback.reportedPlayerAccountId,
                ReportedPlayerHandle = feedback.reportedPlayerHandle
            };
        }
    }

    public static IResult GetReceivedFeedback(long accountId, ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        var feedback = DB.Get().UserFeedbackDao.GetReportsAgainst(accountId)
            .Select(UserFeedbackModel.From)
            .ToList();

        return Results.Ok(new UserFeedbackResponseModel { Feedback = feedback });
    }

    public static IResult GetSentFeedback(long accountId, ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        var feedback = DB.Get().UserFeedbackDao.Get(accountId)
            .Select(UserFeedbackModel.From)
            .ToList();

        return Results.Ok(new UserFeedbackResponseModel { Feedback = feedback });
    }
}