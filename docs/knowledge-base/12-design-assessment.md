# 12 – Design Assessment & Refactoring Roadmap

Goal: clearer, more modular, more testable. This doc lists the shortcomings observed in
docs 01–11, why they matter, and a staged refactoring plan that respects the constraint
that this is a live, working emulator (wire compatibility with an unmodifiable client and
game server must be preserved).

## A. Shortcomings

### A1. Pervasive static singletons / global state (highest impact)

`SessionManager`, `GroupManager`, `MatchmakingManager`, `ServerManager`, `GameManager`,
`CustomGameManager`, `Elo` are static classes; `ChatManager`, `DiscordManager`,
`AdminManager`, `StatsApi`, `DB` are `Get()` singletons; `EvosConfiguration` and all
`GameData` classes are static-lazy. `CentralServer.Init` even carries the comment
`// TODO Dependency injection`.

Consequences: no seams for testing (doc 11), hidden dependency graph (any file may call
any manager), initialization-order fragility (lazy singletons under concurrency — the
`DB.Get()` race that once flaked tests), and impossible-to-scale-out state.

### A2. God classes fusing transport and domain logic

- ~~`LobbyServerProtocol` (2,896 lines, ~75 handlers): store purchases, friends, groups,
  draft, telemetry, chat — all as methods on a websocket connection. Domain logic is
  unreachable without a socket, and the class is a merge-conflict magnet.
  The `LobbyServerProtocolBase` split is nominal: `LobbyServerProtocol` is its only
  subclass, the base is not abstract, all its state fields are public, and it mixes
  transport plumbing (serialization, proxy patching) with domain logic
  (`SendLobbyServerReadyNotification` composes the login state dump, fetches GitHub patch
  notes over HTTP, reads MOTD from the DB). The real transport abstraction is
  `WebSocketBehaviorBase<TMessage>`; the middle layer earns nothing.~~ **Partially
  addressed (Stage 1 in progress):** `LobbyServerProtocolBase` merged; 8 modules extracted
  (Store, Telemetry, Account, Group, Matchmaking, GameLifecycle, Character, Friend) — ~2,100
  lines moved out; protocol file reduced from ~2,900 to ~800 lines. Remaining handlers
  blocked on Stage 3 or deferred to Stage 4 (see §C Stage 1).
- `Game` (1,699 lines): team assembly, bot filling, character validation, ranked draft
  state machine, dodge penalties, queue-priority compensation, result finalization,
  Discord/Elo/TrustWar integration.
- `MatchmakingQueue` (788 lines): queue bookkeeping + sub-type mask algebra + match
  scoring orchestration + client notification composition.

### A3. Layering violations & naming drift

- Framework hosts app-layer logic under foreign namespaces (`EvoS.DirectoryServer.Account.LoginManager`
  in `EvoS.Framework/DataAccess/`, `InventoryManager`, `CharacterManager`, lobby config
  types in `CentralServer.LobbyServer.Config`) — see doc 08. Dependency direction is
  only enforced by project references, not by structure or namespaces.
- Folder `LobbyServer2` / project `CentralServer` / namespace `CentralServer.LobbyServer`.
- Two `CharacterManager`s disambiguated by using-aliases.
- `EvoS.DirectoryServer` is a 1-file project whose actual logic lives in Framework.

### A4. Bidirectional coupling between managers and connections

Managers call `SessionManager.GetClientConnection(id)?.Send(...)` to push state;
connections call managers to mutate state; managers call back into connection methods
(`BroadcastRefreshGroup`, `OnLeaveGroup`). Notification fan-out is duplicated at every
call site instead of being an outbound port. `SessionManager.Broadcast` routing through
"any first connection" is the emblematic hack.

**Partially fixed:** `IClientNotifier` outbound port introduced in
`LobbyServer2/LobbyServer/Session/`; `QueuePenaltyManager`, `MatchmakingQueue`,
`MatchmakingManager`, `GroupManager`, `ChatManager`, `TrustWarManager`, `MapPickBanSession`,
`CrashReportManager`, `FriendsTask`, `FriendManager`, `CustomGame`, and `Game` (37
notification sites total) migrated. Tested via `RecordingClientNotifier` +
`ClientNotifierScope`. Remaining `GetClientConnection` call sites are read-then-use or
connection-mutating and are deferred: `LobbyServerProtocol` (10, Stage 1 service
extraction), `Game.cs` (8, Stage 4 split), `CustomGame.cs` (3, Stage 3/4), `GroupManager`
(4), `AdminManager` (1, `CloseConnection` = session control), `StatusController` (2),
Discord classes (4, `DiscordManager` 1 + `DiscordBotWrapper` 1 + `DiscordLobbyUtils` 2),
`FriendManager` (1) — Stage 3 `ISessionRegistry`.

