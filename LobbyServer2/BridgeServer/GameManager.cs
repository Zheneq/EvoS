using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using log4net;

namespace CentralServer.BridgeServer;

public class GameManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameManager));
    private static readonly ConcurrentDictionary<string, Game> Games = new ConcurrentDictionary<string, Game>();
    
    public static PvpGame CreatePvpGame()
    {
        // Get a server
        BridgeServerProtocol server = ServerManager.GetServer();
        if (server == null)
        {
            log.Info($"No available server for pvp game");
            return null;
        }

        PvpGame game = new PvpGame(server);
        if (!RegisterGame(server.ProcessCode, game))
        {
            log.Info($"Failed to register game {server.ProcessCode}");
            server.Shutdown();
            return null;
        }
        return game;
    }

    public static bool RegisterGame(string processCode, Game game)
    {
        return Games.TryAdd(processCode, game);
    }

    public static Game GetGameWithPlayer(long accountId)
    {
        foreach (Game game in Games.Values)
        {
            if (game.GameStatus is >= GameStatus.Launched and < GameStatus.Stopped)
            {
                foreach (long player in game.GetPlayers())
                {
                    if (player.Equals(accountId))
                    {
                        return game;
                    }
                }
            }
        }

        return null;
    }

    public static List<Game> GetGames()
    {
        return Games.Values.ToList();
    }
}