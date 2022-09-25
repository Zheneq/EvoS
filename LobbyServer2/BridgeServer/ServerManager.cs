using CentralServer.LobbyServer.Matchmaking;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace CentralServer.BridgeServer
{
    public static class ServerManager
    {
        private static Dictionary<string, BridgeServerProtocol> ServerPool = new Dictionary<string, BridgeServerProtocol>();

        public static void AddServer(BridgeServerProtocol gameServer)
        {
            ServerPool.Add(gameServer.ID, gameServer);

            Log.Print(LogType.Lobby, $"New game server connected with address {gameServer.Address}:{gameServer.Port}");
            MatchmakingManager.Update();
        }

        public static void RemoveServer(string connectionID)
        {
            ServerPool.Remove(connectionID);
            Log.Print(LogType.Lobby, $"Game server disconnected");
        }

        public static BridgeServerProtocol GetServer(LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
        {
            lock (ServerPool)
            {
                foreach (BridgeServerProtocol server in ServerPool.Values)
                {
                    if (server.IsAvailable())
                    {
                        server.StartGame(gameInfo, teamInfo);

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
