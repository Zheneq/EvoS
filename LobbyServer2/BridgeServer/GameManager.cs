using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer;
using CentralServer.Utils;
using EvoS.Framework.Constants.Enums;
using log4net;
using Prometheus;

namespace CentralServer.BridgeServer;

public class GameManager : IGameRegistry
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameManager));

    public static GameManager Instance { get; internal set; } = new GameManager();

    private readonly ConcurrentDictionary<string, Game> _games = new ConcurrentDictionary<string, Game>();

    private static readonly Gauge GameNum = Metrics
        .CreateGauge(
            "evos_lobby_games",
            "Number of ongoing games.",
            "gameType",
            "subType");

    private static readonly GameType[] GameTypesForStats = { GameType.PvP, GameType.Custom, GameType.Coop };
    static GameManager()
    {
        Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
        {
            GameManager inst = Instance;
            GameNum.Zero();
            foreach (GameType gameType in GameTypesForStats)
            {
                foreach (var (subType, runningGamesNum) in inst.GetRunningGamesNumCore(gameType))
                {
                    GameNum.WithLabels(gameType.ToString(), subType).Set(runningGamesNum);
                }
            }
            GameNum.WithLabels("Total").Set(inst.GetRunningGamesNumCore());
        });
    }

    // --- Static forwarders (keep existing call sites compiling) ---

    public static PvpGame CreatePvpGame() => Instance.CreatePvpGameCore();
    public static bool RegisterGame(string processCode, Game game) => Instance.RegisterGameCore(processCode, game);
    public static bool UnregisterGame(string processCode) => Instance.UnregisterGameCore(processCode);
    public static Game GetGameWithPlayer(long accountId) => Instance.GetGameWithPlayerCore(accountId);
    public static void ReconnectServer(BridgeServerProtocol server) => Instance.ReconnectServerCore(server);
    public static List<Game> GetGames() => Instance.GetGamesCore();
    public static Dictionary<string, int> GetRunningGamesNum(GameType gameType) => Instance.GetRunningGamesNumCore(gameType);
    public static int GetRunningGamesNum() => Instance.GetRunningGamesNumCore();
    public static void StopAllGames() => Instance.StopAllGamesCore();

    // --- IGameRegistry explicit implementation ---

    PvpGame IGameRegistry.CreatePvpGame() => CreatePvpGameCore();
    bool IGameRegistry.RegisterGame(string processCode, Game game) => RegisterGameCore(processCode, game);
    bool IGameRegistry.UnregisterGame(string processCode) => UnregisterGameCore(processCode);
    Game IGameRegistry.GetGameWithPlayer(long accountId) => GetGameWithPlayerCore(accountId);
    void IGameRegistry.ReconnectServer(BridgeServerProtocol server) => ReconnectServerCore(server);
    List<Game> IGameRegistry.GetGames() => GetGamesCore();
    Dictionary<string, int> IGameRegistry.GetRunningGamesNum(GameType gameType) => GetRunningGamesNumCore(gameType);
    int IGameRegistry.GetRunningGamesNum() => GetRunningGamesNumCore();
    void IGameRegistry.StopAllGames() => StopAllGamesCore();

    // --- Core instance methods ---

    private PvpGame CreatePvpGameCore()
    {
        // Get a server
        BridgeServerProtocol server = ServerManager.GetServer();
        if (server == null)
        {
            log.Info($"No available server for pvp game");
            return null;
        }

        PvpGame game = new PvpGame(server);
        if (!RegisterGameCore(server.ProcessCode, game))
        {
            log.Info($"Failed to register game {server.ProcessCode}");
            server.Shutdown();
            return null;
        }
        return game;
    }

    private bool RegisterGameCore(string processCode, Game game)
    {
        if (processCode is null)
        {
            log.Error("Attempting to register game with no process code");
            return false;
        }
        return _games.TryAdd(processCode, game);
    }

    private bool UnregisterGameCore(string processCode)
    {
        bool success = false;
        if (processCode is null)
        {
            log.Error("Attempting to unregister game with no process code");
        }
        else
        {
            success = _games.TryRemove(processCode, out var game);
        }

        if (CentralServer.PendingShutdown == CentralServer.PendingShutdownType.WaitForGamesToEnd
            && !_games.Values.Any(g => g.GameStatus is > GameStatus.Assembling and < GameStatus.Stopped))
        {
            CentralServer.PendingShutdown = CentralServer.PendingShutdownType.Now;
        }

        return success;
    }

    private Game GetGameWithPlayerCore(long accountId)
    {
        foreach (Game game in _games.Values)
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

    private void ReconnectServerCore(BridgeServerProtocol server)
    {
        if (_games.TryGetValue(server.ProcessCode, out Game game))
        {
            game.AssignServer(server);
        }
        else if (server.IsPrivate)
        {
            log.Warn($"Server {server.ProcessCode} reconnected, but we can't find its game");
        }
    }

    private List<Game> GetGamesCore()
    {
        return _games.Values.ToList();
    }

    private Dictionary<string, int> GetRunningGamesNumCore(GameType gameType)
    {
        return _games.Values
            .Where(g => g.GameInfo?.GameConfig?.GameType == gameType && g.GameStatus == GameStatus.Started)
            .GroupBy(g => g.GameInfo.GameConfig.SelectedSubType?.LocalizedName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private int GetRunningGamesNumCore()
    {
        return _games.Values.Count(g => g.GameStatus == GameStatus.Started);
    }

    private void StopAllGamesCore()
    {
        foreach (Game game in _games.Values)
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
