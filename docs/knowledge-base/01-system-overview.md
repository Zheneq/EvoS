# 01 – System Overview

## Projects (solution `EvoS.sln`)

| Project | Folder | Kind | Role |
|---------|--------|------|------|
| `EvoS.Sandbox` | `EvoS.Sandbox/` | console exe | Composition root. Starts DirectoryServer + CentralServer in one process (`Program.cs`) |
| `EvoS.DirectoryServer` | `EvoS.DirectoryServer/` | library-ish exe | HTTP login endpoint (`DirectoryServer.cs`, single file) |
| `CentralServer` | `LobbyServer2/` | library-ish exe | Lobby, bridge, matchmaking, social, APIs — the bulk of the logic (~100 files) |
| `EvoS.Framework` | `EvoS.Framework/` | library | Wire protocol, persistence, auth, game data, shared utilities (~540 files, mostly generated/ported message & data classes) |
| `Tests` | `Tests/` | xUnit | Unit tests for the testable islands |
| — | `evos.admin/` | React app | Admin web UI (MUI + react-auth-kit) consuming the admin REST API |
| — | `dll/` | binaries | Game DLL dependencies referenced by the C# projects |

Note the naming drift: folder `LobbyServer2`, project file `CentralServer.csproj`, root
namespace `CentralServer`, but the subsystem folder inside is again `LobbyServer`.

## Process topology

```
                            ┌──────────────────────────────────────────────┐
                            │  EvoS.Sandbox process                        │
 Game client ── HTTP ─────► │  DirectoryServer     :6050  (login)          │
 Game client ── WS ───────► │  CentralServer       :6060                   │
                            │    /LobbyGameClientSessionManager (clients)  │
 Game server ── WS ───────► │    /BridgeServer                (servers)    │
 Admin UI    ── HTTP ─────► │  AdminApiServer      :3001                   │
 Website/etc ── HTTP ─────► │  UserApiServer       :3002                   │
 Prometheus  ── HTTP ─────► │  metrics             :1234                   │
                            └──────────────┬───────────────────────────────┘
                                           │
                                        MongoDB (or in-memory mocks)
```

Game-server processes are *external* unmodified `AtlasReactor.exe` instances (configured via
`GameServerExecutable` in settings.yaml, or self-registering over `/BridgeServer` with
key-based auth). `AtlasReactorConfig.json` in the repo root is the config for the *game
client/server*, pointing it at `DirectoryServerAddress 127.0.0.1:6050`.

## Startup sequence (`EvoS.Sandbox/Program.cs`)

1. Configure log4net, validate `EvosConfiguration` and `LoginManager` config (fail fast).
2. `DB.Get()` — force DB/DAO initialization.
3. `CentralServer.Init()` (`LobbyServer2/CentralServer.cs`):
   - ASP.NET Core `WebApplication` with two websocket-accepting routes.
   - Eagerly instantiates singletons: `ChatManager`, `DiscordManager`, `StatsApi`, `AdminManager`.
   - Spawns background `PeriodicRunner` tasks: `FriendsTask`, `GroupsTask`, `MatchmakingTask`,
     `ServerStatisticsTask`.
   - Starts `AdminApiServer` and `UserApiServer` (separate Kestrel hosts).
4. DirectoryServer started on its own thread (its own Kestrel `WebHost`).
5. `CentralServer.MainLoop()` blocks on the app task; shutdown cascades through
   `PendingShutdownType` states (see `CentralServer.PendingShutdown`).

`Program.OnExecute` is `async void` — exceptions escape the sync context and the
CommandLineUtils lifecycle; see [12 – Design assessment](12-design-assessment.md).

## Client connection flow (happy path)

1. Client POSTs `AssignGameClientRequest` to DirectoryServer → auth (password / auth ticket /
   reconnection ticket) → `SessionManager.CreateSession` registers a *connecting* session →
   response carries lobby address + session tokens.
2. Client opens websocket to `/LobbyGameClientSessionManager` → `LobbyServerProtocol` →
   `RegisterGameClientRequest` → `SessionManager.OnPlayerConnect` promotes the connecting
   session to an active one, creates a solo group.
3. Client interacts via ~75 registered message handlers (`LobbyServerProtocol` ctor).
4. Queueing → matchmaking → `Game` orchestration over the bridge (docs 04–05).

## Deployment

- `Dockerfile`: self-contained single-file linux-x64 publish of `EvoS.Sandbox`
  (not trimmed — `EvosSerializer` discovers message types via reflection).
- `.github/workflows/docker-lobby.yml`: builds and pushes `ghcr.io/zheneq/evos` on `v*` tags.
