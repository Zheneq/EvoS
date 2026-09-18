using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.ApiServer;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Stats;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Misc;
using log4net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CentralServer
{
    public class CentralServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CentralServer));

        private static WebApplication _app;
        private static Task _appRunTask;

        private static Action _stopDirectoryServer;
        private static PendingShutdownType _pendingShutdown;
        public static PendingShutdownType PendingShutdown
        {
            get => _pendingShutdown;
            set
            {
                log.Info($"Pending shutdown state is now {value}");
                switch (value)
                {
                    case PendingShutdownType.None:
                        if (_pendingShutdown == PendingShutdownType.WaitForGamesToEnd)
                        {
                            MatchmakingManager.Enabled = true;
                            CustomGameManager.Enabled = true;
                        }

                        break;
                    case PendingShutdownType.Now:
                        Stop();
                        break;
                    case PendingShutdownType.WaitForGamesToEnd:
                        MatchmakingManager.Enabled = false;
                        CustomGameManager.Enabled = false;
                        break;
                    case PendingShutdownType.WaitForPlayersToLeave:
                        break;
                }

                _pendingShutdown = value;
            }
        }

        public static async Task Init(string[] args, Action stopDirectoryServer)
        {
            EvosConfiguration.ValidateConfiguration();
            EvoS.DirectoryServer.Account.LoginManager.ValidateConfiguration();
            _stopDirectoryServer = stopDirectoryServer;
            int port = EvosConfiguration.GetLobbyServerPort();

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddLog4Net(new Log4NetProviderOptions("log4net.xml")
            {
                LogLevelTranslator = new ApiServer.ApiServer.CustomLogLevelTranslator(),
            });
            _app = builder.Build();
            _app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = EvosConfiguration.GetLobbyServerTimeOut()
            });
            _app.Map("/LobbyGameClientSessionManager",
                sub => sub.Run(context => AcceptConnection(context, new LobbyServerProtocol())));
            _app.Map("/BridgeServer",
                sub => sub.Run(context => AcceptConnection(context, new BridgeServerProtocol())));

            ChatManager.Get(); // TODO Dependency injection
            await DiscordManager.Get().Start();
            StatsApi.Get();
            AdminManager.Get().Start();

            FriendsTask friendsTask = new FriendsTask(CancellationToken.None);
            _ = Task.Run(friendsTask.Run, CancellationToken.None);

            GroupsTask groupsTask = new GroupsTask(CancellationToken.None);
            _ = Task.Run(groupsTask.Run, CancellationToken.None);

            MatchmakingTask matchmakingTask = new MatchmakingTask(CancellationToken.None);
            _ = Task.Run(matchmakingTask.Run, CancellationToken.None);

            ServerStatisticsTask serverStatisticsTask = new ServerStatisticsTask(CancellationToken.None);
            _ = Task.Run(serverStatisticsTask.Run, CancellationToken.None);

            _appRunTask = _app.RunAsync($"http://0.0.0.0:{port}");
            log.Info($"Started lobby server on port {port}");

            var adminApi = new AdminApiServer().Init();
            var userApi = new UserApiServer().Init();

            if (EvosConfiguration.GetGameServerExecutable().IsNullOrEmpty())
            {
                log.Warn("GameServerExecutable not set in settings.yaml. " +
                         "Automatic game server launch is disabled. Game servers can still connect to this lobby");
            }

            if (EvosConfiguration.GetDevMode())
            {
                log.Warn("Dev mode is enabled. Proceed with caution.");
            }
        }

        public static void MainLoop()
        {
            // TODO we were checking that _server.IsListening because it could randomly stop
            // Then we started to use it to trigger shutdown manually
            try
            {
                _appRunTask?.Wait();
            }
            catch (Exception e)
            {
                log.Error("Lobby server host stopped with an error", e);
            }

            DiscordManager.Get().Shutdown();
            ReloadableConfig.ShutdownAll();

            log.Info("Lobby server is not listening, exiting...");
        }

        private static async Task AcceptConnection<TMessage>(HttpContext context, WebSocketBehaviorBase<TMessage> behavior)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            await behavior.RunConnection(socket, context);
        }

        private static void Stop()
        {
            _stopDirectoryServer();
            SessionManager.OnServerShutdown();
            _app?.StopAsync();
        }

        public enum PendingShutdownType
        {
            None,
            Now,
            WaitForGamesToEnd,
            WaitForPlayersToLeave,
        }
    }
}
