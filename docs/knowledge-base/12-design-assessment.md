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

- `LobbyServerProtocol` (2,896 lines, ~75 handlers): store purchases, friends, groups,
  draft, telemetry, chat — all as methods on a websocket connection. Domain logic is
  unreachable without a socket, and the class is a merge-conflict magnet.
  The `LobbyServerProtocolBase` split is nominal: `LobbyServerProtocol` is its only
  subclass, the base is not abstract, all its state fields are public, and it mixes
  transport plumbing (serialization, proxy patching) with domain logic
  (`SendLobbyServerReadyNotification` composes the login state dump, fetches GitHub patch
  notes over HTTP, reads MOTD from the DB). The real transport abstraction is
  `WebSocketBehaviorBase<TMessage>`; the middle layer earns nothing.
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
   - `IClientConnection` (AccountId, Send, ... — grown only as modules need it),
     implemented by `LobbyServerProtocol`. Modules never see the concrete class.
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
   tested (`TelemetryModuleTest`, 4 cases) — second module after Store.**

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
