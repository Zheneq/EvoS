# Plan: IServerPool + IGameRegistry — Stage 3 step 3

Same instance+shim pattern as the previous two steps. Two managers in one pass since they
are small and tightly coupled (`GameManager.CreatePvpGame` calls `ServerManager.GetServer`).

Module injection: `IGameRegistry` goes into `GameLifecycleModule` (already has constructor
injection; has 1 `GameManager.GetGameWithPlayer` call at line 222). `IServerPool` gets no
module injection this pass — its callers (`BridgeServerProtocol`, `CustomGameManager`,
`MatchmakingManager`) are not yet injectable.

Note: `GameManager` is already `public class` (not `static class`) but uses static fields —
the conversion is the same pattern, just the starting class declaration differs.

Repository: branch `refactor`; leave `docs/plans/` alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in the acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Interfaces

**New file `LobbyServer2/BridgeServer/IServerPool.cs`**,
namespace `CentralServer.BridgeServer`:

```csharp
public interface IServerPool
{
    void AddServer(BridgeServerProtocol gameServer);
    void RemoveServer(string processCode);
    BridgeServerProtocol GetServer(bool custom = false);
    bool IsAnyServerAvailable();
    List<BridgeServerProtocol> GetServers();
    BridgeServerProtocol FindServerByAddress(string address);
    bool HasOtherServerWithFingerprint(string fingerprint, string processCode);
    void DisconnectByFingerprint(string fingerprint);
}
```

**New file `LobbyServer2/BridgeServer/IGameRegistry.cs`**,
namespace `CentralServer.BridgeServer`:

```csharp
public interface IGameRegistry
{
    PvpGame CreatePvpGame();
    bool RegisterGame(string processCode, Game game);
    bool UnregisterGame(string processCode);
    Game GetGameWithPlayer(long accountId);
    void ReconnectServer(BridgeServerProtocol server);
    List<Game> GetGames();
    Dictionary<string, int> GetRunningGamesNum(GameType gameType);
    int GetRunningGamesNum();
    void StopAllGames();
}
```

Commit: `Add IServerPool and IGameRegistry interfaces`

---

## Batch 2 — Convert `ServerManager`

`LobbyServer2/BridgeServer/ServerManager.cs`:

1. `public static class ServerManager` → `public class ServerManager : IServerPool`
2. `Dictionary<string, BridgeServerProtocol> ServerPool` becomes a private instance field.
3. Add `public static ServerManager Instance { get; internal set; } = new ServerManager()`.
4. Every `public static` method becomes a static forwarder to a private `*Core` instance
   method; implement `IServerPool` explicitly using the same core methods.
5. Private methods (`GetServersInPickOrder`, `IsReserveFilled`, `DisconnectServer`) become
   private instance methods — bodies unchanged.
6. `AddServer` internally calls `GameManager.ReconnectServer(gameServer)` — keep this as
   `GameManager.ReconnectServer(gameServer)` (static forwarder); do not change.

Zero diff in any other file.

Commit: `Convert ServerManager to instance class with static shim`

---

## Batch 3 — Convert `GameManager`

`LobbyServer2/BridgeServer/GameManager.cs`:

1. `GameManager` is already `public class` but with static fields. Add `: IGameRegistry`.
2. `ConcurrentDictionary<string, Game> Games` becomes a private instance field.
3. The `static readonly Gauge GameNum` and the `GameTypesForStats` array stay
   `static readonly` (Prometheus duplicate-metric registration concern, same as
   `SessionManager`). The static constructor's `AddBeforeCollectCallback` body references
   `GetRunningGamesNum` — update those calls to `Instance.GetRunningGamesNum(...)`.
4. Add `public static GameManager Instance { get; internal set; } = new GameManager()`.
5. Every `public static` method → static forwarder to private `*Core` instance method;
   implement `IGameRegistry` explicitly.
6. `CreatePvpGame` internally calls `ServerManager.GetServer()` — keep as static forwarder
   call; do not change.
7. `StopAllGames` iterates `game.GetClients()` returning `LobbyServerProtocol` and calls
   `conn.SendGameUnassignmentNotification()` — body unchanged; it works via existing types.

Zero diff in any other file.

Commit: `Convert GameManager to instance class with static shim`

---

## Batch 4 — DI wiring + inject into protocol and GameLifecycleModule

### `LobbyServer2/CentralServer.cs`

Add after the `IGroupRegistry` registration:
```csharp
builder.Services.AddSingleton<IServerPool, ServerManager>();
builder.Services.AddSingleton<IGameRegistry, GameManager>();
```

After the `GroupManager.Instance = ...` line, add:
```csharp
ServerManager.Instance = (ServerManager)_app.Services.GetRequiredService<IServerPool>();
GameManager.Instance = (GameManager)_app.Services.GetRequiredService<IGameRegistry>();
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Add `IGameRegistry gameRegistry` as third constructor parameter (after `IGroupRegistry`);
store as `private readonly IGameRegistry _gameRegistry`. Pass it to `GameLifecycleModule`:

```csharp
// Before:
_gameLifecycle = new GameLifecycleModule(this);
// After:
_gameLifecycle = new GameLifecycleModule(this, gameRegistry);
```

### `LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`

Add `private readonly IGameRegistry _gameRegistry` field. Add `IGameRegistry gameRegistry`
as second constructor parameter; assign in constructor.

Replace the one static call:
```csharp
// Before (line 222):
Game game = GameManager.GetGameWithPlayer(_conn.AccountId);
// After:
Game game = _gameRegistry.GetGameWithPlayer(_conn.AccountId);
```

Build and test, then commit:
`Wire IServerPool and IGameRegistry into DI; GameLifecycleModule uses injected registry`

---

## Batch 5 — Docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 3:
- Record `IServerPool` and `IGameRegistry` introduced; `ServerManager` and `GameManager`
  converted to instance classes with static shims; DI wired; `GameLifecycleModule`
  migrated to injected `IGameRegistry`.
- Note deferred: `BridgeServerProtocol`, `CustomGameManager`, `MatchmakingManager`,
  `MatchmakingQueue`, `CustomGame`, `Game` still call the static shims.
- Update priority order: `SessionManager` ✓ → `GroupManager` ✓ →
  `ServerManager`/`GameManager` ✓ → `MatchmakingManager` next.

Commit: `docs: IServerPool and IGameRegistry wired; Stage 3 step 3 complete`

---

## Acceptance criteria

1. Zero diff in any file outside: `IServerPool.cs` (new), `IGameRegistry.cs` (new),
   `ServerManager.cs`, `GameManager.cs`, `CentralServer.cs`, `LobbyServerProtocol.cs`,
   `GameLifecycleModule.cs`, doc 12.
2. `grep "GameManager\.\|ServerManager\." LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`
   → zero hits.
3. All existing static `GameManager.X(...)` and `ServerManager.X(...)` call sites in
   other files compile unchanged.
4. Full test suite passes (212 tests).
5. `ServerManager.Instance` and `GameManager.Instance` are non-null at runtime.