### A5. Mixed concurrency idioms

- `lock (SessionInfos)` *around* a `ConcurrentDictionary` (the lock is what actually
  provides atomicity; the concurrent type suggests otherwise).
- Plain `Dictionary` + `lock` in `ServerManager`/`CustomGameManager`/`GroupManager`;
  lock objects sometimes shared across classes (`Game.characterSelectionLock` is a
  `public static object` also taken by `LobbyServerProtocol`).
- Sync-over-async (`Send` → `.GetAwaiter().GetResult()`), `async void` methods
  (`Game.OnServerDisconnect`, `Sandbox Program.OnExecute`), fire-and-forget `Task.Run`
  for the periodic tasks with `CancellationToken.None` (no orderly shutdown).
- Time and delays hard-coded (`DateTime.Now` vs `DateTime.UtcNow` inconsistently;
  `Task.Delay` in `ServerManager.DisconnectServer`, draft timers).

### A6. Shared mutable cached entities

`AccountDao` (cached) returns shared `PersistedAccountData` instances mutated in place by
many subsystems, then saved wholesale (`UpdateAccount`) or per-component. No unit of work,
no optimistic concurrency; lost updates are possible whenever two handlers touch the same
account concurrently.

### A7. Login-path warts

- ~~`PatchAccountData` always returns true → full account rewrite on every login; contains
  a 3× copy-pasted loadout-patch block.~~ **Fixed:** now change-detected via JSON snapshot
  comparison, deduplicated, and tested (`PatchAccountDataTest`); versioned migrations remain
  future work.
- DB failure during login silently creates `temp_user#N` accounts (state divergence that
  later persists partial data).

### A8. Config sprawl

Five formats/loaders, only one hot-reloadable, all static, all CWD-relative (doc 10).

### A9. Dead code & stubs

Commented-out `MatchmakingManager.StartPractice`, `QuestManager` stub, disabled queue
types, `.idea/shelf` archives checked into the repo, `README.md` describing VS2019/.NET
Core 2.2 while the code targets modern .NET (9 in Docker).

## B. What already points the right way (build on these)

- `MatchmakerRanked(AccountDao, ..., Func<Config>)` + convenience ctor defaulting to
  `DB.Get()` — incremental DI without breaking callers.
- `Elo` parameterized by `IAccountProvider`/`IMatchHistoryProvider`/`IAccountUpdater`.
- `BridgeServerProtocol` ↔ `Game` decoupled via events; `IGameServerConnection` interface.
- DAO interface / impl / cached-decorator / mock structure.
- `PeriodicRunner`, `ReloadableConfig` as reusable mechanisms.
- `WebSocketBehaviorBase` cleanly isolates websocket mechanics.

## C. Refactoring roadmap (staged, each stage shippable)

### Stage 1 — Decompose `LobbyServerProtocol` into composed handler modules (no behavior change)

Design (supersedes the earlier "extract services" sketch): the connection *composes*
per-connection module instances; each module registers its own handlers into the existing
`RegisterHandler<T>` dispatch table and owns its slice of per-session state.

1. ~~Merge `LobbyServerProtocolBase` into `LobbyServerProtocol` (the split is nominal, see
   A2). Its transport plumbing folds into the connection; its domain logic
   (`SendLobbyServerReadyNotification`, MOTD/patch notes) becomes Login/Status module
   material.~~ **Done.**
2. Seam contracts:
   - `IClientConnection` (AccountId, Send, ... — grown only as modules need connection-shaped
     members: identity, send, refresh hooks). Modules that need other modules receive them via
     constructor from the composition root (see `CharacterModule` — module-to-module dependency
     precedent), not through `IClientConnection`.
   - `IHandlerRegistry` (`Register<T>(Action<T>)`), implemented by the connection over
     the dispatch table. `Dictionary.Add` throwing on duplicates gives fail-fast when
     two modules claim the same message type.
   - `ILobbyModule` (`Register(IHandlerRegistry)`; lifecycle hooks such as
     `OnDisconnect` added when the first module needs them).
3. Module map (~8 modules, 5–10 handlers each): **Store** (all `Purchase*`, prices,
   store stubs), **Group** (invite/join/confirm/suggest/kick/promote/leave),
   **Matchmaking** (queue join/leave, subtype, ready state), **GameLifecycle**
   (join/rejoin/leave/spectate/create), **Chat**, **Account** (options, keybinds, UI
   state, dev tag, customization selects), **Telemetry** (crash/error/feedback),
   **Login/Status** (ready notification, MOTD, patch notes).
