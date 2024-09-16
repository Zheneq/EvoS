using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer;
using EvoS.Framework.Constants.Enums;
using log4net;
using Prometheus;

namespace CentralServer.BridgeServer;

public class GameManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameManager));
    private static readonly ConcurrentDictionary<string, Game> Games = new ConcurrentDictionary<string, Game>();
    
    private static readonly Gauge GameNum = Metrics
        .CreateGauge(
            "evos_lobby_games",
            "Number of ongoing games.",
            "gameType");

    private static readonly GameType[] GameTypesForStats = { GameType.PvP, GameType.Custom, GameType.Coop };
    static GameManager()
    {
        Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
        {
            foreach (GameType gameType in GameTypesForStats)
            {
                GameNum.WithLabels(gameType.ToString()).Set(GetRunningGamesNum(gameType));
            }
            GameNum.WithLabels("Total").Set(GetRunningGamesNum());
        });
    }
    
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
        if (processCode is null)
        {
            log.Error("Attempting to register game with no process code");
            return false;
        }
        return Games.TryAdd(processCode, game);
    }

    public static bool UnregisterGame(string processCode)
    {
        bool success = false;
        if (processCode is null)
        {
            log.Error("Attempting to unregister game with no process code");
        }
        else
        {
            success = Games.TryRemove(processCode, out var game);
        }
        
        if (CentralServer.PendingShutdown == CentralServer.PendingShutdownType.WaitForGamesToEnd
            && !Games.Values.Any(g => g.GameStatus is > GameStatus.Assembling and < GameStatus.Stopped))
        {
            CentralServer.PendingShutdown = CentralServer.PendingShutdownType.Now;
        }

        return success;
    }

    public static Game GetGameWithPlayer(long accountId)
    {
        foreach (Game game in Games.Values)
        {
            if (game.GameStatus is >= GameStatus.Launched and < GameStatus.Stopped
                && game.Server is { IsConnected: true })
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

    public static void ReconnectServer(BridgeServerProtocol server)
    {
        if (Games.TryGetValue(server.ProcessCode, out Game game))
        {
            game.AssignServer(server);
        }
        else if (server.IsPrivate)
        {
            log.Warn($"Server {server.ProcessCode} reconnected, but we can't find its game");
        }
    }

    public static List<Game> GetGames()
    {
        return Games.Values.ToList();
    }

    public static int GetRunningGamesNum(GameType gameType)
    {
        return Games.Values.Count(g => g.GameInfo?.GameConfig?.GameType == gameType && g.GameStatus == GameStatus.Started);
    }

    public static int GetRunningGamesNum()
    {
        return Games.Values.Count(g => g.GameStatus == GameStatus.Started);
    }

    public static void StopAllGames()
    {
        foreach (Game game in Games.Values)
        {
            if (game.GameInfo is not null)
            {
                game.GameInfo.GameStatus = GameStatus.Stopped;
                game.SendGameInfoNotifications();
                foreach (LobbyServerProtocol conn in game.GetClients())
                {
                    conn?.SendGameUnassignmentNotification();
                }
            }
            game.Terminate();
        }
    }
}