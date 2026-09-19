# Plan: ISessionRegistry — DI wiring for SessionManager (Stage 3 step 1)

Scope is deliberately narrow: introduce `ISessionRegistry`, convert `SessionManager` from
a `static class` to an instance class with a static shim, wire into ASP.NET Core DI, and
inject into `LobbyServerProtocol`. No existing module call sites are updated (they still
call `SessionManager.X(...)` statically — that migration is follow-up work). No behavior
change.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any manager or module that currently calls
   `SessionManager.X(...)` statically.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — `ISessionRegistry` interface

New file `LobbyServer2/LobbyServer/Session/ISessionRegistry.cs`:

```csharp
using System.Collections.Generic;
using System.Net;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace CentralServer.LobbyServer.Session;

public interface ISessionRegistry
{
    IClientConnection? GetClientConnection(long accountId);
    LobbySessionInfo? GetSessionInfo(long accountId);
    IEnumerable<long> GetOnlinePlayers();
    long? GetOnlinePlayerByHandle(string handle);
    long? GetOnlinePlayerByHandleOrUsername(string handleOrUsername);
    LobbySessionInfo CreateSession(long accountId, LobbySessionInfo connectingSessionInfo,
        IPAddress ipAddress, bool rejectIfActive = false);
    LobbySessionInfo? GetDisconnectedSessionInfo(long accountId);
    LobbySessionInfo? KillSession(long accountId);
    void Broadcast(WebSocketMessage message);
}
```

**Not on the interface (static-only for now):**
- `OnPlayerConnect(LobbyServerProtocol, RegisterGameClientRequest)` — sets fields directly on
  the concrete `LobbyServerProtocol` (`AccountId`, `UserName`, `SessionToken`,
  `SelectedGameType`, `SelectedSubTypeMask`); stays static until `HandleRegisterGame` moves
  to a module.
- `OnPlayerDisconnect(LobbyServerProtocol)` — same reason.
- `OnServerShutdown()` — calls `conn.CloseConnection()` which is not on `IClientConnection`.
- Events `OnPlayerConnected` / `OnPlayerDisconnected` — subscribed with `LobbyServerProtocol`-
  typed handlers by `ChatManager` etc.; widen signatures in a later pass.

Commit: `Add ISessionRegistry interface`

---

## Batch 2 — Convert `SessionManager` to instance class + static shim

`LobbyServer2/LobbyServer/Session/SessionManager.cs`:

1. Change `public static class SessionManager` →
   `public class SessionManager : ISessionRegistry`

2. All static state fields (`SessionInfos`, `ConnectingSessions`, `DisconnectedSessionInfos`,
   `LobbySize`, `ClientVersions`) become `private` instance fields (same names, same types,
   same initialization). The static constructor body moves into the instance constructor.

3. Add `public static SessionManager Instance { get; internal set; }` (set by DI in Batch 3).

4. Every existing `public static` method (query side + lifecycle) keeps its signature and
   becomes a **static forwarder** to the instance:
   ```csharp
   public static LobbyServerProtocol? GetClientConnection(long accountId)
       => Instance.GetClientConnectionCore(accountId);
   ```
   All forwarded methods delegate to a new `private` core method containing the original body.

