# 03 – Lobby & Sessions

## Responsibilities

Everything a connected game client can do outside an actual match: session registration,
character/loadout updates, store purchases, groups, queueing, chat, custom games, ranked
draft interactions, error/feedback reporting.

## Key classes

| Class | Location | Notes                                                                                                                                                                                                                                                                                                                                                                                  |
|-------|----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `WebSocketBehaviorBase<TMessage>` | `LobbyServer2/WebSocketBehaviorBase.cs` | Generic websocket connection base: receive loop, typed handler registry (`RegisterHandler<T>`), serialized sends with websocket-sharp-compatible fragmentation, per-connection log context. One static `Connections` registry per closed generic type                                                                                                                                  |
| `LobbyServerProtocol` | `LobbyServer2/LobbyServer/LobbyServerProtocol.cs` | Connection identity (`AccountId`, `SessionToken`), `CurrentGame` pointer, transport plumbing (`Send(WebSocketMessage)`, `Broadcast`, per-connection message patching via `ProxyPatcher` and `ProtocolPatcher`). Implements `IClientConnection` and `IHandlerRegistry`; its public surface is exactly `IClientConnection`, and nothing outside `LobbyServer/Session` names the concrete type (`ConcreteConnectionUsageTest`). Registers no handlers itself: every message handler is in one of the `ILobbyModule`s it composes (`Login`, `Chat`, `Store`, `Telemetry`, `Account`, `Group`, `Friend`, `Matchmaking`, `GameLifecycle`, `Draft`, `Character`, `Admin`) |
| `SessionManager` | `LobbyServer2/LobbyServer/Session/SessionManager.cs` | Static session registry: `SessionInfos` (active), `ConnectingSessions` (login issued, WS not yet up, 30s expiry), `DisconnectedSessionInfos` (reconnect window, 10 min). Also Prometheus lobby gauges                                                                                                                                                                                  |
| `AdminManager` | `LobbyServer2/LobbyServer/AdminManager.cs` | Singleton; ban/mute penalty bookkeeping, admin action events, muted-refresh loop                                                                                                                                                                                                                                                                                                       |
| `UsernameRequestManager` | `LobbyServer2/LobbyServer/UsernameRequestManager.cs` | Create account requests (approved via admin API)                                                                                                                                                                                                                                                                                                                                       |
| `CrashReportManager`, `AccoladeUtils`, etc. | `LobbyServer2/LobbyServer/Utils/` | Support utilities                                                                                                                                                                                                                                                                                                                                                                      |

## Session lifecycle

0. **Concurrent login wait**: a fresh login for an account that is still online first waits in
   `DirectoryServer.WaitForPreviousSessionToEnd`, polling `SessionManager.HasActiveSession` (up to
   `ConcurrentLoginWaitPeriod`, 20s). The lobby endpoint pings
   clients (`LobbyServerKeepAliveTimeout`) and aborts ones that stop answering, so a crashed client's
   session ends and the relaunched client gets in; a live second instance is rejected once the wait expires.
1. **CreateSession** (called by DirectoryServer): under `lock (SessionInfos)`; enforces
   single-session-per-account when `rejectIfActive` (fresh logins). Preserves the old
   `SessionToken` when the caller says the player has a running game (`hasLiveGame`; the
   directory server asks `GameManager`) (so the game server still recognizes
   them); always regenerates `ReconnectSessionToken`. Registers into `ConnectingSessions`.
2. **OnPlayerConnect** (from `LoginModule.HandleRegisterGame`): validates the
   session token against `ConnectingSessions`, checks bans (`AdminManager.UpdatePenalties`),
   raises `OnSessionStarting` (the group manager puts the player in a new solo group), then
   moves the session to `SessionInfos`. The event is raised under the session lock before
   the session is registered, so a registered session always has a group.
3. **OnPlayerDisconnect** (from `LobbyServerProtocol.HandleClose`): token-guarded removal
   (reconnects can race the old close event), session parked in `DisconnectedSessionInfos`,
   `LastLogout` persisted. May trigger pending shutdown completion.
4. **Reconnection**: DirectoryServer validates both tokens against the parked session;
   `Game.ReconnectPlayer` re-attaches the connection to a running game.

`OnSessionStarting` is the only session event. `SessionManager` calls no other manager:
`CentralServer` stops the games before `OnServerShutdown` notifies the sessions.

Token generation is weak by design (`GenerateToken` = GUID string hashcode); tokens are
session-scoped, not secrets equivalent to passwords, but the TODO in
`DirectoryServer.HandleReconnection` acknowledges hijack potential.

## LobbyServerProtocol anatomy (the god class)

Handler groups. They were all instance methods of this class; each group now lives in an
`ILobbyModule` (see the table above), and some details below predate that move:

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
- **Ranked draft** (`DraftModule`): hover/ban/selection/trade requests, passed to
  `DraftController.Hover`/`Ban`/`Select`/`Trade` of the current game (doc 04); the module
  only maps results onto responses.
- **Chat**: `HandleChatNotification` → `ChatManager` (via event `OnChatNotification`).
- **Telemetry**: client status/error/feedback/performance reports → DAOs + Discord.
- **Friends**: `HandleFriendUpdate` (~250 lines: add/accept/reject/block/note...).

State pushed to clients goes through composed notifications (`SendGameInfoNotifications`,
`BroadcastRefreshFriendList`, `BroadcastRefreshGroup`, `SendSystemMessage`, ...), often
broadcast by iterating `SessionManager` connections.

## Observations

- The class mixes **transport identity** (a live websocket) with **player domain logic**
  (purchases, friends, draft) and **fan-out orchestration** (broadcasts to other players).
- Cross-links are pervasive: `LobbyServerProtocol` and the managers call each other in both
  directions. The connection and its modules reach the session, group, game and matchmaking
  managers through injected interfaces (`CustomGameManager` and the other managers that are
  still static, statically); the managers reach connections through
  `SessionManager.GetClientConnection`, which returns `IClientConnection`.
- `SessionManager.Broadcast` delegates to *any* live connection's `Broadcast` because the
  broadcast registry lives in the connection base class — a smell worth fixing.
