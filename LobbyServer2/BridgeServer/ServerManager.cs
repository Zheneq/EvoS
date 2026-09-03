using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Config;
using EvoS.Framework;
using log4net;
using MoreLinq;

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
                         $"with address {gameServer.URI} (IsPrivate={gameServer.IsPrivate})");

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
            lock (ServerPool)
            {
                if (custom && !IsReserveFilled())
                {
                    log.Info("Failed to find a server: all servers are reserved");
                    return null;
                }
                
                foreach (BridgeServerProtocol server in GetServersInPickOrder())
                {
                    if (server.IsAvailable())
                    {
                        server.ReserveForGame();
                        return server;
                    }
                }
            }
            
            log.Info("Failed to find a server for the game");
            return null;
        }

        private static IEnumerable<BridgeServerProtocol> GetServersInPickOrder()
        {
            switch (EvosConfiguration.GetGameServerPickOrder())
            {
                case GameServerPickOrder.RANDOM:
                    return ServerPool.Values.Shuffle();
                case GameServerPickOrder.ALPHABETICAL:
                    return ServerPool.Values.OrderBy(server => server.Name);
                case GameServerPickOrder.ALPHABETICAL_REVERSED:
                    return ServerPool.Values.OrderByDescending(server => server.Name);
                default:
                    log.Error("Unknown game server pick order: " + EvosConfiguration.GetGameServerPickOrder());
                    goto case GameServerPickOrder.RANDOM;
            }
        }

        private static bool IsReserveFilled()
        {
            return ServerPool.Values.Count(server => server.IsAvailable()) > LobbyConfiguration.GetServerReserveSize();
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

        public static BridgeServerProtocol FindServerByAddress(string address)
        {
            return ServerPool.Values.FirstOrDefault(server => address.Equals(server.URI));
        }

        public static bool HasOtherServerWithFingerprint(string fingerprint, string processCode)
        {
            if (fingerprint == null)
            {
                return false;
            }
            lock (ServerPool)
            {
                return ServerPool.Values.Any(
                    s => fingerprint.Equals(s.Fingerprint) && !string.Equals(s.ProcessCode, processCode));
            }
        }

        public static void DisconnectByFingerprint(string fingerprint)
        {
            if (fingerprint == null)
            {
                return;
            }
            lock (ServerPool)
            {
                foreach (BridgeServerProtocol server in ServerPool.Values
                             .Where(s => fingerprint.Equals(s.Fingerprint))
                             .ToList())
                {
                    log.Info($"Disconnecting game server {server.ProcessCode}: key {fingerprint} was revoked");
                    server.CloseConnection();
                }
            }
        }
    }
}
