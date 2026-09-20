# Plan: IMatchmakingManager — Stage 3 step 4

Same instance+shim pattern as previous steps. `MatchmakingModule` already has constructor
injection and makes 5 static `MatchmakingManager.X(...)` calls — those migrate in Batch 3.

Notes:
- `Queues` dict initialization (currently a static field initializer) moves to the instance
  constructor.
- `Enabled` property stays on the instance; static shim exposes it as a static property
  forwarding to `Instance.Enabled`.
- `StartPractice(LobbyServerProtocol)` is a no-op stub (all body commented out); include
  it on the interface for surface completeness.
- `StartGameAsync` is called by `MatchmakingQueue.Update()` internally — stays as a static
  shim call from there; no `MatchmakingQueue` changes needed.
- `MatchmakingModule` is a per-connection module; the injected `IMatchmakingManager` is the
  process-wide singleton passed down from the composition root.

Repository: branch `refactor`; leave `docs/plans/` alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Interface

New file `LobbyServer2/LobbyServer/Matchmaking/IMatchmakingManager.cs`,
namespace `CentralServer.LobbyServer.Matchmaking`:

```csharp
public interface IMatchmakingManager
{
    bool Enabled { get; set; }
    List<MatchmakingQueue> GetQueues();
    MatchmakingQueue GetQueue(GameType gameType);
    void Update();
    bool AddGroupToQueue(GameType gameType, GroupInfo group);
    bool RemoveGroupFromQueue(GroupInfo group, bool suppressWarnings = false);
    bool IsQueued(GroupInfo group);
    void StartPractice(LobbyServerProtocol client);
    Task StartGameAsync(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB,
        GameType gameType, List<GameSubType> gameSubTypes, int subTypeIndex,
        Dictionary<long, DateTime> queueEntryTimes = null);
    void OnGameEnded(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary,
        GameSubType gameSubType, List<MatchPlayerData> players);
}
```

Commit: `Add IMatchmakingManager interface`

---

## Batch 2 — Convert `MatchmakingManager`

`LobbyServer2/LobbyServer/Matchmaking/MatchmakingManager.cs`:

1. `public static class MatchmakingManager` → `public class MatchmakingManager : IMatchmakingManager`
2. `Queues` dict and `_enabled`/`queueUpdateRunning` become private instance fields.
   The `Queues` static field initializer moves into the instance constructor body.
3. Add `public static MatchmakingManager Instance { get; internal set; } = new MatchmakingManager()`.
4. `Enabled` becomes an instance property; add a static shim property:
   ```csharp
   // instance property (IMatchmakingManager implementation):
   public bool Enabled { get => _enabled; set { ... } }
   // static shim so CentralServer.PendingShutdown setter keeps working:
   // (the existing static property getter/setter becomes a forwarder)
   ```
   Since `Enabled` was already a static property, replace it with an instance property and
   add static forwarders `public static bool Enabled { get => Instance.Enabled; set => Instance.Enabled = value; }`.
   Wait — C# doesn't allow a static property and an instance property with the same name.
   Solution: the *instance* property implements the interface (call it e.g. internally
   `EnabledCore`), and the *static* property `Enabled` forwards to `Instance.EnabledCore`.
   OR: simply name the static forwarder differently and update the two call sites in
   `CentralServer.cs` (`MatchmakingManager.Enabled = false/true`) to use the static
   forwarder. Simplest: keep `static bool Enabled` as the static property (forwarder to
   instance), implement the interface member explicitly as `bool IMatchmakingManager.Enabled`.
   See implementation note below.

   **Implementation note for `Enabled`**: C# allows a static property and an explicit
   interface implementation with the same name. Keep the existing `public static bool Enabled`
   property forwarding to `Instance._enabled`; add explicit interface implementation
   `bool IMatchmakingManager.Enabled { get => _enabled; set { ... } }`.

5. All other `public static` methods → static forwarder + private `*Core` instance method +
   explicit `IMatchmakingManager` implementation, following the pattern of previous steps.
6. `StartGameAsync` body calls `GameManager.CreatePvpGame()` — leave as static shim call.
7. `AddGroupToQueue` body calls `GroupManager.Broadcast(...)` and
   `GroupManager.GetGroupSubTypeMask(...)` — leave as static shim calls.

Zero diff in any other file after this batch.

Commit: `Convert MatchmakingManager to instance class with static shim`

---

## Batch 3 — DI wiring + inject into protocol and MatchmakingModule

### `LobbyServer2/CentralServer.cs`

Add after the `IGameRegistry` registration:
```csharp
builder.Services.AddSingleton<IMatchmakingManager, MatchmakingManager>();
```

After `GameManager.Instance = ...`:
```csharp
MatchmakingManager.Instance = (MatchmakingManager)_app.Services.GetRequiredService<IMatchmakingManager>();
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Add `IMatchmakingManager matchmakingManager` as fourth constructor parameter; store as
`private readonly IMatchmakingManager _matchmakingManager`. Pass it to `MatchmakingModule`:

```csharp
// Before:
_matchmaking = new MatchmakingModule(this);
// After:
_matchmaking = new MatchmakingModule(this, matchmakingManager);
```

### `LobbyServer2/LobbyServer/Matchmaking/MatchmakingModule.cs`

Add `private readonly IMatchmakingManager _matchmakingManager` field. Add
`IMatchmakingManager matchmakingManager` as second constructor parameter; assign in
constructor.

Replace all 5 static calls:
```
MatchmakingManager.AddGroupToQueue(...)  →  _matchmakingManager.AddGroupToQueue(...)
MatchmakingManager.RemoveGroupFromQueue(...)  →  _matchmakingManager.RemoveGroupFromQueue(...)
MatchmakingManager.IsQueued(...)  →  _matchmakingManager.IsQueued(...)
```

Any test files that construct `LobbyServerProtocol` or `MatchmakingModule` directly need
their constructor calls updated to pass `MatchmakingManager.Instance` — fix minimally.

Build and test, commit:
`Wire IMatchmakingManager into DI; MatchmakingModule uses injected manager`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 3:
- Record `IMatchmakingManager` introduced; `MatchmakingManager` converted; DI wired;
  `MatchmakingModule` migrated (5 static call sites removed).
- Note deferred: `MatchmakingQueue`, `GroupManager.UpdateSelectedSubTypes`,
  `MatchmakingTask` still call `MatchmakingManager.X()` statically.
- Update priority order: all four named managers now converted ✓. Note remaining static
  singletons (`CustomGameManager`, `ChatManager`, `AdminManager`, etc.) as future work.

Commit: `docs: IMatchmakingManager wired; Stage 3 step 4 complete`

---

## Acceptance criteria

1. Zero diff outside: `IMatchmakingManager.cs` (new), `MatchmakingManager.cs`,
   `CentralServer.cs`, `LobbyServerProtocol.cs`, `MatchmakingModule.cs`, doc 12,
   plus minimal test constructor fixes.
2. `grep "MatchmakingManager\." LobbyServer2/LobbyServer/Matchmaking/MatchmakingModule.cs`
   → zero hits.
3. All existing `MatchmakingManager.X(...)` static call sites in other files compile
   unchanged.
4. Full test suite passes (212 tests).
5. `MatchmakingManager.Instance` non-null at runtime.
