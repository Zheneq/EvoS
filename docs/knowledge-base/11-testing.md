# 11 – Testing

## Infrastructure (`Tests/`)

- xUnit; `Tests/Lib/EvosTest.cs` base class wires log4net output into xUnit's
  `ITestOutputHelper` (with careful once-only `XmlConfigurator.Configure` and appender
  teardown — see the memory note about the formerly flaky teardown; suite failures are
  real regressions now).
- `Tests/Lib/TestAccountHelper.cs` — account fixtures.
- `Tests/Lib/RecordingClientConnection.cs` — fake `IClientConnection` that records what a
  module sends; what the module tests are built on.
- `Tests/Lib/TestLobby.cs` — the one place tests construct connections (`Connect`), lobby
  modules (one factory per module) and `LobbyServerProtocol` (`Protocol`), and reach the
  managers (`Sessions`, `Groups`, `Servers`, `Games`, `Matchmaking`). A constructor
  change touches this file, not the tests. Test classes keep one per test
  (`private readonly TestLobby _lobby = new();`). For now its services are the shared
  static `X.Instance`s; it becomes a fresh graph per test as the managers are converted
  (doc 12, Stage 3c).
- `Tests/Lib/RecordingClientNotifier.cs` — fake `IClientNotifier`, installed for the
  duration of a test with `ClientNotifierScope`.
