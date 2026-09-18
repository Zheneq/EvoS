# 04 – Game Lifecycle (Bridge & Games)

## Responsibilities

Manage the pool of external game-server processes, launch matches on them, track match
state, handle disconnect/reconnect/dodge, and finalize results (stats, Elo, Discord posts).

## Key classes

| Class | Location | Notes |
|-------|----------|-------|
| `BridgeServerProtocol` | `LobbyServer2/BridgeServer/BridgeServerProtocol.cs` | Websocket connection *to one game-server process*. Auth challenge on open, registration, message relay. Exposes **events** (`OnGameEnded`, `OnStatusUpdate`, `OnPlayerDisconnect`, `OnServerDisconnect`, `OnGameMetricsUpdate`) |
| `BridgeMessageSerializer` | `LobbyServer2/BridgeServer/BridgeMessageSerializer.cs` | Binary (de)serialization + message-type table for bridge messages (`Messages/` folder) |
| `GameServerKeyManager` | `LobbyServer2/BridgeServer/GameServerKeyManager.cs` | Public-key fingerprint approval workflow (approved/pending/declined/revoked) backed by `GameServerKeyDao`; pending servers wait for admin approval |
| `ServerManager` | `LobbyServer2/BridgeServer/ServerManager.cs` | Static pool of registered servers; pick order (random/alphabetical) from config; reserve-for-game; delayed shutdown after game end |
| `GameManager` | `LobbyServer2/BridgeServer/GameManager.cs` | Static registry `processCode → Game`; creates `PvpGame`s; reconnect-server rebinding; Prometheus game gauges; pending-shutdown completion |
| `Game` (abstract) | `LobbyServer2/BridgeServer/Game.cs` | **1,699 lines.** Match orchestration: team fill (humans + bots), character validation/duplicates, ready state, launch, status transitions, dodge handling & queue-priority compensation, ranked draft phase state machine (`HandleRankedResolutionPhase`), reconnection, finalize (stats/Elo/TrustWar/Discord) |
| `PvpGame` | `LobbyServer2/BridgeServer/PvpGame.cs` | Matchmade game: fill teams, pick map, assign players, launch |
| `CustomGame` | `LobbyServer2/LobbyServer/CustomGames/CustomGame.cs` | Player-created game: owner, team management via `GameInfoUpdateRequest`, starts when owner launches |
| `CustomGameManager` | `LobbyServer2/LobbyServer/CustomGames/CustomGameManager.cs` | Static registry of custom games + subscriber list for the custom-game browser |

## Game-server registration (bridge handshake)

1. Server connects to `/BridgeServer`; `HandleOpen` sends a nonce challenge
   (`ServerAuthChallengeNotification`).
2. `RegisterGameServerRequest` carries a public key + signature over the nonce;
   `GameServerAuth.VerifySignature` gates registration.
3. Key fingerprint status decides: Approved → `ServerManager.AddServer`; Pending → held in
   `GameServerKeyManager` until an admin approves (admin API / UI); Declined/Revoked → reject.
4. Reconnecting servers (same process code) re-bind to their existing `Game` via
   `GameManager.ReconnectServer`.

## Match lifecycle (PvP)

1. `MatchmakingManager.StartGameAsync` → `GameManager.CreatePvpGame` (reserves a server) →
   `PvpGame.StartGameAsync`.
2. `FillTeam`: converts `MatchPlayerData` into `LobbyServerPlayerInfo`s, adds bots where
   configured, validates connectivity; `CheckDuplicatedAndFill` resolves duplicate/unavailable
   characters (forced randoms); optional ranked draft (`HandleRankedResolutionPhase` — a
   multi-sub-phase async state machine with timers, trades, bans).
3. `BuildGameInfo` + `AssignServer`: subscribes `Game` methods to the server connection's
   events. `SendGameAssignmentNotification` to clients; when all load,
   `BridgeServerProtocol.LaunchGame` sends `LaunchGameRequest` with team/session info.
4. During play: status notifications (`OnStatusUpdate` → `SetGameStatus`), metrics, player
   disconnect notifications (dodge penalties via `QueuePenaltyManager`, queue-priority
   grants to innocents when a match is cancelled).
5. End: `ServerGameSummaryNotification` → `OnGameEnded` → `FinalizeGame`: persists match
   history, updates Elo (`MatchmakingManager.OnGameEnded` → queue → `Elo`), Trust War
   contributions, accolades/badges, Discord game log; clients get unassigned;
   `ServerManager.DisconnectServer` shuts the server down after configured delays.

## Reconnection

`SessionManager.CreateSession` preserves the session token when `GameManager.GetGameWithPlayer`
finds a live game; `Game.ReconnectPlayer` re-attaches the new connection,
`BridgeServerProtocol.StartGameForReconnection` tells the game server the new session id.

## Observations

- The bridge side is the **best-factored** area: connection ↔ game decoupled via events,
  `IGameServerConnection` interface exists, auth is clean. But `Game` itself is a god class
  aggregating team assembly, draft UX, penalty policy, and result persistence.
- `ServerManager`/`GameManager`/`CustomGameManager` are static with ad-hoc locking
  (`lock(ServerPool)` around a plain `Dictionary`, `ConcurrentDictionary` elsewhere).
- `ReserveForGame` has an acknowledged leak (`// TODO release if game did not start?`).
