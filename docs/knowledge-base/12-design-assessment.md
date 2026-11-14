# 12 – Design Assessment & Refactoring Roadmap

Goal: clearer, more modular, more testable. This doc lists the shortcomings observed in
docs 01–11, why they matter, and a staged refactoring plan that respects the constraint
that this is a live, working emulator (wire compatibility with an unmodifiable client and
game server must be preserved).

Last full review: 2026-11-13 (after the practice/tutorial, custom game invite, queue
requirement and multilancer draft features). Per-step logs of the completed work were
removed in that review; they are in the git history of this file.

## A. Status at a glance

| Stage | State | What is left |
|-------|-------|--------------|
| 1 – Handler modules | Done except draft | Four draft handlers (~360 lines) still on `LobbyServerProtocol`; move with Stage 4a |
| 2 – `IClientNotifier` | Done | Two small leaks of the concrete connection type (see B4) |
| 3 – De-static the managers | **Interfaces and shims only** | ~200 static call sites; cycles between managers; several managers not converted at all (see C, Stage 3) |
| 4 – Split `Game` | Not started | `Game.cs` grew from 1,699 to 1,997 lines since the plan was written |
| 5 – Hygiene | `PatchAccountData` and the temp-account fallback done | Everything else |

Tests: 435, all passing. New features mostly came with tests, and the tests show where the
seams are missing: draft and character selection tests subclass `PvpGame(null)` to reach
protected members (`DraftBotPickTest`, `DraftTradeTest`, `CharacterSelectionTest`,
`QueuePenaltyIssueTest`).

## B. Shortcomings

### B1. Static singletons / global state (highest impact)

Five managers (`SessionManager`, `GroupManager`, `ServerManager`, `GameManager`,
`MatchmakingManager`) are instance classes behind interfaces, registered in DI, but each
keeps a static `Instance` and a static forwarder for every method, and that static surface
is what the code uses: ~80 `SessionManager.X`, ~45 `GroupManager.X`, ~40 `GameManager.X`,
~30 `MatchmakingManager.X` call sites. Only `GroupModule`, `MatchmakingModule` and one call
in `GameLifecycleModule` go through an injected interface; `LobbyServerProtocol` receives
`ISessionRegistry` and never uses it.

Not converted at all: `CustomGameManager`, `QueuePenaltyManager`, `QueuePriorityManager`,
`FriendManager`, `GameServerKeyManager`, `PracticeManager`, `TutorialManager` (static
classes); `ChatManager`, `DiscordManager`, `AdminManager`, `StatsApi` (`Get()`
singletons); `ClientNotifier` (static accessor with a test scope); `DB.Get()` (~200 call
sites); `EvosConfiguration`, `LobbyConfiguration` and `GameData` (static-lazy).

Everything added since Stage 3 was written against the statics, including two new static
managers. The half-converted state costs more than either end state: every manager method
is declared four times (interface, static forwarder, explicit implementation, `*Core`),
and the dependency graph is still hidden.

Consequences: state leaks between tests (they share `X.Instance`), hidden dependencies,
initialization-order fragility, and no way to construct a subsystem in isolation.

### B2. God classes

- `Game` (1,997 lines): server binding, roster, team assembly, bot filling, character
  validation, duplicate resolution, the whole draft, dodge handling, queue priority
  compensation, penalties bookkeeping (`PenalizedPlayers`), result finalization,
  Discord/Elo/TrustWar/stats integration. `SoloGame` uses almost none of it and switches
  the rest off through virtual hooks (`ShouldProcessAccolades`, `ShouldSendStats`,
  `GetFinalizeGameDelay`, `LogDodge`).
- Draft is split across two classes: `Game` runs the phase loop, and the four handlers on
  `LobbyServerProtocol` read the draft state out of the game, mutate it and write it back
  (`GetRankedResolutionPhaseData` / `SetRankedResolutionPhaseData`, `UpdatePlayersInDeck`,
  `SkipRankedResolutionSubPhase`). This is the most frequently changed code in the repo.
- `MatchmakingQueue` (791 lines): queue bookkeeping + sub-type mask algebra + match
  scoring orchestration + client notification composition.
- `AdminController` (820 lines, static) after the admin panel rework.
- Growing: `GameLifecycleModule` (644 lines; custom game invites are ~180 of them) and
  `GroupModule` (589).

### B3. Layering violations & naming drift

