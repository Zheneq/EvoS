using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework;
using log4net;

namespace CentralServer.BridgeServer
{
    public static class ServerManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServerManager));

        private static readonly Dictionary<string, BridgeServerProtocol> ServerPool = new Dictionary<string, BridgeServerProtocol>();

        public static void AddServer(BridgeServerProtocol gameServer)
        {
            lock (ServerPool)
            {
                bool isReconnection = ServerPool.Remove(gameServer.ProcessCode, out BridgeServerProtocol oldServer);
                ServerPool.TryAdd(gameServer.ProcessCode, gameServer);

                log.Info($"{(isReconnection ? "A server reconnected" : "New game server connected")} " +
                         $"with address {gameServer.Address}:{gameServer.Port} (IsPrivate={gameServer.IsPrivate})");

                gameServer.OnGameEnded += async (server, _, _) => await DisconnectServer(server);
                if (isReconnection || gameServer.IsPrivate)
                {
                    GameManager.ReconnectServer(gameServer);
                }
            }
        }

        public static void RemoveServer(string processCode)
        {
            if (processCode == null)
            {
                return;
            }
            lock (ServerPool)
            {
                ServerPool.Remove(processCode);
                log.Info($"Game server disconnected");
            }
        }

        public static BridgeServerProtocol GetServer(bool custom = false)
        {
            int num = 0;
            lock (ServerPool)
            {
                Random rnd = new Random();
                foreach (BridgeServerProtocol server in ServerPool.Values.OrderBy((item) => rnd.Next()))
                {
                    if (server.IsAvailable())
                    {
                        if (!custom || num >= LobbyConfiguration.GetServerReserveSize())
                        {
                            server.ReserveForGame();
                            return server;
                        }

                        num++;
                    }
                }
            }
            
            log.Info($"Failed to find a server for the game ({num} servers available)");

            return null;
        }

        public static bool IsAnyServerAvailable()
        {
            lock (ServerPool)
            {
                foreach (BridgeServerProtocol server in ServerPool.Values)
                {
                    if (server.IsAvailable()) return true;
                }

                return false;
            }
        }
        
        private static async Task DisconnectServer(BridgeServerProtocol server)
        {
            await Task.Delay(
                LobbyConfiguration.GetServerGGTime()
                + LobbyConfiguration.GetServerShutdownTime()
                + TimeSpan.FromSeconds(15));

            if (server.IsConnected)
            {
                server.Shutdown();
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            if (server.IsConnected)
            {
                server.CloseConnection();
            }
        }

        public static List<BridgeServerProtocol> GetServers()
        {
            return ServerPool.Values.ToList();
        }
    }
}
