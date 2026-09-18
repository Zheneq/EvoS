# Plan: Extract MatchmakingModule from LobbyServerProtocol (first state migration)

Fifth module extraction and the first that **moves per-session state** into a module,
per `docs/knowledge-base/12-design-assessment.md` §C Stage 1 step 4. Pattern references:
`StoreModule`, `TelemetryModule`, `AccountModule`, `GroupModule`. Read the design doc
section and `GroupModule.cs` first.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Design (read carefully — this differs from previous extractions)

`MatchmakingModule` becomes the owner of the matchmaking session state:
`IsReady`, `SelectedGameType`, `SelectedSubTypeMask`, `AllyDifficulty`,
`EnemyDifficulty` — plus the machinery that operates on it (`GetSubTypeMask`,
`SetGameType`, `SetAllyDifficulty`, `SetEnemyDifficulty`, `SetContextualReadyState`,
`ResetReadyState`, `UpdateGroupReadyState`) and the three matchmaking handlers.

**Transition rule:** `LobbyServerProtocol` keeps every one of those members as a thin
delegator to the module, with the *same signature and accessibility* it has today
(except the state fields, which become delegating properties). This guarantees all
external callers compile and behave unchanged:

- `SessionManager.OnPlayerConnect` writes `client.SelectedGameType = GameType.PvP;
  client.SelectedSubTypeMask = 0;` (SessionManager.cs ~128).
- `GroupManager` reads `SelectedGameType` (×3), `GetSubTypeMask()` (×1),
  `IsReady` (×1 in `GetMemberData`).
- `GroupModule.HandlePlayerGroupInfoUpdateRequest` calls `SetGameType` cross-connection.
- `HandlePlayerInfoUpdateRequest` (stays on the connection — it is mostly a
  character-select handler) calls `SetGameType`, `SetAllyDifficulty`,
  `SetContextualReadyState`, `SetEnemyDifficulty`.
- Game handlers staying behind (`HandleJoinGameRequest`, `HandleCreateGameRequest`,
  `HandleRejoinGameRequest`) call `ResetReadyState()`.

The connection holds a typed field `private readonly MatchmakingModule _matchmaking;`
(initialized in the constructor *before* the modules array, and included in it).

## Ground rules

1. No behavior change. Moved bodies stay verbatim except the substitutions in each
   step. Cross-connection reads/calls via `SessionManager.GetClientConnection`
   (`conn?.IsReady`, `leader.SelectedGameType` inside `UpdateGroupReadyState`) stay
   exactly as they are, typed `LobbyServerProtocol` — same deferred-coupling rule as
   the Group extraction.
2. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` must pass **unchanged**.
3. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), commit with the given
   message (no trailers).
4. Anything off-plan: stop and report. The test section's drop-and-report clause
   applies.

## Batch 1 — module owns the state; connection delegates

### `IClientConnection`: add `string UserName { get; }`

`UserName` is currently a public **field** on `LobbyServerProtocol`; convert to a
public auto-property (`{ get; set; }`) like `AccountId` was. First grep for `ref `/
`out ` usage of `UserName` — expected none; stop if found. (The queue handlers moved in
Batch 2 log it.)

### New file `LobbyServer2/LobbyServer/Matchmaking/MatchmakingModule.cs`

Namespace `CentralServer.LobbyServer.Matchmaking`. `ILobbyModule` with ctor taking
`IClientConnection`; `Register` is empty in this batch (handlers come in Batch 2).

Move from `LobbyServerProtocol` into the module:

- State: `public GameType SelectedGameType { get; set; }`,
  `public ushort SelectedSubTypeMask { get; set; }`,
  `public BotDifficulty AllyDifficulty { get; set; }`,
  `public BotDifficulty EnemyDifficulty { get; set; }`,
  `public bool IsReady { get; private set; }`,
  plus `public void Unready() => IsReady = false;` (see below for its callers).
- Methods, bodies verbatim with substitutions `AccountId` → `_conn.AccountId`,
  `Send(` → `_conn.Send(`, `SendSystemMessage(` → `_conn.SendSystemMessage(`,
  `BroadcastRefreshGroup()` → `_conn.BroadcastRefreshGroup()`,
  `CurrentGame` → `_conn.CurrentGame`, state references stay bare (module's own):
  - `GetSubTypeMask()` (public)
  - `SetGameType(GameType)` (public)
  - `SetAllyDifficulty(BotDifficulty)` / `SetEnemyDifficulty(BotDifficulty)` (public
    on the module; the connection-side delegators keep their current accessibility)
  - `SetContextualReadyState(ContextualReadyState)` (public on module) — note its
    `IsReady = ...` assignment stays a direct assignment to the module's own property
  - `ResetReadyState()` (public on module)
  - `UpdateGroupReadyState()` (public) — its `LobbyServerProtocol leader` /
    `conn?.IsReady` cross-connection loop stays verbatim per rule 1
- Module gets its own `log` field.

### `LobbyServerProtocol` becomes a delegator

- Delete the four state fields and the `IsReady` auto-property; replace with:
  ```csharp
  public GameType SelectedGameType
  {
      get => _matchmaking.SelectedGameType;
      set => _matchmaking.SelectedGameType = value;
  }
  // same shape for SelectedSubTypeMask, AllyDifficulty, EnemyDifficulty
  public bool IsReady => _matchmaking.IsReady;
  ```
- Replace the moved method bodies with one-line delegations, keeping today's
  signatures/accessibility: `GetSubTypeMask` (public), `SetGameType` (public),
  `SetAllyDifficulty`/`SetEnemyDifficulty` (protected), `SetContextualReadyState`
  (protected), `ResetReadyState` (private), `UpdateGroupReadyState` (public).
- The remaining direct `IsReady = ...` writes on the connection become:
  - `RefreshGroup`: the pair `IsReady = false; UpdateGroupReadyState();` →
    `_matchmaking.ResetReadyState();` (that method is by definition exactly those two
    statements).
  - `OnJoinGroup`, `OnLeaveGroup`, `OnStartGame`: `IsReady = false;` →
    `_matchmaking.Unready();` (no ready-state re-evaluation — same as today).
- Constructor: `_matchmaking = new MatchmakingModule(this);` before the modules array;
  array gains `_matchmaking` as its last element.

### Tests (this batch)

New `Tests/MatchmakingModuleTest.cs`, `[Collection("ClientNotifierSeam")]` (it exercises
`GroupManager`/queue statics shared with other collection members), unique AccountIds:
1. `GetSubTypeMask()` returns 1 when mask is 0; returns the mask when set.
2. `SetContextualReadyState(Ready)` for an account with no group and no penalties:
   `IsReady` becomes true (the no-group branch logs and returns after setting it).
3. `ResetReadyState()` after (2): `IsReady` false again.
4. `Unready()` sets `IsReady` false without touching anything else (fresh module,
   no group — must not throw).

Commit: `Move matchmaking session state into MatchmakingModule`

## Batch 2 — move the three handlers

Move verbatim (same substitutions as Batch 1, plus `UserName` → `_conn.UserName`) and
register in `MatchmakingModule.Register`:

| Message type | Handler | Notes |
|---|---|---|
| `JoinMatchmakingQueueRequest` | `HandleJoinMatchmakingQueueRequest` | `IsReady = true` stays a direct module-property assignment |
| `LeaveMatchmakingQueueRequest` | `HandleLeaveMatchmakingQueueRequest` | same for `IsReady = false` |
| `SetGameSubTypeRequest` | `HandleSetGameSubTypeRequest` | writes `SelectedSubTypeMask` (now module-own state) |

All three verified: no callers outside their registrations (they are `public` today —
become `private` in the module). Remove the three `RegisterHandler` lines from the
constructor. **Snapshot test must pass unchanged.**

Tests added in this batch:
5. `JoinMatchmakingQueueRequest` for an account with no group: caught by the handler's
   try/catch → exactly one `JoinMatchmakingQueueResponse` with `Success = false` and
   the `ServerError@Global` failure payload.
6. `LeaveMatchmakingQueueRequest` for an account with no group: `Success = false`
   response (same catch path).
7. `SetGameSubTypeRequest`: `SelectedSubTypeMask` updated, one `SetGameSubTypeResponse`
   sent (account in a solo group via `GroupManager.CreateGroup` so the
   `UpdateSelectedSubTypesForAccount` call takes its logged no-op path — offline
   leader).

Commit: `Move matchmaking queue handlers into MatchmakingModule`

## Batch 3 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1: record Matchmaking as the
fifth module and the **first state migration** — `IsReady`/`SelectedGameType`/
`SelectedSubTypeMask`/difficulties now live in the module, with the connection
delegating for external callers (`SessionManager` login defaults, `GroupManager` reads,
cross-connection `SetGameType`) until Stage 3 replaces those with a session-registry
interface. Small edit.

Commit: `docs: MatchmakingModule extracted with its state`

## Acceptance criteria

1. `LobbyServerProtocol` has no matchmaking state fields — only delegating properties —
   and no matchmaking handler methods; grep for `IsReady = ` in the file returns
   nothing (all writes go through the module).
2. `SessionManager`, `GroupManager`, `GroupModule` are **not modified** — their calls
   compile against the delegators.
3. Snapshot test passes unmodified in every batch; full suite green
   (182 pre-existing + new tests).
4. Changes limited to: `MatchmakingModule.cs` (new), `LobbyServerProtocol.cs`,
   `IClientConnection.cs`, `RecordingClientConnection.cs` (add `UserName`),
   `MatchmakingModuleTest.cs` (new), doc 12.
