using System;
using EvoS.DirectoryServer;
using EvoS.DirectoryServer.Account;
using EvoS.Framework;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CentralServer.ApiServer;

public class AdminApiServer : ApiServer
{
    public AdminApiServer()
        : base(
            LogManager.GetLogger(typeof(AdminApiServer)),
            EvosConfiguration.GetAdminApiKey(),
            EvosConfiguration.GetAdminApiPort(),
            EvosAuth.Context.ADMIN_API)
    {
        if (EvosConfiguration.GetDevMode() && EvosConfiguration.GetDBConfig().Type == EvosConfiguration.DBType.None)
        {
            try
            {
                long accountId = LoginManager.Register("admin", "admin", ignoreConditions: true);
                PersistedAccountData account = LoginManager.CreateAccount(accountId, "admin");
                account.AccountComponent.AppliedEntitlements.TryAdd("DEVELOPER_ACCESS", 1);
                log.Info("Created a debug admin account");
            }
            catch (Exception e)
            {
                log.Info("Failed to create a debug admin account", e);
            }
        }
    }

    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("api_readonly", policy => policy.RequireRole("api_readonly"))
            .AddPolicy("api_admin", policy => policy.RequireRole("api_admin"));
    }

    protected override void ConfigureApp(WebApplication app)
    {
        app.MapPost("/api/admin/login", Login).AllowAnonymous();
        app.MapGet("/api/admin/lobby/status", StatusController.GetStatus).RequireAuthorization("api_readonly");
        app.MapPost("/api/admin/lobby/broadcast", AdminController.Broadcast).RequireAuthorization("api_admin");
        app.MapPut("/api/admin/lobby/motd/{type}", AdminController.SetMotd).RequireAuthorization("api_admin");
        app.MapGet("/api/admin/lobby/motd/{type}", AdminController.GetMotd).AllowAnonymous();
        app.MapPut("/api/admin/queue/paused", AdminController.PauseQueue).RequireAuthorization("api_admin");
        app.MapPut("/api/admin/server/shutdown", AdminController.ScheduleShutdown).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/server/shutdowngame", AdminController.ShutDownServer).RequireAuthorization("api_admin");
        app.MapGet("/api/admin/player/find", AdminController.FindUser).RequireAuthorization("api_admin");
        app.MapGet("/api/admin/player/details", AdminController.GetUser).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/player/muted", AdminController.MuteUser).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/player/banned", AdminController.BanUser).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/player/adminMessage", AdminController.SendAdminMessage).RequireAuthorization("api_admin");
        app.MapGet("/api/admin/player/adminMessage", AdminController.GetAdminMessages).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/player/generateTempPassword", AdminController.GenerateTempPassword).RequireAuthorization("api_admin");
        app.MapPost("/api/admin/player/registrationCode", AdminController.IssueRegistrationCode).RequireAuthorization("api_admin");
        app.MapGet("/api/admin/player/registrationCode", AdminController.GetRegistrationCodes).RequireAuthorization("api_admin");
        app.UseAuthorization();
    }

    protected override bool LoginFilter(HttpContext httpContext, LoginModel authInfo, PersistedAccountData account)
    {
        return account.AccountComponent.AppliedEntitlements.ContainsKey("DEVELOPER_ACCESS");
    }
}