4. State ownership is the point of the exercise — modules that only split methods but
   share `conn.IsReady`/`conn.CurrentGame` stay coupled through a god-state object.
   Target owners: `SelectedGameType`/`SelectedSubTypeMask`/difficulties → Matchmaking;
   `IsReady` → Matchmaking/Group; `CurrentGame`/`Status` → GameLifecycle;
   `AccountId`/`SessionToken`/`UserName`/`Proxy` stay on the connection. State migrates
   with its module, exposed to other modules via narrow interfaces.
5. Safety nets: a snapshot test asserting the set of registered message types is
   unchanged after every module extraction (wire contract), plus per-module unit tests
   against mock DAOs and a recording `IClientConnection`. **Snapshot test
   (`LobbyHandlerRegistrationTest`) added; 75 message types locked.**
6. Order: Base merge → snapshot test → **Store** pilot (most self-contained: needs only
   `AccountId` + `Send`, no per-connection state) → Telemetry, Account (nearly
   stateless) → Group, Matchmaking, GameLifecycle (where state migration happens) →
   Login/Status. External callers of moved members keep working via delegation on the
   connection during the transition. **Store pilot (`StoreModule`, 13 handlers) extracted
   and tested (`StoreModuleTest`, 9 cases). `TelemetryModule` (8 handlers) extracted and
   tested (`TelemetryModuleTest`, 4 cases) — second module after Store. `AccountModule`
   (15 handlers: options, keybinds, UI state, dev tag, customization selects, match data,
   RAF stubs, check account/loading screen) extracted and tested (`AccountModuleTest`,
   13 cases) — third module; `IClientConnection` grown with `OnAccountVisualsUpdated()`.
   `GroupModule` (8 handlers: invite/join/confirm/suggest/kick/promote/leave/group-info-update)
   extracted and tested (`GroupModuleTest`, 6 cases) — fourth module; `IClientConnection` grown
   with `Handle`, `SendSystemMessage`, `BroadcastRefreshFriendList`, `BroadcastRefreshGroup`.
   `MatchmakingModule` (3 handlers: `JoinMatchmakingQueueRequest`, `LeaveMatchmakingQueueRequest`,
   `SetGameSubTypeRequest`) extracted and tested (`MatchmakingModuleTest`, 7 cases) — **fifth
   module and first state migration**: `IsReady`, `SelectedGameType`, `SelectedSubTypeMask`,
   `AllyDifficulty`, `EnemyDifficulty` now live in the module; `LobbyServerProtocol` keeps
   delegating properties and thin delegator methods so external callers (`SessionManager` login
   defaults, `GroupManager` reads, cross-connection `SetGameType` from `GroupModule`) compile
   unchanged. This is the Stage 3 transition point: those delegation seams stay until a
   `ISessionRegistry` interface replaces direct `GetClientConnection` reads.
   `IClientConnection` grown with `UserName`.
   `GameLifecycleModule` (11 handlers: `JoinGameRequest`, `CreateGameRequest`,
   `GameInfoUpdateRequest`, `BalancedTeamRequest`, `LeaveGameRequest`,
   `PreviousGameInfoRequest`, `GameInvitationRequest`, `GameInviteConfirmationResponse`,
   `RankedLeaderboardOverviewRequest`, `CalculateFreelancerStatsRequest`,
   `PlayerPanelUpdatedNotification`) extracted and tested (`GameLifecycleModuleTest`, 11 cases) —
   **sixth module and second state migration**: `CurrentGame` now lives in the module;
   `LobbyServerProtocol` keeps thin delegators (`CurrentGame`, `JoinGame`, `LeaveGame`,
   `IsInGame`, `IsInCharacterSelect`, `PlayerInfo`) so `Game.cs`, `PvpGame.cs`,
   `CustomGame.cs`, `GameManager.cs`, `FriendManager.cs`, `ChatManager.cs`, `GroupManager.cs`,
   `GroupModule.cs`, `MatchmakingModule.cs`, and Discord classes compile unchanged.
   `IClientConnection` grown with `ResetReadyState` and `SendGameUnassignmentNotification`.
   **`Status` (`PlayerOnlineStatus`) deliberately left on the connection** (friend-status
   concern, not game-lifecycle state). **Game callbacks (`OnStartGame`, `OnGameAssigned`,
   `SendGameUnassignmentNotification`) deliberately left on the connection** (invoked by
   `Game`/`PvpGame`/`CustomGame`/`GameManager` on concrete connections; no state ownership).
   **Chat module deferred entirely**: its two handlers (`ChatNotification`,
   `GroupChatRequest`) are one-line event raises, and `ChatManager` subscribes to those events
   per connection with `LobbyServerProtocol`-typed signatures and reads `conn.PlayerInfo` —
   extracting them means redesigning that coupling for no handler-logic gain. Revisit at Stage 3.
   `CharacterModule` (2 handlers: `PlayerInfoUpdateRequest`, `UpdateRemoteCharacterRequest`;
   2 private helpers: `HandlePlayerInfoUpdateRequest`, `UpdateCharacterSlots`) extracted and
   tested (`CharacterModuleTest`, 6 cases) — **seventh module and first module-to-module
   dependency**: `CharacterModule` takes `MatchmakingModule` and `GameLifecycleModule` via
   constructor from the composition root; `IClientConnection` stays connection-shaped (no new
   members added). Unused delegators `SetAllyDifficulty`, `SetEnemyDifficulty`,
   `SetContextualReadyState` deleted from `LobbyServerProtocol` (their only caller,
   `HandlePlayerInfoUpdateRequest`, moved with the module). `SetGameType` kept (public —
   `GroupModule` calls it cross-connection).
   `FriendModule` (2 handlers: `FriendUpdateRequest`, `PlayerUpdateStatusRequest`; 1 private
   helper: `Unblock`) extracted and tested (`FriendModuleTest`, 5 cases) — **eighth module;
   fully extracted; stateless (no state migration)**. `Status` (`PlayerOnlineStatus`) added to
   `IClientConnection`; `FriendManager.OnPlayerUpdateStatusRequest` widened to
   `IClientConnection`; `HandlePlayerUpdateStatusRequest` moved from `LobbyServerProtocol` to
   `FriendModule`; `FriendManager.MarkForUpdate(LobbyServerProtocol)` overload deleted.
   `IClientConnection` grown with `Status { get; set; }` (Step 5, see §C Stage 3).
   ~~`UseOverconRequest` / `UseGGPackRequest` (iterate `CurrentGame.GetClients()` — natural fit
   in `GameLifecycleModule`)~~ **Done:** moved to `GameLifecycleModule` (ninth and tenth
   handlers added to that module; no new `IClientConnection` members; covariance on
   `IEnumerable<T>` allows `foreach (IClientConnection client in CurrentGame.GetClients())`).
   ~~`DEBUG_AdminSlashCommandNotification` (self-contained, ~30 lines, could be an
   `AdminModule`)~~ **Done:** `AdminModule` extracted to
   `LobbyServer2/LobbyServer/Admin/AdminModule.cs`, namespace
   `CentralServer.LobbyServer.Admin`, 1 handler; registered in `LobbyServerProtocol`
   constructor after the module `foreach` loop.
   **What remains on the connection — categorized:**
   - *Blocked on Stage 3* (`SessionManager` takes concrete `LobbyServerProtocol`):
     `HandleRegisterGame`, chat handlers (`ChatNotification`, `GroupChatRequest`).
   - *Stage 4*: ranked draft handlers (`RankedTradeRequest`, `RankedSelectionRequest`,
     `RankedBanRequest`, `RankedHoverClickRequest`) — belong in `DraftController`.
   **Stage 1 is fully complete. All handlers that could move in Stage 1 have been extracted.
   Proceeding to Stage 3 (de-static the managers) unlocks the blocked group and makes the
   remaining small extractions straightforward.**