- Framework hosts app-layer logic under foreign namespaces (`EvoS.DirectoryServer.Account.LoginManager`
  in `EvoS.Framework/DataAccess/`, `InventoryManager`, `CharacterManager`, lobby config
  types in `CentralServer.LobbyServer.Config`) — see doc 08.
- Folder `LobbyServer2` / project `CentralServer` / namespace `CentralServer.LobbyServer`;
  folder `GameMode` / namespace `Gamemode`.
- Two `CharacterManager`s disambiguated by using-aliases.
- `EvoS.DirectoryServer` is a 1-file project whose actual logic lives in Framework.

### B4. Coupling to the concrete connection

Pushing messages goes through `IClientNotifier` now. What remains is code that needs
connection *state* or *behavior* that is not on `IClientConnection`, so it takes
`LobbyServerProtocol`: the static `SessionManager.GetClientConnection` downcasts to it,
and ~35 call sites depend on that (`Game` 8, `GroupModule` 5, `GroupManager` 4,
`CustomGame` 3, Discord 4, others 1–2). `Game.GetClients()` returns
`List<LobbyServerProtocol>`; `InviteRules.CheckInvitee` hands one out. The members they
use: `LeaveGame`, `IsInGame`, `IsInCharacterSelect`, `IsInGroup`, `IsReady`,
`SelectedGameType`, `GetSubTypeMask`, `SetGameType`, `RefreshGroup`, `OnGameAssigned`,
`SendSystemMessage(string)`.

`GroupManager` reading queue selection and ready state from each member's connection is
the deeper issue: that state belongs to the group/session, not to a socket.

### B5. Concurrency

- Draft state has no single guard. The handlers lock on one of the selection dictionaries
  (or nothing, for hover and ban), `GetDraftActingSlot` takes `characterSelectionLock`,
  the phase loop takes no lock (`// TODO: There can be a race condition`).
- `Game.characterSelectionLock` is `public static`: character selection in every game
  serializes on one global lock, shared with `CustomGame`.
- `lock (this)` in `Game.CancelMatch`.
- `lock (SessionInfos)` around a `ConcurrentDictionary`; plain `Dictionary` + `lock` in
  `ServerManager`/`CustomGameManager`/`GroupManager`.
- Sync-over-async (`Send` → `.GetAwaiter().GetResult()`), `async void`
  (`Game.OnServerDisconnect`, 15 in `DiscordManager`), periodic tasks started with
  `Task.Run` and `CancellationToken.None`.
- Time and delays hard-coded (`DateTime.Now`/`UtcNow`, `Task.Delay` in draft timers,
  `PvpGame`/`CustomGame` start flows, `ServerManager.DisconnectServer`).

### B6. Shared mutable cached entities

`AccountDao` (cached) returns shared `PersistedAccountData` instances mutated in place by
many subsystems, then saved wholesale or per-component. No unit of work, no optimistic
concurrency; lost updates are possible when two handlers touch the same account.

### B7. Config sprawl

Five formats/loaders, only one hot-reloadable, all static, all CWD-relative (doc 10).
`GameModeManager` re-reads `Custom.json` on every `GetCustomGameTypeAvailability` call
(i.e. every login) and passes `Bans` through a static `LocalizedNameToBans` side
dictionary because the field is not serialized.

### B8. Smaller items

- `LobbyServerProtocol.SendImpl` looks up the session statically on every outgoing
  message to get the protocol version for `ProtocolPatcher`; the connection should hold it.
- `HandleClose` does cleanup for four subsystems inline (game, custom game invites,
  session, group). `ILobbyModule` still has no `OnDisconnect` hook.
- Dead code: `QuestManager` stub, disabled queue types (`Ranked`, `Custom` queues
  commented out).
- `CustomGame` carries its own near-copies of `CheckIfAllSelected` (shadowed with `new`)
  and `IsCharacterUnavailable`.

## C. What already points the right way (build on these)

- The module pattern: per-connection `ILobbyModule`s composed in the connection
  constructor, tested against `RecordingClientConnection`, with
  `LobbyHandlerRegistrationTest` locking the set of handled message types. New handlers
  have been landing in modules without prompting.
- `IClientNotifier` + `RecordingClientNotifier`.
- `CustomGameInvites`: a small instance class with its own lock and its own tests. This is
  the shape new stateful code should have (it only needs to stop hanging off a static).
- `CharacterRules`, `InviteRules.ResolveInvitee`, `Game.GetDraftPhases`,
  `Game.PickCharacter(teammates, unavailable, forBot)`: rules extracted as pure functions.
- `ProtocolPatcher`: outbound patching as a table of pure functions that never mutate
  shared objects.
