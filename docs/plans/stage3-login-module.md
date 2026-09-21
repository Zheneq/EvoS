# Plan: Stage 3 step 8 — HandleRegisterGame → LoginModule

## Motivation

`HandleRegisterGame` is the last non-draft, non-chat handler on `LobbyServerProtocol`.
It is blocked by `SessionManager.OnPlayerConnect`, which takes a concrete
`LobbyServerProtocol` and sets five fields directly on it:

```
client.AccountId          = account.AccountId;
client.UserName           = account.UserName;
client.SessionToken       = sessionInfo.SessionToken;
client.SelectedGameType   = GameType.PvP;
client.SelectedSubTypeMask = 0;
```

The handler also calls three concrete members not yet on `IClientConnection`:
`CloseConnection()`, `Proxy?.Name`, and `SendLobbyServerReadyNotification()`.

Strategy:
- Add `Initialize(accountId, userName, sessionToken)`, `CloseConnection()`, and
  `string? ProxyName { get; }` to `IClientConnection`.
- Move the `SelectedGameType = GameType.PvP` default into `MatchmakingModule`
  (where it belongs). `SelectedSubTypeMask = 0` is already the `ushort` default.
- Widen `SessionManager.OnPlayerConnect` to `IClientConnection`.
- Widen `SessionInfo.conn` to `IClientConnection`; keep `GetClientConnection`
  static forwarder returning `LobbyServerProtocol?` via downcast (other callers
  still need the concrete type for `IsInGame()`, `PlayerInfo`, etc.).
- Create `LoginModule` owning `HandleRegisterGame` and the cluster of private
  helpers (`SendLobbyServerReadyNotification`, `GetServerQueueConfigurationUpdateNotification`,
  `GetLobbyStatusNotification`, `GetServerMessageOverrides`, `GetMotdText`,
  `GetMotdPopUpText`, `FetchGithubPatchNotes`, `CachedPatchNotes`).

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Add `Initialize`, `CloseConnection`, `ProxyName` to `IClientConnection`

`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:
```csharp
void Initialize(long accountId, string userName, long sessionToken);
void CloseConnection();
string? ProxyName { get; }
```

`Tests/Lib/RecordingClientConnection.cs`:

Add field: `public long SessionToken { get; set; }`
Add field: `public int CloseConnectionCalls;`
Add implementations:
```csharp
public void Initialize(long accountId, string userName, long sessionToken)
{
    AccountId = accountId;
    UserName = userName;
    SessionToken = sessionToken;
}
public void CloseConnection() => CloseConnectionCalls++;
public string? ProxyName => null;
```

`LobbyServer2/LobbyServer/LobbyServerProtocol.cs`:

`CloseConnection()` is already inherited from `WebSocketBehaviorBase` — no new body
needed. Add only:
```csharp
public void Initialize(long accountId, string userName, long sessionToken)
{
    AccountId = accountId;
    UserName = userName;
    SessionToken = sessionToken;
}
public string? ProxyName => Proxy?.Name;
```

Build must pass with zero diff in any other file.

Commit: `Add Initialize/CloseConnection/ProxyName to IClientConnection`

---

## Batch 2 — Fix `MatchmakingModule` default + widen `SessionManager.OnPlayerConnect`

`LobbyServer2/LobbyServer/Matchmaking/MatchmakingModule.cs`:

```csharp
// Before:
public GameType SelectedGameType { get; set; }
// After:
public GameType SelectedGameType { get; set; } = GameType.PvP;
```

`LobbyServer2/LobbyServer/Session/SessionManager.cs`:

1. Change `SessionInfo.conn` type (private nested class):
```csharp
// Before:
public LobbyServerProtocol conn;
// After:
public IClientConnection conn;
```

2. Change `OnPlayerConnect` parameter:
```csharp
// Before:
public static void OnPlayerConnect(LobbyServerProtocol client, RegisterGameClientRequest registerRequest)
// After:
public static void OnPlayerConnect(IClientConnection client, RegisterGameClientRequest registerRequest)
```

3. Inside `OnPlayerConnect`, replace the five field-mutation lines:
```csharp
// Remove these five lines:
client.AccountId = account.AccountId;
client.UserName = account.UserName;
client.SelectedGameType = GameType.PvP;
client.SelectedSubTypeMask = 0;
client.SessionToken = sessionInfo.SessionToken;

// Replace with:
client.Initialize(account.AccountId, account.UserName, sessionInfo.SessionToken);
```

4. The `GroupManager.CreateGroup(client.AccountId)` call stays — `client.AccountId`
   is on `IClientConnection` and `Initialize` has set it before this line.

5. Change `GetClientConnectionCore` return type:
```csharp
// Before:
private LobbyServerProtocol? GetClientConnectionCore(long accountId)
{
    SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
    return sessionInfo?.conn;
}
// After:
private IClientConnection? GetClientConnectionCore(long accountId)
{
    SessionInfos.TryGetValue(accountId, out SessionInfo sessionInfo);
    return sessionInfo?.conn;
}
```

6. Add downcast to static forwarder:
```csharp
// Before:
public static LobbyServerProtocol? GetClientConnection(long accountId)
    => Instance.GetClientConnectionCore(accountId);