**Step 7 complete (branch `refactor`):**
- `bool IsConnected { get; }` added to `IClientConnection`; stub implementation
  `public bool IsConnected { get; set; } = true` added to `RecordingClientConnection`.
- `CustomGameManager.Subscribers` dict widened from `Dictionary<long, LobbyServerProtocol>`
  to `Dictionary<long, IClientConnection>`; `Subscribe` and `Unsubscribe` parameters widened
  from `LobbyServerProtocol` to `IClientConnection`; `NotifyUpdate` foreach variable widened
  to `IClientConnection`. `using CentralServer.LobbyServer.Session` added to
  `CustomGameManager.cs`.
- `HandleSubscribeToCustomGamesRequest` and `HandleUnsubscribeFromCustomGamesRequest` moved
  from `LobbyServerProtocol` to `GameLifecycleModule`: two `registry.Register` calls added in
  `Register`; two private handler methods added that delegate to `CustomGameManager.Subscribe(_conn)`
  and `CustomGameManager.Unsubscribe(_conn)`. Registration lines and method bodies removed from
  `LobbyServerProtocol`.
- Remaining blocked on Stage 3: `HandleRegisterGame` (blocked on
  `SessionManager.OnPlayerConnect` field-mutation pattern) and chat handlers
  (`ChatNotification`, `GroupChatRequest`, blocked on `ChatManager` event redesign).

### Stage 2 — Introduce an outbound notification port

