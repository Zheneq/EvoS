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
    public class ServerManager : IServerPool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServerManager));

        public static ServerManager Instance { get; internal set; } = new ServerManager();

        private readonly Dictionary<string, BridgeServerProtocol> _serverPool = new Dictionary<string, BridgeServerProtocol>();

        // --- Static forwarders (keep existing call sites compiling) ---

        public static void AddServer(BridgeServerProtocol gameServer) => Instance.AddServerCore(gameServer);
        public static void RemoveServer(string processCode) => Instance.RemoveServerCore(processCode);
        public static BridgeServerProtocol GetServer(bool custom = false) => Instance.GetServerCore(custom);
        public static bool IsAnyServerAvailable() => Instance.IsAnyServerAvailableCore();
        public static List<BridgeServerProtocol> GetServers() => Instance.GetServersCore();
        public static BridgeServerProtocol FindServerByAddress(string address) => Instance.FindServerByAddressCore(address);
        public static bool HasOtherServerWithFingerprint(string fingerprint, string processCode) => Instance.HasOtherServerWithFingerprintCore(fingerprint, processCode);
        public static void DisconnectByFingerprint(string fingerprint) => Instance.DisconnectByFingerprintCore(fingerprint);

        // --- IServerPool explicit implementation ---

        void IServerPool.AddServer(BridgeServerProtocol gameServer) => AddServerCore(gameServer);
        void IServerPool.RemoveServer(string processCode) => RemoveServerCore(processCode);
        BridgeServerProtocol IServerPool.GetServer(bool custom) => GetServerCore(custom);
        bool IServerPool.IsAnyServerAvailable() => IsAnyServerAvailableCore();
        List<BridgeServerProtocol> IServerPool.GetServers() => GetServersCore();
        BridgeServerProtocol IServerPool.FindServerByAddress(string address) => FindServerByAddressCore(address);
        bool IServerPool.HasOtherServerWithFingerprint(string fingerprint, string processCode) => HasOtherServerWithFingerprintCore(fingerprint, processCode);
        void IServerPool.DisconnectByFingerprint(string fingerprint) => DisconnectByFingerprintCore(fingerprint);

        // --- Core instance methods ---

        private void AddServerCore(BridgeServerProtocol gameServer)
        {
            lock (_serverPool)
            {
                bool isReconnection = _serverPool.Remove(gameServer.ProcessCode, out BridgeServerProtocol oldServer);
                _serverPool.TryAdd(gameServer.ProcessCode, gameServer);

                log.Info($"{(isReconnection ? "A server reconnected" : "New game server connected")} " +
                         $"with address {gameServer.URI} (IsPrivate={gameServer.IsPrivate})");

                gameServer.OnGameEnded += async (server, _, _) => await DisconnectServer(server);
                if (isReconnection || gameServer.IsPrivate)
                {
                    GameManager.ReconnectServer(gameServer);
                }
            }
        }

        private void RemoveServerCore(string processCode)
        {
            if (processCode == null)
            {
                return;
            }
            lock (_serverPool)
            {
                _serverPool.Remove(processCode);
                log.Info($"Game server disconnected");
            }
        }

        private BridgeServerProtocol GetServerCore(bool custom = false)
        {
            lock (_serverPool)
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

        private IEnumerable<BridgeServerProtocol> GetServersInPickOrder()
        {
            switch (EvosConfiguration.GetGameServerPickOrder())
            {
                case GameServerPickOrder.RANDOM:
                    return _serverPool.Values.Shuffle();
                case GameServerPickOrder.ALPHABETICAL:
                    return _serverPool.Values.OrderBy(server => server.Name);
                case GameServerPickOrder.ALPHABETICAL_REVERSED:
                    return _serverPool.Values.OrderByDescending(server => server.Name);
                default:
                    log.Error("Unknown game server pick order: " + EvosConfiguration.GetGameServerPickOrder());
                    goto case GameServerPickOrder.RANDOM;
            }
        }

        private bool IsReserveFilled()
        {
            return _serverPool.Values.Count(server => server.IsAvailable()) > LobbyConfiguration.GetServerReserveSize();
        }

        private bool IsAnyServerAvailableCore()
        {
            lock (_serverPool)
            {
                foreach (BridgeServerProtocol server in _serverPool.Values)
                {
                    if (server.IsAvailable()) return true;
                }

                return false;
            }
        }

        private async Task DisconnectServer(BridgeServerProtocol server)
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

        private List<BridgeServerProtocol> GetServersCore()
        {
            return _serverPool.Values.ToList();
        }

        private BridgeServerProtocol FindServerByAddressCore(string address)
        {
            return _serverPool.Values.FirstOrDefault(server => address.Equals(server.URI));
        }

        private bool HasOtherServerWithFingerprintCore(string fingerprint, string processCode)
        {
            if (fingerprint == null)
            {
                return false;
            }
            lock (_serverPool)
            {
                return _serverPool.Values.Any(
                    s => fingerprint.Equals(s.Fingerprint) && !string.Equals(s.ProcessCode, processCode));
            }
        }

        private void DisconnectByFingerprintCore(string fingerprint)
        {
            if (fingerprint == null)
            {
                return;
            }
            lock (_serverPool)
            {
                foreach (BridgeServerProtocol server in _serverPool.Values
                             .Where(s => fingerprint.Equals(s.Fingerprint))
                             .ToList())
                {
                    log.Info($"Disconnecting game server {server.ProcessCode}: key {fingerprint} was revoked");
                    server.Shutdown();
                    server.CloseConnection();
                }
            }
        }
    }
}
