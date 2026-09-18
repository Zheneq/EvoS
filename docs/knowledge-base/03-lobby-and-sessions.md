# 03 – Lobby & Sessions

## Responsibilities

Everything a connected game client can do outside an actual match: session registration,
character/loadout updates, store purchases, groups, queueing, chat, custom games, ranked
draft interactions, error/feedback reporting.

## Key classes

| Class | Location | Notes |
|-------|----------|-------|
| `WebSocketBehaviorBase<TMessage>` | `LobbyServer2/WebSocketBehaviorBase.cs` | Generic websocket connection base: receive loop, typed handler registry (`RegisterHandler<T>`), serialized sends with websocket-sharp-compatible fragmentation, per-connection log context. One static `Connections` registry per closed generic type |
| `LobbyServerProtocolBase` | `LobbyServer2/LobbyServer/LobbyServerProtocolBase.cs` | Client-flavored base: `Send(WebSocketMessage)`, `Broadcast`, error responses, `SendLobbyServerReadyNotification` (assembles the massive initial state dump, fetches GitHub patch notes) |
| `LobbyServerProtocol` | `LobbyServer2/LobbyServer/LobbyServerProtocol.cs` | **2,896 lines, ~75 message handlers.** Connection identity (`AccountId`, `SessionToken`), `CurrentGame` pointer, and business logic for a dozen domains inline |
| `SessionManager` | `LobbyServer2/LobbyServer/Session/SessionManager.cs` | Static session registry: `SessionInfos` (active), `ConnectingSessions` (login issued, WS not yet up, 30s expiry), `DisconnectedSessionInfos` (reconnect window, 10 min). Also Prometheus lobby gauges |
| `AdminManager` | `LobbyServer2/LobbyServer/AdminManager.cs` | Singleton; ban/mute penalty bookkeeping, admin action events, muted-refresh loop |
| `UsernameRequestManager` | `LobbyServer2/LobbyServer/UsernameRequestManager.cs` | Username change requests (approved via admin API) |
| `CrashReportManager`, `AccoladeUtils`, etc. | `LobbyServer2/LobbyServer/Utils/` | Support utilities |

## Session lifecycle

1. **CreateSession** (called by DirectoryServer): under `lock (SessionInfos)`; enforces
   single-session-per-account when `rejectIfActive` (fresh logins). Preserves the old
   `SessionToken` when the player has a running game (so the game server still recognizes
   them); always regenerates `ReconnectSessionToken`. Registers into `ConnectingSessions`.
2. **OnPlayerConnect** (from `HandleRegisterGame` in `LobbyServerProtocol`): validates the
   session token against `ConnectingSessions`, checks bans (`AdminManager.UpdatePenalties`),
   creates a solo group, moves the session to `SessionInfos`, fires `OnPlayerConnected` event.
3. **OnPlayerDisconnect**: token-guarded removal (reconnects can race the old close event),
   session parked in `DisconnectedSessionInfos`, `LastLogout` persisted. Fires
   `OnPlayerDisconnected`. May trigger pending shutdown completion.
4. **Reconnection**: DirectoryServer validates both tokens against the parked session;
   `Game.ReconnectPlayer` re-attaches the connection to a running game.

Events `OnPlayerConnected`/`OnPlayerDisconnected` are consumed by `ChatManager`,
`FriendManager`, `CustomGameManager` etc. — the one place where the design is
properly event-driven.

Token generation is weak by design (`GenerateToken` = GUID string hashcode); tokens are
session-scoped, not secrets equivalent to passwords, but the TODO in
`DirectoryServer.HandleReconnection` acknowledges hijack potential.

## LobbyServerProtocol anatomy (the god class)

Handler groups registered in the constructor, all implemented as instance methods on the
same class:

- **Session/bootstrap**: `HandleRegisterGame`, options, keybinds, region.
- **Account state**: `HandlePlayerInfoUpdateRequest` (character select/loadouts — with a
  `characterSelectionLock` shared with `Game`), `HandleSelectBanner/Title/Ribbon`, dev tag.
- **Store**: ~12 `HandlePurchase*` handlers (banners, mods, taunts, vfx, loadout slots...)
  — each mutates `PersistedAccountData`, saves via DAO, replies.
- **Groups**: invite/join/leave/kick/promote/confirmation — heavy logic here (e.g.
  `HandleGroupInviteRequest` is ~150 lines), delegating partially to `GroupManager`.
- **Queue**: join/leave matchmaking queue, ready-state (`SetContextualReadyState` — an
  important state machine deciding queue join vs custom-game ready vs game load).
- **Game**: create/join custom games, rejoin, leave, spectate, game invitations.
- **Ranked draft**: trade/selection/ban/hover handlers manipulating
  `Game.RankedResolutionPhaseData` under locks.
- **Chat**: `HandleChatNotification` → `ChatManager` (via event `OnChatNotification`).
- **Telemetry**: client status/error/feedback/performance reports → DAOs + Discord.
- **Friends**: `HandleFriendUpdate` (~250 lines: add/accept/reject/block/note...).

State pushed to clients goes through composed notifications (`SendGameInfoNotifications`,
`BroadcastRefreshFriendList`, `BroadcastRefreshGroup`, `SendSystemMessage`, ...), often
broadcast by iterating `SessionManager` connections.

## Observations

- The class mixes **transport identity** (a live websocket) with **player domain logic**
  (purchases, friends, draft) and **fan-out orchestration** (broadcasts to other players).
- Cross-links are pervasive: `LobbyServerProtocol` ↔ static managers
  (`GroupManager`, `MatchmakingManager`, `SessionManager`, `CustomGameManager`) call each
  other in both directions.
- `SessionManager.Broadcast` delegates to *any* live connection's `Broadcast` because the
  broadcast registry lives in the connection base class — a smell worth fixing.