Create `IClientNotifier` (send-to-account, broadcast-to-group, broadcast-to-all) backed by
`SessionManager`. Replace direct `SessionManager.GetClientConnection(x)?.Send(...)`
call sites in managers. This kills the manager→connection coupling and makes fan-out
testable (assert on a recording notifier).

### Stage 3 — De-static the managers behind interfaces

Convert one manager at a time to an instance class with an interface
(`ISessionRegistry`, `IGroupRegistry`, `IServerPool`, `IGameRegistry`), keeping a static
`Instance` shim so call sites migrate gradually. Compose them in `CentralServer.Init`
(or Microsoft DI, which ASP.NET already provides). Priority order:
`SessionManager` → `GroupManager` → `ServerManager`/`GameManager` → `MatchmakingManager`.

**Step 1 complete (branch `refactor`):**
- `ISessionRegistry` introduced in `LobbyServer2/LobbyServer/Session/ISessionRegistry.cs`
  with members: `GetClientConnection`, `GetSessionInfo`, `GetOnlinePlayers`,
  `GetOnlinePlayerByHandle`, `GetOnlinePlayerByHandleOrUsername`, `CreateSession`,
  `GetDisconnectedSessionInfo`, `KillSession`, `Broadcast`. `OnPlayerConnect`,
  `OnPlayerDisconnect`, `OnServerShutdown`, and the events are not on the interface.
- `SessionManager` converted from `static class` to `public class SessionManager : ISessionRegistry`
  with a `public static SessionManager Instance { get; internal set; }` shim.
  State fields (`SessionInfos`, `ConnectingSessions`, `DisconnectedSessionInfos`) became
  instance fields. Prometheus `Gauge` fields stayed `static readonly` to avoid metric
  re-registration. All existing `public static` methods are static forwarders calling
  private `*Core` instance methods. `ISessionRegistry` implemented explicitly.
  `Instance` pre-initialized to `new SessionManager()` so tests (which never call
  `CentralServer.Init`) still get an empty-state instance and all static forwarders
  remain callable without any test-setup change.
- DI wired in `CentralServer.Init`: `AddSingleton<ISessionRegistry, SessionManager>` +
  `AddTransient<LobbyServerProtocol>`; `SessionManager.Instance` overwritten from DI after
  `builder.Build()` so production uses the DI-managed singleton; the lobby map lambda
  resolves `LobbyServerProtocol` from `context.RequestServices` instead of `new`.
- `LobbyServerProtocol` receives `ISessionRegistry sessionRegistry` via constructor and
  stores it as `private readonly ISessionRegistry _sessionRegistry`.

**Deferred to follow-up steps (after step 1):**
- Static call sites in modules/managers (`SessionManager.GetClientConnection(...)` etc.)
  still call the static shim; migration to `ISessionRegistry` injection is follow-up work.
- `OnPlayerConnect`/`OnPlayerDisconnect` interface widening (they set concrete
  `LobbyServerProtocol` fields directly and are not yet on `ISessionRegistry`).
- Full concrete-type removal: `GroupManager`, `Game`, `CustomGame`, `FriendManager` etc.
  still receive `LobbyServerProtocol` from static `GetClientConnection`.

**Step 2 complete (branch `refactor`):**
- `IGroupRegistry` introduced in `LobbyServer2/LobbyServer/Group/IGroupRegistry.cs`
  with all public method members of `GroupManager`: `Lock`, `GetGroup`, `GetGroupMembers`,
  `GetGroups`, `GetPlayerGroup`, `CreateGroupRequest`, `PopGroupRequest`, `PingGroupRequests`,
  `CreateGroup`, `LeaveGroup`, `JoinGroup`, `PromoteMember`, `GetGroupInfo`, `GetGroupID`,
  `OnLeaveQueue`, `Broadcast`, `BroadcastSystemMessage`, `GetGroupSubTypeMask` (two overloads),
  `UpdateSelectedSubTypes`, `UpdateSelectedSubTypesForAccount`.
- `GroupManager` converted from non-public `static class` to `public class GroupManager : IGroupRegistry`
  with a `public static GroupManager Instance { get; internal set; }` shim pre-initialized to
  `new GroupManager()`. All static state fields (`ActiveGroups`, `PlayerToGroup`, `GroupRequests`,
  `_lastGroupId`, `_lastGroupRequestId`, `_lock`) became private instance fields. Every
  `public static` method is a static forwarder calling a private `*Core` instance method.
  `IGroupRegistry` implemented explicitly. Private helpers (`GetMemberData`, `OnJoinGroup`,
  `OnLeaveGroup`, `OnGroupDisbanded`, `OnGroupMembersUpdated`) became private instance methods
  with bodies unchanged.