// After:
public static LobbyServerProtocol? GetClientConnection(long accountId)
    => Instance.GetClientConnectionCore(accountId) as LobbyServerProtocol;
```

7. The `ISessionRegistry.GetClientConnection` explicit implementation already
   returns `IClientConnection?` — it compiles unchanged since `GetClientConnectionCore`
   now returns `IClientConnection?`.

8. `KillSessionCore` calls `sessionInfo.conn?.CloseConnection()` — compiles unchanged
   since `CloseConnection` is now on `IClientConnection`.

9. `OnServerShutdown` calls `session.conn?.Send(notify)` — compiles unchanged since
   `Send` is on `IClientConnection`.

10. `OnPlayerConnected` event is `Action<LobbyServerProtocol>`. Keep the event type
    unchanged. Add explicit cast at the raise site:
```csharp
// Before:
OnPlayerConnected(client);
// After:
OnPlayerConnected((LobbyServerProtocol)client);
```
This cast is safe: only `LobbyServerProtocol` objects ever call `OnPlayerConnect`
in production. The event type stays `Action<LobbyServerProtocol>` because `ChatManager`
subscribes with that signature — changing the event type is the separate Step 9 blocker.

Build must pass with zero diff in any other file.

Commit: `Widen SessionManager.OnPlayerConnect and SessionInfo.conn to IClientConnection`

---

## Batch 3 — Create `LoginModule` and move `HandleRegisterGame`

### New file `LobbyServer2/LobbyServer/Login/LoginModule.cs`

Copy the handler and all private helpers verbatim from `LobbyServerProtocol`,
replacing `this.X` / bare `X` with `_conn.X` for connection members, and making
`CachedPatchNotes` and `FetchGithubPatchNotes` static (they were already static on
the protocol).

The module needs these usings (derive from the existing ones in `LobbyServerProtocol.cs`):
```
System, System.Collections.Generic, System.IO, System.Linq, System.Net.Http,
System.Text, CentralServer.BridgeServer, CentralServer.LobbyServer,
CentralServer.LobbyServer.Config, CentralServer.LobbyServer.Friend,
CentralServer.LobbyServer.Gamemode, CentralServer.LobbyServer.Group,
CentralServer.LobbyServer.Quest, CentralServer.LobbyServer.Session,
CentralServer.LobbyServer.TrustWar, CentralServer.LobbyServer.Utils,
EvoS.Framework, EvoS.Framework.DataAccess, EvoS.Framework.Exceptions,
EvoS.Framework.Network.NetworkMessages, EvoS.Framework.Network.Static,
log4net, Newtonsoft.Json.Linq,
static EvoS.Framework.DataAccess.Daos.MiscDao,
static EvoS.Framework.Misc.GameUtils
```
(Trim any that the compiler says are unused.)

The handler's fan-out loop uses the static `SessionManager.GetClientConnection`
returning `LobbyServerProtocol?` and calls `player.IsInGame()` on it. This is the
one place in the module that still touches a concrete type; it is explicitly accepted
because widening `IsInGame` onto `IClientConnection` is out of scope for this step.

Skeleton:
```csharp
namespace CentralServer.LobbyServer.Login;

