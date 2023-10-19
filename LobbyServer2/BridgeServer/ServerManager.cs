using System;
using System.Collections.Generic;
using System.Linq;
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
                bool newServer = ServerPool.TryAdd(gameServer.ProcessCode, gameServer);

                log.Info($"{(newServer ? "New game server connected" : "A server reconnected")} with address {gameServer.Address}:{gameServer.Port}");
                MatchmakingManager.Update();
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

        public static List<BridgeServerProtocol> GetServers()
        {
            return ServerPool.Values.ToList();
        }
    }
}