- DI wired in `CentralServer.Init`: `AddSingleton<IGroupRegistry, GroupManager>` after
  `ISessionRegistry`; `GroupManager.Instance` overwritten from DI after `builder.Build()`.
- `LobbyServerProtocol` receives `IGroupRegistry groupRegistry` as second constructor
  parameter; stores as `private readonly IGroupRegistry _groupRegistry`; passes it to
  `GroupModule`.
- `GroupModule` receives `IGroupRegistry groupRegistry` as second constructor parameter;
  all 31 `GroupManager.X(...)` static calls replaced with `_groupRegistry.X(...)`.
  Tests (`GroupModuleTest`, `LobbyHandlerRegistrationTest`) updated to pass the new
  constructor arguments using `GroupManager.Instance`.

**Deferred to follow-up steps (after step 2):**
- Other modules (`MatchmakingModule`, `GameLifecycleModule`, `CharacterModule`, `FriendModule`)
  and managers (`MatchmakingManager`, `MatchmakingQueue`, `QueuePenaltyManager`,
  `SessionManager`, `LobbyServerProtocol`, `GroupsTask`, `ChatManager`, `CustomGame`,
  `GameLifecycleModule`, `StatusController`, `DiscordLobbyUtils`) still call
  `GroupManager.X()` statically via the shim.
- `GetGroupInfo`, `UpdateSelectedSubTypes`, `GetMemberData` internally call
  `SessionManager.GetClientConnection(...)` via the static shim for `SelectedGameType`,
  `GetSubTypeMask()`, `IsReady` — those retain the concrete `LobbyServerProtocol` internally.
- Priority order: `SessionManager` ✓ → `GroupManager` ✓ → `ServerManager`/`GameManager` ✓
  → `MatchmakingManager` next.

**Step 3 complete (branch `refactor`):**
- `IServerPool` introduced in `LobbyServer2/BridgeServer/IServerPool.cs` with 8 members:
  `AddServer`, `RemoveServer`, `GetServer`, `IsAnyServerAvailable`, `GetServers`,
  `FindServerByAddress`, `HasOtherServerWithFingerprint`, `DisconnectByFingerprint`.
- `IGameRegistry` introduced in `LobbyServer2/BridgeServer/IGameRegistry.cs` with 9 members:
  `CreatePvpGame`, `RegisterGame`, `UnregisterGame`, `GetGameWithPlayer`, `ReconnectServer`,
  `GetGames`, `GetRunningGamesNum` (two overloads), `StopAllGames`.
- `ServerManager` converted from `static class` to `public class ServerManager : IServerPool`
  with `public static ServerManager Instance { get; internal set; }` pre-initialized to
  `new ServerManager()`. `ServerPool` dict became private instance field `_serverPool`.
  All `public static` methods are static forwarders calling private `*Core` instance methods.
  `IServerPool` implemented explicitly. Private helpers (`GetServersInPickOrder`,
  `IsReserveFilled`, `DisconnectServer`) became private instance methods — bodies unchanged.
  `AddServerCore` still calls `GameManager.ReconnectServer(gameServer)` via static shim.
- `GameManager` converted from `public class` (with static fields) to
  `public class GameManager : IGameRegistry` with `public static GameManager Instance`
  pre-initialized to `new GameManager()`. `Games` ConcurrentDictionary became private
  instance field `_games`. `GameNum` gauge and `GameTypesForStats` stayed `static readonly`
  (Prometheus re-registration concern). The static constructor's `AddBeforeCollectCallback`
  now calls `inst.GetRunningGamesNumCore(...)` via a local `GameManager inst = Instance`
  variable (avoids the static-member-via-instance-reference compiler error). All `public
  static` methods are static forwarders; `IGameRegistry` implemented explicitly.
  `CreatePvpGameCore` still calls `ServerManager.GetServer()` via static shim.
- DI wired in `CentralServer.Init`: `AddSingleton<IServerPool, ServerManager>` and
  `AddSingleton<IGameRegistry, GameManager>` after the `IGroupRegistry` registration.
  `ServerManager.Instance` and `GameManager.Instance` overwritten from DI after
  `builder.Build()` so production uses the DI-managed singletons.
- `LobbyServerProtocol` receives `IGameRegistry gameRegistry` as third constructor
  parameter; stores as `private readonly IGameRegistry _gameRegistry`; passes it to
  `GameLifecycleModule`.
- `GameLifecycleModule` receives `IGameRegistry gameRegistry` as second constructor
  parameter; stores as `private readonly IGameRegistry _gameRegistry`. The one static
  call (`GameManager.GetGameWithPlayer` in `HandlePreviousGameInfoRequest`) replaced with
  `_gameRegistry.GetGameWithPlayer`. Tests updated minimally (pass `GameManager.Instance`
  at the three new-`GameLifecycleModule` call sites and at the `new LobbyServerProtocol`
  call site).