- `MatchmakerRanked(AccountDao, ..., Func<Config>)`, `Elo` parameterized by providers.
- `BridgeServerProtocol` ↔ `Game` decoupled via events; `IGameServerConnection`.
- DAO interface / impl / cached-decorator / mock structure; `PeriodicRunner`,
  `ReloadableConfig`.

## D. How the new features fit

| Feature | Where it landed | Fit | Follow-up |
|---------|-----------------|-----|-----------|
| Practice, Tutorial (`SoloGame`) | `Game` subclass + static `PracticeManager`/`TutorialManager` + static `SoloGame.Start` | Reuses lifecycle well; exposes that base `Game` carries PvP-only behavior | Stage 4 (composition), Stage 3 (game factory replaces the static managers) |
| Custom game invites | `CustomGameInvites` + handlers in `GameLifecycleModule` | Good | Own module once `CustomGameManager` is an instance (Stage 3) |
| Group invite rework, `InviteRules` | `GroupModule`, shared static rules | Good | Stop returning `LobbyServerProtocol` (B4) |
| Multilancer / procedural draft, bot picks | `Game` + handlers on the connection | Worst fit: grew both halves of the split draft | Stage 4a, first in line |
| Queue requirements, `ProtocolPatcher` | Framework types + patch step in `SendImpl` | Good | B8 (cache protocol version) |
| Asymmetric sub types | `AsymmetricSubType`, `MatchmakingQueue`, `GameModeManager` | OK; adds to `MatchmakingQueue` and config sprawl | Stage 5 |
| Queue penalties (leave time, pardons) | Static `QueuePenaltyManager`, state on `Game.PenalizedPlayers` | Well tested through `TestGame`; state lives in the wrong class | Stage 4c |
| Admin panel | `AdminController` | Works; one 820-line static class | Split by area when controllers move to DI (Stage 3) |

## E. Roadmap

Decisions taken on 2026-11-13:

1. **Stage 3 goes all the way**: constructor injection everywhere, then the static
   `Instance`s, forwarders and `*Core` methods are deleted. No ambient singletons as an
   end state.
2. **Draft becomes a `DraftController`** owned by the game, with a thin `DraftModule` on
   the connection. Code is moved, not rewritten into an event-emitting state machine.
3. **The `Game` hierarchy stays** (`PvpGame`, `CustomGame`, `SoloGame` are the start-flow
   scripts); shared behavior moves into components that only the subclasses that need them
   create.

Order: 4a (draft) → 3a–3c (DI foundation and managers) → 4b–4d with 3d (rest of `Game`,
game factory) → 3e–3f (hosts, delete shims) → 5. Draft goes first because it is the most
actively changed code and needs almost nothing from the managers. Every step is one
reviewable change with no behavior change unless stated.

Step-by-step plans for the first fifteen actions (Stage 4a, then Stage 3a, 3b and 3c
through the matchmaking manager) are in [`docs/plans/`](../plans/), numbered in
execution order.

Decisions taken on 2026-11-14:

4. **`TimeProvider`** is the clock abstraction for converted classes; no project-defined
   `IClock`.
5. **Queue selection state is split by owner**: game type and per-member ready state on
   `GroupInfo`; each player's requested sub type mask kept per player for the life of the
   session, because the client never re-sends it after leaving a group (plan 11).
6. **`QueuePenaltyManager` is converted as is**; splitting account-level blocks from
   game-level policy waits for Stage 4c.

### Stage 4a — `DraftController` (also finishes Stage 1)

- `DraftController` in `BridgeServer/` owns `RankedResolutionPhaseData`, the phase list,
  the deck, sub-phase timing, bot picks and auto-lock on timeout. Created by `PvpGame` and
  `CustomGame` only when the sub type has `RankedFreelancerSelection`; `Game.Draft` is
  null otherwise.
- The request side becomes methods on it — `Hover`, `Ban`, `Select`, `Trade` — taking
  the account id and request data and returning success or a `LocalizationPayload`
  failure. The handlers move to a `DraftModule` that only resolves `CurrentGame?.Draft`,
  calls the method and sends the response.
- One private per-game lock guards all draft state, on both the request side and the
  phase loop. This is the one intended behavior change: it closes the acknowledged race.
  `GetRankedResolutionPhaseData`/`SetRankedResolutionPhaseData`, `UpdatePlayersInDeck`
  and `PlayersInDeck` stop being public.
