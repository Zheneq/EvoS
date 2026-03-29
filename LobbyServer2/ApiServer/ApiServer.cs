using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer;
using EvoS.DirectoryServer.Account;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;
using log4net.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Prometheus;
using WebSocketSharp;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace CentralServer.ApiServer;

public abstract class ApiServer
{
    protected readonly ILog log;
    private readonly string apiKey;
    private readonly int apiPort;
    private readonly EvosAuth.Context authContext;

    protected ApiServer(ILog log, string apiKey, int apiPort, EvosAuth.Context authContext)
    {
        this.log = log;
        this.apiKey = apiKey;
        this.apiPort = apiPort;
        this.authContext = authContext;
    }

    public WebApplication Init()
    {
        if (CollectionUtilities.IsNullOrEmpty(apiKey))
        {
            log.Info($"{authContext} api server is not enabled");
            return null;
        }
        
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews();
        builder.Logging.ClearProviders();
        builder.Logging.AddLog4Net(new Log4NetProviderOptions("log4net.xml")
        { 
            LogLevelTranslator = new CustomLogLevelTranslator(),
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy =>
                {
                    policy.AllowAnyMethod().AllowAnyOrigin().AllowAnyHeader();
                });
        });
        builder.Services.AddAuthentication()
            .AddJwtBearer(o =>
            {
                o.SecurityTokenValidators.Clear();
                o.SecurityTokenValidators.Add(new EvosSecurityTokenValidator(authContext));
            });
        ConfigureBuilder(builder);
        WebApplication app = builder.Build();
        
        app.Use(async (context, next) =>
        {
            log.Debug($"API call: {context.User.FindFirstValue(ClaimTypes.Name) ?? "<anon>"} {authContext} " +
                      $"{context.Request.Method} " +
                      $"{context.Request.Path}{context.Request.QueryString}");
            await next.Invoke();
        });
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        }); 
        app.UseCors();
        app.UseMiddleware<ApiAuthMiddleware>(authContext);
        app.UseExceptionHandler(ehApp => ehApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            Exception ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

            switch (ex)
            {
                case ArgumentException:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await WriteError(context, ex.Message);
                    break;
                case ConflictException:
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await WriteError(context, ex.Message);
                    break;
                case EvosException:
                    context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                    await WriteError(context, ex.Message);
                    break;
                default:
                    log.Warn("Unhandled exception in api request", ex);
                    await WriteError(context, "Unexpected server error");
                    break;
            }
        }));
        app.UseHttpMetrics(options =>
        {
            options.AddCustomLabel("host", context => context.Request.Host.Host);
            options.AddCustomLabel("api", context => authContext.ToString());
        });
        
        ConfigureApp(app);
        
        string url = $"http://localhost:{apiPort}";
        _ = app.RunAsync(url);
        
        log.Info($"Started {authContext} api server at {url}");
        return app;
    }

    private static async Task WriteError(HttpContext context, string msg)
    {
        await context.Response.WriteAsync(
            JsonConvert.SerializeObject(new ErrorResponseModel { message = msg }),
            Encoding.UTF8);
    }

    protected virtual void ConfigureBuilder(WebApplicationBuilder builder)
    {
        
    }

    protected abstract void ConfigureApp(WebApplication app);

    protected virtual bool LoginFilter(HttpContext httpContext, LoginModel authInfo, PersistedAccountData account)
    {
        return true;
    }
    
    protected class LoginModel
    {
        public string Password { internal get; set; }  // internal so that it is not serialized
        public string UserName { get; set; }
        public List<LinkedAccount.Ticket> LinkedAccountTickets { get; set; }
        public string Code { get; set; }
        [JsonIgnore] public string _Password => Password;
    }

    protected IResult Login(HttpContext httpContext, [FromBody] LoginModel authInfo)
    {
        if (authInfo.UserName.IsNullOrEmpty() || authInfo._Password.IsNullOrEmpty())
        {
            log.Info($"Attempt to login for {authContext} access without credentials");
            return Results.Unauthorized();
        }
        long accountId;
        try
        {
            accountId = LoginManager.Login(authInfo.UserName, authInfo._Password);
        }
        catch (Exception)
        {
            log.Info($"Failed to authorize {authInfo.UserName} from {LobbyServerUtils.GetActualClientIpAddress(httpContext)} for {authContext} access");
            return Results.Unauthorized();
        }
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        if (account is null || !LoginFilter(httpContext, authInfo, account))
        {
            log.Info($"{authInfo.UserName} attempted to get {authContext} access");
            return Results.Unauthorized();
        }
        string token = EvosAuth.GenerateToken(
            authContext,
            new EvosAuth.TokenData(accountId, httpContext.Connection.RemoteIpAddress, account.Handle));
        log.Info($"{authInfo.UserName} logged in for {authContext} access");
        return Results.Ok(new LoginResponseModel
        {
            handle = account.Handle,
            token = token,
            banner = account.AccountComponent.SelectedForegroundBannerID
        });
    }
        
    public struct LoginResponseModel
    {
        public string handle { get; set; }
        public string token { get; set; }
        public long banner { get; set; }
    }
        
    public struct ErrorResponseModel
    {
        public string message { get; set; }
    }
    
    public class CustomLogLevelTranslator : ILog4NetLogLevelTranslator
    {
        public Level TranslateLogLevel(LogLevel logLevel, Log4NetProviderOptions options) {
            return logLevel switch {
                LogLevel.Critical    => Level.Critical,
                LogLevel.Error       => Level.Error,
                LogLevel.Warning     => Level.Warn,
                LogLevel.Information => Level.Debug,
                LogLevel.Debug       => Level.Debug,
                LogLevel.Trace       => Level.Debug,
                _ => Level.Debug,
            };
        }
    }
}