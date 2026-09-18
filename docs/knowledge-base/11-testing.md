# 11 – Testing

## Infrastructure (`Tests/`)

- xUnit; `Tests/Lib/EvosTest.cs` base class wires log4net output into xUnit's
  `ITestOutputHelper` (with careful once-only `XmlConfigurator.Configure` and appender
  teardown — see the memory note about the formerly flaky teardown; suite failures are
  real regressions now).
- `Tests/Lib/TestAccountHelper.cs` — account fixtures.
- `Tests/DataAccess/DbTestFramework.cs` — harness for DAO tests (Mongo-backed DAOs and
  cached decorators).

## What is covered

| Test | Target | Enabler |
|------|--------|---------|
| `EloTest` | `Elo` | delegate-based providers (`EvosDelegates`) |
| `MatchmakerTest` | `MatchmakerRanked` | ctor-injected `AccountDao` + config `Func` |
| `QueuePenaltyManagerTest`, `QueuePriorityManagerTest` | penalty/priority logic | mock DAO mode |
| `LoginManagerTest` | registration/login rules | mock DAO mode |
| `EvosAuthTest`, `AuthTicketTest`, `GameServerAuthTest`, `GameServerKeyManagerTest` | JWT/ticket/game-server auth | mostly pure crypto/logic |
| `EvosConfigurationTest` | startup validation | internal overloads taking values |
| `LogRedactionTest` | log masking | pure |
| `Tests/DataAccess/*` | Mongo DAOs, cached decorators | `DbTestFramework` |

## What is not covered (and why)

The entire connection-facing core: `LobbyServerProtocol` handlers, `Game` lifecycle,
`SessionManager`, `GroupManager`, `MatchmakingQueue` orchestration, `ChatManager`,
`CustomGameManager`. Root causes:

1. Logic lives on live websocket connection objects (`LobbyServerProtocol : WebSocketBehaviorBase`)
   — you can't construct one meaningfully without a socket.
2. Static managers with process-wide state and cross-calls — no seams to isolate one
   subsystem; state leaks between tests.
3. `DB.Get()` / `EvosConfiguration` / `GameWideData` static access inside business logic.
4. Time (`DateTime.Now/UtcNow`) and `Task.Delay` used directly (draft phase timers,
   server shutdown delays) — untestable without abstraction.

The tested islands (matchmaker, Elo, penalties, auth) demonstrate the house style that
works: constructor-inject DAOs/config with static-default convenience constructors.
The refactoring roadmap (doc 12) generalizes exactly that pattern.
