using CentralServer.BridgeServer;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// A game server coming back re-attaches to its game. The pool and the registry are a fresh
/// pair per test, wired the way the container wires them.
/// </summary>
public class GameRegistryReconnectTest : EvosTest
{
    private const string ProcessCode = "test-process";

    private readonly IServerPool _servers;
    private readonly IGameRegistry _games;

    public GameRegistryReconnectTest(ITestOutputHelper output) : base(output)
    {
        _servers = new ServerManager();
        _games = new GameManager(_servers);
    }

    private static BridgeServerProtocol Server(string processCode) => new() { ProcessCode = processCode };

    private PvpGame RegisterGame(string processCode)
    {
        PvpGame game = new(null);
        Assert.True(_games.RegisterGame(processCode, game));
        return game;
    }

    [Fact]
    public void ReconnectedServer_IsAssignedToItsGame()
    {
        PvpGame game = RegisterGame(ProcessCode);
        BridgeServerProtocol original = Server(ProcessCode);
        game.AssignServer(original);
        _servers.AddServer(original);

        BridgeServerProtocol reconnected = Server(ProcessCode);
        _servers.AddServer(reconnected);

        Assert.Same(reconnected, game.Server);
    }

    [Fact]
    public void FirstConnection_OfPublicServer_DoesNotTouchGames()
    {
        PvpGame game = RegisterGame(ProcessCode);

        _servers.AddServer(Server(ProcessCode));

        Assert.Null(game.Server);
    }

    [Fact]
    public void ReconnectedServer_WithoutGame_LeavesOtherGamesAlone()
    {
        PvpGame game = RegisterGame(ProcessCode);
        BridgeServerProtocol server = Server(ProcessCode);
        game.AssignServer(server);

        _servers.AddServer(Server("other-process"));
        _servers.AddServer(Server("other-process"));

        Assert.Same(server, game.Server);
    }
}