- What the controller needs from the game goes through a narrow interface (roster lookup,
  send per-player snapshot, cancel match, connectivity check), so tests construct a
  controller with a fake instead of subclassing `PvpGame`.
- Time goes through an injected `TimeProvider` (default `TimeProvider.System`), which
  lets tests run a whole draft without waiting.
- `DraftPhasesTest`, `DraftBotPickTest`, `DraftTradeTest` move onto the controller; add
  tests for the four request methods (wrong phase, not on deck, taken character, double
  lock-in, fill roles, trade offer/accept/reject, trade with a bot).

### Stage 3 — Finish dependency injection

Target dependency direction (arrows point at what a class may take in its constructor):

```
Session ← ClientNotifier ← Group ← QueuePenalty/Priority ← Matchmaking
ServerPool ← GameRegistry ← GameFactory          Matchmaking → GameRegistry
modules, API controllers, periodic tasks → any of the above
```

Signals in the other direction are events, not references.

**3a. Composition root.** One `AddLobbyServices(IServiceCollection)` used by
`CentralServer.Init`. The admin and user API servers build their own `WebApplication`s,
so they register the same singleton *instances* from the main container. In `Tests/Lib`,
a `TestLobby` helper builds a fresh graph per test (mock DAOs, `RecordingClientNotifier`),
replacing the shared `X.Instance` and `ClientNotifierScope`.

**3b. Break the cycles** (each is a small change on its own):

| Cycle | Today | Break |
|-------|-------|-------|
| Server ↔ Game | `ServerManager.AddServer` calls `GameManager.ReconnectServer` | `IServerPool.ServerAdded` event; the game registry subscribes |
| Session ↔ Group | `SessionManager` creates the solo group on connect; `GroupManager` reads connections | Group subscribes to the existing `OnPlayerConnected` event |
| Group ↔ Matchmaking | `GroupManager` removes groups from the queue | Group raises a membership-changed event; matchmaking subscribes |
| Penalty ↔ Matchmaking | `QueuePenaltyManager` removes the group from the queue | The penalty service raises a "queue block applied" event (alongside the existing `OnPenalty`/`OnPardon` report events); matchmaking subscribes. Done in 3c with the penalty service conversion, not in 3b: a static event would be handled by two generations of instances during startup |
| Session → Game | `CreateSession` asks `GameManager` whether to keep the token; shutdown calls `StopAllGames` | Caller passes the "has live game" answer in; shutdown orchestration moves to `CentralServer` |

**3c. Convert consumers, bottom-up.** For each class: add constructor parameters for
what it uses, replace static calls, update tests to `TestLobby`. Order:

1. Remaining modules and `LobbyServerProtocol` itself (`LoginModule`, `ChatModule`,
   `FriendModule`, `CharacterModule`, `AdminModule`, the static leftovers in
   `MatchmakingModule`/`GameLifecycleModule`/`GroupModule`).
2. `IClientConnection` grows the members listed in B4 so that nothing needs the concrete
   type; queue selection and ready state move from `MatchmakingModule` to group/session
   state that `GroupManager` owns, which removes its reads of member connections. Then
   `GetClientConnection` returns `IClientConnection` everywhere and the delegating
   properties on `LobbyServerProtocol` go away.
3. `ClientNotifier` becomes an injected instance.
4. `GroupManager`, then `QueuePenaltyManager`/`QueuePriorityManager` (static → instance),
   then `MatchmakingManager` with `MatchmakingQueue`.
5. `ServerManager`, `GameManager`, `GameServerKeyManager`, `BridgeServerProtocol`
   (resolved from DI like `LobbyServerProtocol`).
6. `CustomGameManager` (static → instance, owning `CustomGameInvites`); custom game
   handlers split out of `GameLifecycleModule` into a `CustomGameModule`.
7. `ChatManager`, `FriendManager`, `AdminManager`, `StatsApi`, `TrustWarManager`,
   `DiscordManager` and its helpers.

**3d. Game family** (together with Stage 4): `Game` takes one `GameServices` bundle
(registries, notifier, penalties, DAOs, `TimeProvider`) because the hierarchy is deep and tests
subclass it; the components extracted in Stage 4 take only the narrow dependencies they
use. An `IGameFactory` creates `PvpGame`, `CustomGame`, `PracticeGame` and
`TutorialGame`; `PracticeManager`, `TutorialManager` and `SoloGame.Start` fold into a
`SoloGameLauncher` service that uses it.

**3e. Hosts.** API controllers become classes with injected dependencies
(`AdminController` split by area: players, games, servers, moderation). The four periodic
tasks become hosted services with a cancellation token tied to shutdown.

