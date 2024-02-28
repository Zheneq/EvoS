using System;
using System.Collections.Generic;
using System.Security.Claims;
using CentralServer.LobbyServer.Config;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer;
using EvoS.DirectoryServer.Account;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebSocketSharp;

namespace CentralServer.ApiServer;

public class UserApiServer : ApiServer
{
    public UserApiServer()
        : base(
            LogManager.GetLogger(typeof(UserApiServer)),
            EvosConfiguration.GetUserApiKey(),
            EvosConfiguration.GetUserApiPort(),
            EvosAuth.Context.USER_API)
    {
    }

    protected override void ConfigureApp(WebApplication app)
    {
        app.MapPost("/api/login", Login).AllowAnonymous();
        app.MapPost("/api/register", Register).AllowAnonymous();
        app.MapGet("/api/lobby/status", StatusController.GetSimpleStatus).AllowAnonymous();
        app.MapGet("/api/lobby/motd/{type}/{language}", GetMotd).AllowAnonymous();
        app.MapGet("/api/lobby/playerInfo", PlayerInfo).RequireAuthorization();
        app.MapGet("/api/account/me", ActivePlayer).RequireAuthorization();
        // app.MapGet("/api/account/linkedAccountSupport", GetThirdPartyAccountTypes).RequireAuthorization();
        // app.MapGet("/api/account/linkAccount", LinkAccount).RequireAuthorization();
        // app.MapGet("/api/account/unlinkAccount", UnlinkAccount).RequireAuthorization();
        app.MapPut("/api/account/changePassword", ChangePassword).RequireAuthorization();
        app.MapGet("/api/ticket", GetTicket).RequireAuthorization();
        app.MapGet("/api/logout", LogOutEverywhere).RequireAuthorization();
        app.UseAuthorization();
    }

    protected IResult PlayerInfo(string handle)
    {
        long AccountId = LobbyServerUtils.ResolveAccountId(0, handle);
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(AccountId);
        if (account == null)
        {
            return Results.NotFound();
        }

        return Results.Json(StatusController.Player.Of(account));
    }

    protected IResult ActivePlayer(ClaimsPrincipal user)
    {
        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(tokenData.AccountId);
        if (account == null)
        {
            return Results.NotFound();
        }

        return Results.Json(StatusController.ActivePlayer.Of(account));
    }

    protected IResult Register(HttpContext httpContext, [FromBody] LoginModel authInfo)
    {
        log.Info($"Registering via api: {authInfo.UserName}");
        try
        {
            LoginManager.Register(authInfo.UserName, authInfo._Password, authInfo.Code, authInfo.LinkedAccountTickets);
        }
        catch (ArgumentException e)
        {
            log.Info($"Cannot register {authInfo.UserName} with given credentials", e);
            return Results.BadRequest(new ErrorResponseModel { message = e.Message });
        }
        catch (Exception e)
        {
            log.Error($"Failed to register {authInfo.UserName}", e);
            return Results.Problem(null, null, StatusCodes.Status500InternalServerError);
        }

        return Login(httpContext, authInfo);
    }

    protected IResult GetTicket(ClaimsPrincipal user)
    {
        if (!EvosConfiguration.GetAllowTicketAuth())
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        log.Debug($"Generating auth ticket for {tokenData.Handle} {tokenData.AccountId}");

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(tokenData.AccountId);
        if (account is null)
        {
            return Results.NotFound();
        }

        AuthTicket authTicket = new AuthTicket(EvosAuth.GenerateToken(EvosAuth.Context.TICKET_AUTH, tokenData), account);
        return new XmlResult(authTicket.AsXML());
    }

    protected IResult LogOutEverywhere(HttpContext httpContext, ClaimsPrincipal user)
    {
        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        log.Info($"Log out everywhere: {tokenData.Handle} {tokenData.AccountId} {httpContext.Connection.RemoteIpAddress}");

        LoginManager.RevokeActiveTickets(tokenData.AccountId);
        SessionManager.KillSession(tokenData.AccountId);

        return Results.Ok();
    }