5. Implement `ISessionRegistry` explicitly (so it doesn't collide with the same-named static):
   ```csharp
   IClientConnection? ISessionRegistry.GetClientConnection(long accountId)
       => GetClientConnectionCore(accountId);   // LobbyServerProtocol IS IClientConnection

   LobbySessionInfo? ISessionRegistry.GetSessionInfo(long accountId) => GetSessionInfoCore(accountId);
   IEnumerable<long> ISessionRegistry.GetOnlinePlayers() => GetOnlinePlayersCore();
   long? ISessionRegistry.GetOnlinePlayerByHandle(string handle) => GetOnlinePlayerByHandleCore(handle);
   long? ISessionRegistry.GetOnlinePlayerByHandleOrUsername(string h) => GetOnlinePlayerByHandleOrUsernameCore(h);
   LobbySessionInfo ISessionRegistry.CreateSession(long a, LobbySessionInfo b, IPAddress c, bool d)
       => CreateSessionCore(a, b, c, d);
   LobbySessionInfo? ISessionRegistry.GetDisconnectedSessionInfo(long a) => GetDisconnectedSessionInfoCore(a);
   LobbySessionInfo? ISessionRegistry.KillSession(long a) => KillSessionCore(a);
   void ISessionRegistry.Broadcast(WebSocketMessage m) => BroadcastCore(m);
   ```

6. `OnPlayerConnect`, `OnPlayerDisconnect`, `OnServerShutdown`, and the events remain as
   `public static` methods/fields with their existing signatures — they do NOT go on the
   interface and do NOT become instance methods yet. They may call `Instance.X()` internally
   if they need state, OR they can remain fully static by keeping private static state copies
   for those specific paths (choose whichever keeps the diff smallest).

   Actually, since all state is now on the instance, `OnPlayerConnect` etc. must call
   `Instance.SomeMethod()` for any state access. That is fine — they remain `static` in
   signature only.

Build must pass with zero changes to any other file.

Commit: `Convert SessionManager to instance class with static shim`

---

## Batch 3 — DI registration and protocol injection

### `LobbyServer2/CentralServer.cs`

In `Init`, after `WebApplicationBuilder builder = WebApplication.CreateBuilder()`:
```csharp
builder.Services.AddSingleton<ISessionRegistry, SessionManager>();
builder.Services.AddTransient<LobbyServerProtocol>();
```

Set the static shim so existing static call sites continue to work:
```csharp
// after builder.Build() / before app.Run():
SessionManager.Instance = (SessionManager)app.Services.GetRequiredService<ISessionRegistry>();
```

Change the lobby map lambda:
```csharp
_app.Map("/LobbyGameClientSessionManager",
    sub => sub.Run(context => AcceptConnection(context,
        context.RequestServices.GetRequiredService<LobbyServerProtocol>())));
```

(`BridgeServerProtocol` is unchanged — it does not use `ISessionRegistry`.)

### `LobbyServerProtocol` constructor

Add `ISessionRegistry sessionRegistry` parameter; store as `private readonly ISessionRegistry _sessionRegistry`:

```csharp
public LobbyServerProtocol(ISessionRegistry sessionRegistry)
{
    _sessionRegistry = sessionRegistry;
    // ... rest of constructor unchanged
}
```

No modules are changed in this batch. The field is available for future injection into modules
that need it.

Build and test must pass.

Commit: `Wire ISessionRegistry into DI and inject into LobbyServerProtocol`

---

## Batch 4 — docs

`docs/knowledge-base/12-design-assessment.md`:
- In §C Stage 3: record `ISessionRegistry` introduced; `SessionManager` instance + shim;
  DI wiring in place; `LobbyServerProtocol` now receives `ISessionRegistry` via constructor.
  Note what is deferred: static call sites in modules/managers, `OnPlayerConnect`/
  `OnPlayerDisconnect` interface widening, full concrete-type removal.

Commit: `docs: ISessionRegistry wired; Stage 3 step 1 complete`

---

## Acceptance criteria

1. Zero diff in any file outside: `ISessionRegistry.cs` (new), `SessionManager.cs`,
   `CentralServer.cs`, `LobbyServerProtocol.cs`, doc 12.
2. All existing static `SessionManager.X(...)` call sites compile unchanged — grep for
   `SessionManager\.` in the solution; none should need editing.
3. Full test suite passes (212 tests).
4. `LobbyServerProtocol` has a `private readonly ISessionRegistry _sessionRegistry` field.
5. `SessionManager.Instance` is non-null at runtime (set from DI before any connection lands).