**3f. Delete the shims.** Remove static forwarders and `Instance`, rename `*Core` back,
drop the explicit interface implementations. Prometheus metric objects stay static
(they cannot be registered twice).

Scope notes:
- `DB.Get()`: a class converted in 3c/3d takes the DAOs it uses through its constructor.
  Static helpers that remain static (`LobbyServerUtils.GetHandle` and the like) keep
  calling `DB.Get()` until Stage 5.
- Static configuration getters stay until Stage 5.
- Pure rule classes (`CharacterRules`, `InviteRules.ResolveInvitee`, `ProtocolPatcher`,
  `AccoladeUtils`) stay static.

### Stage 4b–4d — Rest of the `Game` split

- **4b. `TeamAssembler` + `CharacterSelection`.** `FillTeam`, `AddBot`, `PickCharacter`,
  controlling-player resolution → `TeamAssembler` (pure given roster inputs and an account
  lookup). `UpdateCharacterInfo`, `ValidateSelectedCharacter`, `CheckDuplicatedAndFill`,
  `CheckIfAllSelected`, `AssignRandomCharacter*` → `CharacterSelection` with a per-game
  lock replacing the static `characterSelectionLock`. `CustomGame`'s shadowed copies
  become a rule parameter. Created by `PvpGame`/`CustomGame` only.
- **4c. `MatchCancellation`.** `CancelMatch`, connectivity checks, dodge logging, queue
  priority compensation and the `PenalizedPlayers` state, behind the penalty service.
- **4d. `MatchFinalizer`.** `OnGameEnded` + `FinalizeGame` (history, Elo, accolades,
  TrustWar, stats, Discord, delays) driven by a `FinalizationPolicy` (accolades on/off,
  stats on/off, GG delay, dodge reporting) that each subclass supplies, replacing the
  virtual hooks.
- What stays in `Game`: server binding and bridge events, roster queries, status,
  game info building and sending, reconnection.
- The start flows (`PvpGame.StartGameAsync`, `CustomGame.StartCustomGameAsync`,
  `SoloGame.Launch`) stay in the subclasses and read as scripts over the components.

### Stage 5 — Consistency & hygiene

- `ILobbyModule.OnDisconnect`; `HandleClose` cleanup moves to the owning modules.
- Connection holds its protocol version (B8).
- `TimeProvider` in the remaining time-dependent code (session expiry, group request
  expiry, start-flow delays); replace `async void`.
- One concurrency idiom per structure; document the `SessionManager` invariant.
- Configs: cache game sub types, make `Bans` a real field of the server-side sub type
  model, migrate loaders onto `ReloadableConfig`, inject config objects.
- Move misnamespaced Framework code to real homes; align folder/project/namespace names
  (`LobbyServer2` → `CentralServer`, `Gamemode` → `GameMode`); delete dead code.
- `PatchAccountData` → versioned migrations.
- Account mutation discipline (B6): at least per-account serialization of handlers.
- Split `MatchmakingQueue` (bookkeeping / mask algebra / notification composition).

## F. Rules for new code until the roadmap is done

- New stateful logic is an instance class with constructor parameters, registered in
  `CentralServer.Init`; no new static managers and no new `Get()` singletons.
- New handlers go into a module; modules take interfaces, not `X.Instance`.
- Do not hand out or accept `LobbyServerProtocol` outside `LobbyServer/Session`; grow
  `IClientConnection` or use `IClientNotifier`.
- New draft or character-selection rules are written as pure functions with their own
  tests (as `GetDraftPhases` and `PickCharacter` were), so they move cleanly in Stage 4.

## G. Decisions from completed stages still worth knowing

- Modules that need other modules get them through their constructor from the composition
  root (`CharacterModule` takes `MatchmakingModule` and `GameLifecycleModule`), not
  through `IClientConnection`.
- `Status` (`PlayerOnlineStatus`) and the game callbacks (`OnStartGame`, `OnGameAssigned`,
  `SendGameUnassignmentNotification`) deliberately live on the connection, not in
  `GameLifecycleModule`.
- `IClientNotifier` methods are no-ops for offline accounts, mirroring the old
  `?.Send(...)` call sites.
- `IHandlerRegistry.Register` throws on a duplicate message type; two modules cannot
  claim the same message.
- Manager `Instance`s are pre-initialized so that tests work without `CentralServer.Init`;
  this is what `TestLobby` (3a) replaces.