    protected class ThirdPartyAccountModel
    {
        public HashSet<LinkedAccount.AccountType> SupportedAccountTypes;
    }

    protected IResult GetThirdPartyAccountTypes()
    {
        return Results.Ok(new ThirdPartyAccountModel
        {
            SupportedAccountTypes = EvosConfiguration.GetLinkedAccountAllowedTypes()
        });
    }

    protected class LinkedAccountTicketModel
    {
        public LinkedAccount.Ticket Ticket;
    }

    protected IResult LinkAccount(ClaimsPrincipal user, [FromBody] LinkedAccountTicketModel ticket)
    {
        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        log.Info($"Linking {ticket.Ticket.Type} account to {tokenData.Handle}/{tokenData.AccountId}");
        LoginManager.LinkAccounts(tokenData.AccountId, new List<LinkedAccount.Ticket> { ticket.Ticket });
        log.Info($"Successfully linked {ticket.Ticket.Type} account to {tokenData.Handle}/{tokenData.AccountId}");
        return Results.Ok();
    }

    protected class LinkedAccountModel
    {
        public LinkedAccount Account;
    }

    protected IResult UnlinkAccount(ClaimsPrincipal user, [FromBody] LinkedAccountModel linkedAccount)
    {
        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        log.Info($"Unlinking {linkedAccount.Account.Type} account from {tokenData.Handle}/{tokenData.AccountId}");
        LoginManager.DisableLink(tokenData.AccountId, linkedAccount.Account);
        log.Info($"Successfully unlinked {linkedAccount.Account.Type} account from {tokenData.Handle}/{tokenData.AccountId}");
        return Results.Ok();
    }

    protected IResult ChangePassword(HttpContext httpContext, ClaimsPrincipal user, [FromBody] LoginModel authInfo)
    {
        EvosAuth.TokenData tokenData = EvosAuth.GetTokenData(user);
        log.Info($"Change password: {tokenData.Handle} {tokenData.AccountId} {httpContext.Connection.RemoteIpAddress}");
        LoginManager.ResetPassword(tokenData.AccountId, authInfo._Password);
        log.Info($"Successfully updated password: {tokenData.Handle} {tokenData.AccountId} {httpContext.Connection.RemoteIpAddress}");
        return Results.Ok();
    }

    private static readonly Dictionary<EvosServerMessageType, EvosServerMessageType> SERVER_MESSAGE_FALLBACK = new()
    {
        { EvosServerMessageType.LauncherMessageOfTheDay, EvosServerMessageType.MessageOfTheDay },
        { EvosServerMessageType.LauncherNotification, EvosServerMessageType.MessageOfTheDayPopup },
    };

    public class ServerMessageModel
    {
        public string Text { get; set; }
        public string Severity { get; set; }
    }

    public static IResult GetMotd(string type, string language)
    {
        if (!Enum.TryParse(type, true, out EvosServerMessageType messageType)
            || !Enum.TryParse(language, true, out ServerMessageLanguage messageLang))
        {
            return Results.NotFound();
        }

        MiscDao.ServerMessageEntry motd = DB.Get().MiscDao.GetEntry(messageType.ToString())
            as MiscDao.ServerMessageEntry;

        EvosServerMessageSeverity? severityOverride = null;
        if (motd is not null && motd.Message.IsEmpty())
        {
            severityOverride = motd.Severity;
        }

        if ((motd is null || motd.Message.IsEmpty()) && SERVER_MESSAGE_FALLBACK.TryGetValue(messageType, out var fallbackType))
        {
            motd = DB.Get().MiscDao.GetEntry(fallbackType.ToString()) as MiscDao.ServerMessageEntry;
        }

        string text = string.Empty;
        EvosServerMessageSeverity severity = EvosServerMessageSeverity.Warning;
        if (motd is not null)
        {
            text = motd.Message.GetValue(messageLang);
            if (text.IsNullOrEmpty())
            {
                text = motd.Message.GetValue(ServerMessageLanguage.EN);
            }

            severity = motd.Severity;
        }

        return Results.Json(new ServerMessageModel { Text = text, Severity = (severityOverride ?? severity).ToString().ToLower() });
    }
}