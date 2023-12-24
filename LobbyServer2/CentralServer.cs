using System;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.ApiServer;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using EvoS.Framework;
using log4net;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace CentralServer
{
    public class CentralServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CentralServer));

        private static WebSocketServer _server;

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
            _stopDirectoryServer = stopDirectoryServer;
            int port = EvosConfiguration.GetLobbyServerPort();
            _server = new WebSocketServer(port);
            _server.AddWebSocketService<LobbyServerProtocol>("/LobbyGameClientSessionManager");
            _server.AddWebSocketService<BridgeServerProtocol>("/BridgeServer");
            _server.Log.Level = LogLevel.Debug;
            _server.WaitTime = EvosConfiguration.GetLobbyServerTimeOut();

            ChatManager.Get(); // TODO Dependency injection
            await DiscordManager.Get().Start();
            AdminManager.Get().Start();

            FriendsTask friendsTask = new FriendsTask(CancellationToken.None);
            _ = Task.Run(friendsTask.Run, CancellationToken.None);

            MatchmakingTask matchmakingTask = new MatchmakingTask(CancellationToken.None);
            _ = Task.Run(matchmakingTask.Run, CancellationToken.None);
            
            _server.Start();
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
            while (_server.IsListening)
            {
                Thread.Sleep(5000);
            }
            
            DiscordManager.Get().Shutdown();
            
            log.Info("Lobby server is not listening, exiting...");
        }

        private static void Stop()
        {
            _stopDirectoryServer();
            SessionManager.OnServerShutdown();
            _server.Stop();
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
