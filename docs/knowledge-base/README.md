# EvoS Knowledge Base

EvoS is a server emulator for **Atlas Reactor** (a discontinued 4v4 turn-based PvP game).
The original game client and the original game-server binary are unmodified; EvoS re-implements
the *backend* services they talked to: login, lobby, matchmaking, social features, store, and
orchestration of game-server processes.

This knowledge base is organized by **subsystem** (which mostly, but not exactly, follows the
folder structure). Each document describes responsibilities, key classes with file references,
main flows, and dependencies. The final document assesses design shortcomings and sketches a
refactoring roadmap.

## Index

| Doc | Subsystem | Primary code location |
|-----|-----------|----------------------|
| [01 – System overview](01-system-overview.md) | Processes, projects, deployment | whole repo |
| [02 – Authentication & Directory](02-authentication-and-directory.md) | Login, registration, tickets | `EvoS.DirectoryServer`, `EvoS.Framework/Auth`, `EvoS.Framework/DataAccess/LoginManager.cs` |
| [03 – Lobby & Sessions](03-lobby-and-sessions.md) | Client websocket protocol, session lifecycle | `LobbyServer2/LobbyServer` |
| [04 – Game lifecycle](04-game-lifecycle.md) | Game servers, game orchestration | `LobbyServer2/BridgeServer`, `LobbyServer2/LobbyServer/CustomGames` |
| [05 – Matchmaking](05-matchmaking.md) | Queues, matchmakers, Elo, penalties | `LobbyServer2/LobbyServer/Matchmaking` |
| [06 – Social systems](06-social-systems.md) | Groups, friends, chat, Discord | `LobbyServer2/LobbyServer/{Group,Friend,Chat,Discord}` |
| [07 – Data access](07-data-access.md) | DAOs, MongoDB, caching, mocks | `EvoS.Framework/DataAccess` |
| [08 – Framework & wire protocol](08-framework-and-protocol.md) | Serialization, message types, game data | `EvoS.Framework/{Network,GameData,Constants,Misc}` |
| [09 – HTTP APIs & admin](09-http-apis-and-admin.md) | REST APIs, admin web UI | `LobbyServer2/ApiServer`, `evos.admin` |
| [10 – Configuration](10-configuration.md) | settings.yaml, reloadable configs | `EvoS.Framework/EvosConfiguration.cs`, `LobbyServer2/Config` |
| [11 – Testing](11-testing.md) | Test infrastructure and coverage | `Tests` |
| [12 – Design assessment](12-design-assessment.md) | Shortcomings & refactoring roadmap | — |

## One-paragraph architecture

A single process (`EvoS.Sandbox`) hosts everything: the **DirectoryServer** (an HTTP JSON
endpoint the game client hits first to authenticate and learn the lobby address), and the
**CentralServer** (assembly name for the `LobbyServer2` project), which serves two websocket
endpoints — `/LobbyGameClientSessionManager` for game clients and `/BridgeServer` for game-server
processes — plus two REST APIs (admin, user) and Prometheus metrics. State lives in MongoDB (or
in-memory mocks when no DB is configured). Actual gameplay runs in separate, unmodified
Atlas Reactor server processes that connect back over the bridge websocket; the lobby assigns
players to them, relays lifecycle events, and persists results.