**Deferred callers (still use static shims after step 3):**
- `BridgeServerProtocol`, `CustomGameManager`, `MatchmakingManager`, `MatchmakingQueue`,
  `CustomGame`, `Game` still call `ServerManager.X()` and/or `GameManager.X()` statically.
- `LobbyServerProtocol.HandleDEBUG_AdminSlashCommandNotification` and
  `HandleRejoinGameRequest` still call `GameManager.GetGameWithPlayer` statically (not in
  `GameLifecycleModule`; blocked on Stage 3 service extraction or Stage 4 `DraftController`).
- Priority order: `SessionManager` ✓ → `GroupManager` ✓ → `ServerManager`/`GameManager` ✓
  → `MatchmakingManager` next.

**Step 4 complete (branch `refactor`):**
- `IMatchmakingManager` introduced in
  `LobbyServer2/LobbyServer/Matchmaking/IMatchmakingManager.cs` with 10 members: `Enabled`,
  `GetQueues`, `GetQueue`, `Update`, `AddGroupToQueue`, `RemoveGroupFromQueue`, `IsQueued`,
  `StartPractice`, `StartGameAsync`, `OnGameEnded`.
- `MatchmakingManager` converted from `public static class` to
  `public class MatchmakingManager : IMatchmakingManager` with a
  `public static MatchmakingManager Instance { get; internal set; }` shim pre-initialized
  to `new MatchmakingManager()`. The `Queues` dict and `_enabled`/`_queueUpdateRunning`
  fields became private instance fields; the queue dict initialization moved to the instance
  constructor. `Enabled` retains the existing `public static bool Enabled` property
  (forwarding to `Instance._enabled` with its log-on-change side effect); the interface
  member is implemented explicitly as `bool IMatchmakingManager.Enabled`. All other
  `public static` methods are static forwarders calling private `*Core` instance methods;
  `IMatchmakingManager` implemented explicitly throughout. `AddGroupToQueueCore` and
  `RemoveGroupFromQueueCore` still call `GroupManager.Broadcast/GetGroupSubTypeMask` via
  static shim; `StartGameAsyncCore` still calls `GameManager.CreatePvpGame()` via static shim.
- DI wired in `CentralServer.Init`: `AddSingleton<IMatchmakingManager, MatchmakingManager>`
  after the `IGameRegistry` registration; `MatchmakingManager.Instance` overwritten from DI
  after `builder.Build()` so production uses the DI-managed singleton.
- `LobbyServerProtocol` receives `IMatchmakingManager matchmakingManager` as fourth
  constructor parameter; stores it and passes it to `MatchmakingModule`.
- `MatchmakingModule` receives `IMatchmakingManager matchmakingManager` as second constructor
  parameter. All 5 static `MatchmakingManager.X(...)` call sites replaced with
  `_matchmakingManager.X(...)`: `AddGroupToQueue` (×2), `RemoveGroupFromQueue` (×2),
  `IsQueued` (×1). Tests updated minimally (pass `MatchmakingManager.Instance` at the three
  `new MatchmakingModule` call sites and the `new LobbyServerProtocol` call site in
  `LobbyHandlerRegistrationTest`).

**Deferred callers (still use static shims after step 4):**
- `MatchmakingTask` calls `MatchmakingManager.Update()` statically.
- `CentralServer.PendingShutdown` setter accesses `MatchmakingManager.Enabled` statically.
- `MatchmakingQueue` calls `MatchmakingManager.StartGameAsync(...)` statically.
- `GroupManager.UpdateSelectedSubTypes`, `MatchmakingModule.UpdateGroupReadyState`,
  `MatchmakingModule.SetContextualReadyState` still call `GroupManager.X()` statically via shim.
- All four named managers now converted ✓. Remaining static singletons
  (`CustomGameManager`, `ChatManager`, `AdminManager`, `FriendManager`, `QueuePenaltyManager`,
  `DiscordManager`, `StatsApi`) are future work.
- Priority order: `SessionManager` ✓ → `GroupManager` ✓ → `ServerManager`/`GameManager` ✓
  → `MatchmakingManager` ✓.

**Step 5 complete (branch `refactor`):**
- `PlayerOnlineStatus Status { get; set; }` added to `IClientConnection`
  (initialised to `PlayerOnlineStatus.Online` in both `LobbyServerProtocol` and
  `RecordingClientConnection`). `using CentralServer.LobbyServer.Friend` added to both
  `IClientConnection.cs` and `RecordingClientConnection.cs`.
