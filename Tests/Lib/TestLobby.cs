using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Account;
using CentralServer.LobbyServer.Admin;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Login;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Store;
using CentralServer.LobbyServer.Utils;

namespace Tests.Lib;

/// <summary>
/// The one place tests construct lobby modules, connections and the protocol, so that a
/// constructor change touches this file instead of every test.
/// For now the services are the shared static <c>X.Instance</c> objects: while managers
/// still call each other through statics, a fresh graph per test would not be isolated,
/// and swapping the statics would break test classes running in parallel. This becomes a
/// real per-test graph when the managers are converted to constructor injection.
/// </summary>
public sealed class TestLobby
{
    public ISessionRegistry Sessions { get; } = SessionManager.Instance;
    public IGroupRegistry Groups { get; } = GroupManager.Instance;
    public IServerPool Servers { get; } = ServerManager.Instance;
    public IGameRegistry Games { get; } = GameManager.Instance;
    public IMatchmakingManager Matchmaking { get; } = MatchmakingManager.Instance;

    public RecordingClientConnection Connect(long accountId, string? userName = null)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        if (userName != null)
        {
            conn.UserName = userName;
        }
        return conn;
    }

    public LoginModule LoginModule(IClientConnection conn) => new(conn);
    public ChatModule ChatModule(IClientConnection conn) => new(conn);
    public StoreModule StoreModule(IClientConnection conn) => new(conn);
    public TelemetryModule TelemetryModule(IClientConnection conn) => new(conn);
    public AccountModule AccountModule(IClientConnection conn) => new(conn);
    public GroupModule GroupModule(IClientConnection conn) => new(conn, Groups);
    public FriendModule FriendModule(IClientConnection conn) => new(conn);
    public MatchmakingModule MatchmakingModule(IClientConnection conn) => new(conn, Matchmaking);
    public GameLifecycleModule GameLifecycleModule(IClientConnection conn) => new(conn, Games);
    public DraftModule DraftModule(IClientConnection conn) => new(conn);
    public AdminModule AdminModule(IClientConnection conn) => new(conn);

    /// <summary>
    /// Builds the module's matchmaking and game lifecycle dependencies unless the test
    /// passes its own (to inspect their state afterwards).
    /// </summary>
    public CharacterModule CharacterModule(
        IClientConnection conn,
        MatchmakingModule? matchmaking = null,
        GameLifecycleModule? gameLifecycle = null)
    {
        return new CharacterModule(
            conn,
            matchmaking ?? MatchmakingModule(conn),
            gameLifecycle ?? GameLifecycleModule(conn));
    }

    public LobbyServerProtocol Protocol() => new(Sessions, Groups, Games, Matchmaking);
}