- `Tests/Lib/FakeDraftHost.cs` — fake `IDraftHost`: a roster to fill in, settable game
  state, and a record of draft snapshots and match cancellations. A `DraftController` is
  built directly over it, without a `Game`. Tests put a controller in a sub-phase with
  `OpenSubPhaseForTest` and read its state with `GetStateForTest` (a copy taken under the
  draft's lock).
- `Tests/DataAccess/DbTestFramework.cs` — harness for DAO tests (Mongo-backed DAOs and
  cached decorators).

## What is covered

| Test | Target | Enabler |
|------|--------|---------|
| `StoreModuleTest`, `AccountModuleTest`, `TelemetryModuleTest`, `FriendModuleTest`, `GroupModuleTest`, `MatchmakingModuleTest`, `CharacterModuleTest`, `GameLifecycleModuleTest` | lobby handler modules (doc 03) | module from `TestLobby` over a `RecordingClientConnection`, mock DAO mode |
| `LobbyHandlerRegistrationTest` | the set of message types the lobby handles (wire contract snapshot) | `TestLobby.Protocol()`: `LobbyServerProtocol` constructed without a socket |
| `ConcreteConnectionUsageTest` | no field, property, parameter or return type outside `LobbyServer/Session` mentions `LobbyServerProtocol` (doc 12, B4); locals are not visible to it | reflection over the `CentralServer` assembly, with a fixture type that proves each kind of mention is found |
| `InviteRulesTest` | checks shared by group and custom game invites | static rules, `RecordingClientConnection` |
| `CustomGameInvitesTest` | pending invite bookkeeping | plain instance class |
| `CustomGameManagerTest` | joining as invitee / spectator fallback, invite cleanup | static manager, `ClientNotifierScope` |
| `PracticeManagerTest` | character checks before a practice game starts | static manager, mock DAO mode |
| `DraftPhasesTest` | draft phase order for regular, no-ban and multilancer rosters | pure static `DraftController.GetDraftPhases` |
| `DraftBotPickTest`, `DraftTradeTest` | bot picks on deck, trading slot resolution | `DraftController` over `FakeDraftHost` with hand-built roster and draft state |
| `DraftControllerTest` | sub-phase timing (full timeout for humans, 5 s for bots, lock-in and match cancellation end the wait, time left in snapshots); `Hover`/`Ban`/`Select`/`Trade` cases; full runs (everyone locks in, only bots, no bans, `ControlAllBots`, AFK pick, hover auto-locked, disconnect, unknown bot); concurrency: requests after a sub-phase closed (the draft held between sub-phases by a `FakeDraftHost` hook), two lock-ins of one character at once, hovers while the draft moves on; a few tests pin current behavior that may be wrong (see doc 12, Stage 4a) | `DraftController` over `FakeDraftHost` and a `FakeTimeProvider` that counts the draft's waits, so a test knows when the draft has set up the next sub-phase |
| `DraftModuleTest` | draft requests: response type and content per result, not drafting, wrong phase | module from `TestLobby` over `RecordingClientConnection` + `TestGame : PvpGame(null)` with a controller attached (the module needs a `Game` for `CurrentGame`) |
| `CharacterSelectionTest` | `CharacterRules`, character updates per game status | `TestGame : PvpGame(null)` |
| `QueuePenaltyManagerTest`, `QueuePriorityManagerTest` | penalty/priority logic | mock DAO mode |
| `GameRegistryReconnectTest` | a reconnecting game server re-attaches to its game; first connections and unknown process codes leave games alone | fresh `ServerManager` + `GameManager(pool)` pair, `BridgeServerProtocol` without a socket |
| `QueuePenaltyIssueTest` | when leavers are penalized and pardoned | `TestGame : Game` driven through protected members (`OnStatusUpdate`, `CancelMatch`, setters), mock DAO mode, `ClientNotifierScope` |
| `TrustWarManagerTest` | who earns points (proxies, bots) | `TestGame : PvpGame`, `ClientNotifierScope` |
| `GroupManagerNotifierTest` | group broadcast fan-out | `ClientNotifierScope` |
| `GroupMembersChangedTest` | `OnMembersChanged` raised once per join (and once for the group left), leave (including the one that disbands the group) and promotion; a queued group leaves the queue on each, and a promotion resets the ready state | global `GroupManager`/`MatchmakingManager` through `TestLobby`, `ClientNotifierScope` |
| `SessionManagerTest` | `HasActiveSession`; login: solo group, `OnSessionStarting` before the session is registered, no error logged, relogin leaves the previous group; `CreateSession` keeps the token only with a live game | own instance for `HasActiveSession`; the global instance (`TestLobby.Sessions`) otherwise, because the global `GroupManager` listens to its `OnSessionStarting` |
| `EloTest` | `Elo` | delegate-based providers (`EvosDelegates`) |
| `MatchmakerTest` | `MatchmakerRanked` | ctor-injected `AccountDao` + config `Func` |
| `AsymmetricSubTypeTest`, `GameSubTypeJsonTest`, `QueueRequirementMinClientVersionTest`, `QueueRequirementProtocolDependentTest` | sub type config loading, queue requirements | pure / JSON round trips |
| `ProtocolPatcherTest` | every message carrying requirements is patched; shared objects are not mutated | pure, reflection over message types |
| `LoginManagerTest`, `PatchAccountDataTest`, `InventoryManagerTest` | registration/login rules, account patching on login, unlock lists | mock DAO mode |
| `EvosAuthTest`, `AuthTicketTest`, `GameServerAuthTest`, `GameServerKeyManagerTest` | JWT/ticket/game-server auth | mostly pure crypto/logic |
| `EvosConfigurationTest` | startup validation | internal overloads taking values |
| `LogRedactionTest` | log masking | pure |
| `Tests/DataAccess/*` | Mongo DAOs, cached decorators | `DbTestFramework` |

## What is not covered (and why)

- A game start run end to end: the start flows (`PvpGame.StartGameAsync`,
  `CustomGame.StartCustomGameAsync`) wait on real `Task.Delay`s.
- `Game` team assembly, duplicate resolution, cancellation and finalization, beyond what
  the penalty tests reach.
- `MatchmakingQueue` orchestration, `ChatManager`, `ServerManager`/`GameManager` beyond
  server reconnection,
  `BridgeServerProtocol`, the API controllers, Discord.
- `SessionManager` and `GroupManager` beyond the narrow cases above.

Root causes:

1. Static managers with process-wide state and cross-calls. Tests reach them through
   `X.Instance`, which every test in the process shares, so state can leak between tests
   and a subsystem cannot be built in isolation.
2. `Game` is reachable only by subclassing it and driving protected members, with a null
   server.
3. `DB.Get()` / `EvosConfiguration` / `GameWideData` static access inside business logic.
4. Time (`DateTime.Now/UtcNow`) and `Task.Delay` used directly (start flows, server
   shutdown delays; the draft is on `TimeProvider` now).

Doc 12 addresses these in order: `DraftController` with an injected delay (Stage 4a),
constructor injection everywhere with a per-test `TestLobby` graph replacing the shared
`Instance`s (Stage 3), and the rest of the `Game` split (Stage 4b–4d).
