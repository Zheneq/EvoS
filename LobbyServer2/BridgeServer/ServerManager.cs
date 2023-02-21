using System.Collections.Generic;
using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer
{
    public static class ServerManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServerManager));
        
        private static Dictionary<string, BridgeServerProtocol> ServerPool = new Dictionary<string, BridgeServerProtocol>();

        public static void AddServer(BridgeServerProtocol gameServer)
        {
            ServerPool.Add(gameServer.ID, gameServer);

            log.Info($"New game server connected with address {gameServer.Address}:{gameServer.Port}");
            MatchmakingManager.Update();
        }

        public static void RemoveServer(string connectionID)
        {
            ServerPool.Remove(connectionID);
            log.Info($"Game server disconnected");
        }

        public static BridgeServerProtocol GetServer()
        {
            lock (ServerPool)
            {
                foreach (BridgeServerProtocol server in ServerPool.Values)
                {
                    if (server.IsAvailable())
                    {
                        // Let MatchmakingManager start it when ready (Cause mods etc can be updated pre game)
                        //server.StartGame(gameInfo, teamInfo);

                        return server;
                    }
                }
            }
            
            return null;
        }

        public static bool IsAnyServerAvailable()
        {
            foreach (BridgeServerProtocol server in ServerPool.Values)
            {
                if (server.IsAvailable()) return true;
            }
            return false;
        }
    }
}