- `FriendManager.OnPlayerUpdateStatusRequest` parameter widened from
  `LobbyServerProtocol` to `IClientConnection`; internal call `MarkForUpdate(client)` replaced
  with `MarkForUpdate(client.AccountId)`.
- `FriendManager.MarkForUpdate(LobbyServerProtocol)` overload deleted (it only forwarded to
  `MarkForUpdate(long)`); `LobbyServerProtocol.BroadcastRefreshFriendList` updated to call
  `FriendManager.MarkForUpdate(AccountId)` directly.
- `HandlePlayerUpdateStatusRequest` moved from `LobbyServerProtocol` to `FriendModule`;
  `FriendModule` now fully extracted — owns both `FriendUpdateRequest` and
  `PlayerUpdateStatusRequest`.
- Deferred: `FriendManager.GetStatusString(LobbyServerProtocol)` still takes the concrete
  type (reads `IsInGame`, `IsInCharacterSelect`, `IsInQueue`, `IsInGroup` which are not on
  `IClientConnection`); all its call sites already have a concrete `LobbyServerProtocol` from
  `SessionManager.GetClientConnection` and are unaffected.

**Step 6 complete (branch `refactor`):**
- `JoinGame(Game)` and `OnStartGame(Game)` added to `IClientConnection`; stub
  implementations added to `RecordingClientConnection`.
- `Game.ReconnectPlayer` widened from `LobbyServerProtocol` to `IClientConnection`;
  `Game.SendGameInfo` widened from `LobbyServerProtocol` to `IClientConnection`. Both
  methods only use `conn.AccountId`, `conn.Send`, `conn.JoinGame`, and `conn.OnStartGame`
  — all already on the interface. All existing callers (`PvpGame`, `CustomGame`,
  `SendGameInfoNotifications`) pass `LobbyServerProtocol` which implements `IClientConnection`
  and compile unchanged.
- `HandleRejoinGameRequest` moved from `LobbyServerProtocol` to `GameLifecycleModule`:
  registration added in `Register`; handler adapted to use `_conn.X` instead of `this.X`;
  `game.ReconnectPlayer(this)` replaced with `game.ReconnectPlayer(_conn)`.
  Registration line and method body removed from `LobbyServerProtocol`.

**Deferred (still blocked after step 6):**
- `HandleRegisterGame` — blocked on `SessionManager.OnPlayerConnect` field-mutation
  pattern (sets concrete `LobbyServerProtocol` fields directly).
- Chat handlers (`ChatNotification`, `GroupChatRequest`) — blocked on `ChatManager`
  event redesign (`LobbyServerProtocol`-typed event signatures).

### Stage 4 — Split `Game`

- `GameLifecycle` (status transitions, server binding, reconnection),
- `TeamAssembler` (FillTeam/bots/duplicate resolution — pure given inputs; highly testable),
- `DraftController` (ranked resolution state machine, injected clock/timer),
- `MatchFinalizer` (summary → history/Elo/TrustWar/accolades/Discord, behind interfaces).
`Game` becomes a thin aggregate wiring these together.

### Stage 5 — Consistency & hygiene

- Move misnamespaced Framework code to real homes (`LoginManager` → DirectoryServer or an
  `EvoS.Accounts` library); align folder/project/namespace names (`LobbyServer2` →
  `CentralServer`); delete dead code; update README.
- One concurrency idiom per structure (drop `ConcurrentDictionary` where a lock is
  authoritative, or drop the lock where the dictionary suffices); replace `async void`;
  give background tasks a real `CancellationTokenSource` tied to shutdown.
- `IClock` abstraction for penalty/draft/session-expiry logic (Elo already takes `now`).
- Make `PatchAccountData` report whether it changed anything; dedupe the loadout blocks;
  turn it into versioned migrations.
- Migrate remaining configs onto `ReloadableConfig`/one loader; inject config objects
  instead of static getters in new/refactored code.

### Suggested first PRs (small, high leverage)

1. ~~`IClientNotifier` + convert `MatchmakingQueue`/`GroupManager` notification call sites.~~ **Done.**
2. ~~Stage-1 kickoff: merge `LobbyServerProtocolBase` into `LobbyServerProtocol`, add the
   handler-set snapshot test, extract `StoreModule` as the pilot (pure account math,
   tested against mock DAOs).~~ **Done.**
3. Fix `PatchAccountData` return value + dedupe (removes a DB write per login).
4. Deduplicate `SessionManager` lock/concurrent-dictionary idiom, document the invariant.
5. Extract `TeamAssembler` from `Game` with tests around `CheckDuplicatedAndFill`
   (historically bug-prone: dodge/fill edge cases).