public class LoginModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LoginModule));
    private static readonly Lazy<string> CachedPatchNotes = new(FetchGithubPatchNotes);
    private readonly IClientConnection _conn;

    public LoginModule(IClientConnection conn) { _conn = conn; }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<RegisterGameClientRequest>(HandleRegisterGame);
    }

    private void HandleRegisterGame(RegisterGameClientRequest request)
    {
        if (request == null)
        {
            SendError(new RegisterGameClientResponse(), 0, Messages.LoginFailed);
            _conn.CloseConnection();
            return;
        }
        try
        {
            SessionManager.OnPlayerConnect(_conn, request);

            log.Info(string.Format(Messages.LoginSuccess, _conn.UserName));
            LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(request.SessionInfo.AccountId);
            RegisterGameClientResponse response = new RegisterGameClientResponse
            {
                AuthInfo = request.AuthInfo,
                SessionInfo = sessionInfo,
                ResponseId = request.RequestId
            };

            response.AuthInfo.Password = null;
            response.AuthInfo.AccountId = _conn.AccountId;
            response.AuthInfo.Handle = sessionInfo.Handle;
            response.AuthInfo.TicketData = new SessionTicketData
            {
                AccountID = _conn.AccountId,
                SessionToken = sessionInfo.SessionToken,
                ReconnectionSessionToken = sessionInfo.ReconnectSessionToken
            }.ToStringWithSignature();

            _conn.Send(response);
            SendLobbyServerReadyNotification();

            foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
            {
                LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                if (player != null && !player.IsInGame())
                {
                    player.SendSystemMessage($"<link=name>{sessionInfo.Handle}</link> connected to lobby server");
                }
            }

            DB.Get().UserMetadataDao.UpsertLastSession(_conn.AccountId, _conn.ProxyName, sessionInfo.BuildVersionInfo);
        }
        catch (RegisterGameException e)
        {
            SendError(new RegisterGameClientResponse(), request.RequestId, error: e);
            _conn.CloseConnection();
            return;
        }
        catch (Exception e)
        {
            SendError(new RegisterGameClientResponse(), request.RequestId);
            log.Error("Exception while registering game client", e);
            _conn.CloseConnection();
            return;
        }
        _conn.BroadcastRefreshFriendList();
    }

    private void SendLobbyServerReadyNotification() { /* body from LobbyServerProtocol, using _conn.AccountId */ }
    private ServerQueueConfigurationUpdateNotification GetServerQueueConfigurationUpdateNotification() { /* body unchanged */ }
    private LobbyStatusNotification GetLobbyStatusNotification(PersistedAccountData account) { /* body from LobbyServerProtocol, using _conn.AccountId in GetServerMessageOverrides */ }
    private ServerMessageOverrides GetServerMessageOverrides() { /* body from LobbyServerProtocol, using _conn.AccountId */ }
    private static ServerMessage GetMotdText() { /* body unchanged (was already static) */ }
    private static ServerMessage GetMotdPopUpText() { /* body unchanged (was already static) */ }
    private static string FetchGithubPatchNotes() { /* body unchanged (was already static) */ }

    private void SendError(WebSocketResponseMessage response, int requestId,
        string message = null, Exception error = null)
    {
        response.Success = false;
        response.ErrorMessage = message ?? error?.Message;
        response.ResponseId = requestId;
        if (message != null) log.Info($"Sending error response: {message}");
        else log.Info("Sending error response", error);
        _conn.Send(response);
    }
}
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

1. Add `using CentralServer.LobbyServer.Login;` to the using block.

2. Remove registration line from constructor:
```csharp
RegisterHandler<RegisterGameClientRequest>(HandleRegisterGame);
```

3. Add `LoginModule` to the modules array (add it FIRST, so login runs before other
   modules could theoretically interfere):
```csharp
// Before:
ILobbyModule[] modules = { new StoreModule(this), new TelemetryModule(this), ... };
// After: add new LoginModule(this) at the front
ILobbyModule[] modules = { new LoginModule(this), new StoreModule(this), new TelemetryModule(this), ... };
```

4. Remove entirely from `LobbyServerProtocol`:
   - `HandleRegisterGame` method
   - `SendLobbyServerReadyNotification` method
   - `GetServerQueueConfigurationUpdateNotification` method
   - `GetLobbyStatusNotification` method
   - `GetServerMessageOverrides` method
   - `GetMotdText` static method
   - `GetMotdPopUpText` static method
   - `FetchGithubPatchNotes` static method
   - `CachedPatchNotes` static field

Build and test, commit:
`Move HandleRegisterGame and login helpers to LoginModule`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md`:
- Add Step 8 complete section: `Initialize`, `CloseConnection`, `ProxyName` added to
  `IClientConnection`; `MatchmakingModule.SelectedGameType` initializer fixed;
  `SessionManager.OnPlayerConnect` and `SessionInfo.conn` widened; `LoginModule`
  created with `HandleRegisterGame` and `SendLobbyServerReadyNotification` cluster;
  all moved out of `LobbyServerProtocol`.
- Update "Blocked on Stage 3": remove `HandleRegisterGame`; note only chat handlers remain.
- Note: `GetClientConnection` static forwarder keeps `LobbyServerProtocol?` via downcast
  (fan-out loop in `LoginModule` still uses concrete type for `IsInGame()`).

Commit: `docs: HandleRegisterGame in LoginModule; Stage 3 step 8`

---

## Acceptance criteria

1. Zero diff outside: `IClientConnection.cs`, `RecordingClientConnection.cs`,
   `MatchmakingModule.cs`, `SessionManager.cs`, `LobbyServerProtocol.cs`,
   `Login/LoginModule.cs` (new), doc 12.
2. `grep "HandleRegisterGame\|SendLobbyServerReadyNotification\|CachedPatchNotes\|FetchGithubPatchNotes\|GetLobbyStatusNotification\|GetServerMessageOverrides\|GetServerQueueConfiguration" LobbyServerProtocol.cs` → zero hits.
3. `LoginModule` registers `RegisterGameClientRequest`.
4. `SessionManager.OnPlayerConnect` takes `IClientConnection`.
5. `SessionInfo.conn` is `IClientConnection` (private nested class).
6. `MatchmakingModule.SelectedGameType` initializes to `GameType.PvP`.
7. Full test suite passes (212 tests).
