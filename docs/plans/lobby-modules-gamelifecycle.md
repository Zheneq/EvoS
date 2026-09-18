# Plan: Extract GameLifecycleModule from LobbyServerProtocol (second state migration)

Sixth module extraction, following the state-migration recipe proven by
`MatchmakingModule` (commits `79b6971`/`bcf3901`): module owns the state, the
connection keeps thin delegators with today's signatures/accessibility so external
callers have zero diff. Read `MatchmakingModule.cs`, `LobbyServerProtocol.cs`, and
`docs/knowledge-base/12-design-assessment.md` §C Stage 1 first.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Scope decision

`GameLifecycleModule` takes ownership of `CurrentGame` (including its side-effecting
setter), the `JoinGame`/`LeaveGame` mutators, the derived reads
(`IsInGame`, `IsInCharacterSelect`, `PlayerInfo`), and 11 game handlers.

Explicitly NOT in scope:
- `HandleSubscribeToCustomGamesRequest` / `HandleUnsubscribeFromCustomGamesRequest` —
  they pass `this` (concrete `LobbyServerProtocol`) to `CustomGameManager`; stay on the
  connection until Stage 3 gives CustomGameManager an interface.
- `HandleRejoinGameRequest` — passes `this` to `game.ReconnectPlayer`; same reason.
- The 4 ranked draft handlers (`RankedTradeRequest`, `RankedSelectionRequest`,
  `RankedBanRequest`, `RankedHoverClickRequest`) — Stage 4 `DraftController` territory.
- `OnStartGame` / `OnGameAssigned` / `SendGameUnassignmentNotification` — callbacks
  invoked by `Game`/`PvpGame`/`CustomGame`/`GameManager` on concrete connections; they
  stay on the connection (they don't own state).
- `Status` (`PlayerOnlineStatus`) — friend-status concern, untouched.
- `HandleRegisterGame`, `HandlePlayerInfoUpdateRequest`, chat handlers — later modules.
- **Chat module is deferred entirely** (record in docs batch): its two handlers are
  one-line event raises, and `ChatManager` subscribes to those events per connection
  with `LobbyServerProtocol`-typed signatures and reads `conn.PlayerInfo` — extracting
  it means redesigning that coupling for no handler-logic gain. Revisit at Stage 3.

## Ground rules

1. No behavior change. Verbatim moves; substitutions listed per batch.
   Cross-connection calls stay exactly as-is (notably
   `SessionManager.GetClientConnection(groupMember)?.JoinGame(game)` in
   `HandleCreateGameRequest` — `JoinGame` remains public on the connection as a
   delegator, so this compiles unchanged).
2. External files must have **zero diff**: `Game.cs`, `PvpGame.cs`, `CustomGame.cs`,
   `GameManager.cs`, `FriendManager.cs`, `ChatManager.cs`, `GroupManager.cs`,
   `GroupModule.cs`, `MatchmakingModule.cs`, Discord classes. If one fails to compile,
   the delegators are wrong — fix the delegators.
3. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` passes **unchanged** in every
   batch.
4. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), commit with the given
   message (no trailers).
5. Anything off-plan: stop and report. Drop-and-report clause applies to tests.

## Batch 1 — module owns CurrentGame; connection delegates

### Grow `IClientConnection` (two members)

```csharp
void ResetReadyState();                  // JoinGame/CreateGame handlers call it
void SendGameUnassignmentNotification(); // LeaveGame handler calls it
```

- `ResetReadyState` on `LobbyServerProtocol` is currently a `private` delegator to
  `_matchmaking` — make it `public` (verified: it's a two-statement semantic that
  external exposure doesn't change).
- `SendGameUnassignmentNotification` is already `public` (GameManager calls it).

### New file `LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`

Namespace `CentralServer.LobbyServer.GameLifecycle`. `ILobbyModule`, ctor takes
`IClientConnection`, own `log`; `Register` empty in this batch.

Move from `LobbyServerProtocol`, bodies verbatim with the standard substitutions
(`AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
`BroadcastRefreshFriendList()` → `_conn.BroadcastRefreshFriendList()`,
`BroadcastRefreshGroup()` → `_conn.BroadcastRefreshGroup()`; state references stay
bare):

- The `_currentGame` backing field and the `CurrentGame` property **including its
  side-effecting setter** (on change: refresh friend list + group). Keep the setter
  `private` on the module; all mutation goes through `JoinGame`/`LeaveGame`.
- `public void JoinGame(Game game)` and `public bool LeaveGame(Game game)` (the
  latter's `ForcedCharacterChangeFromServerNotification` send included).
- Derived reads: `IsInGame()`, `IsInCharacterSelect()`, `PlayerInfo`.

### `LobbyServerProtocol` delegators

```csharp
public Game CurrentGame => _gameLifecycle.CurrentGame;
public void JoinGame(Game game) => _gameLifecycle.JoinGame(game);
public bool LeaveGame(Game game) => _gameLifecycle.LeaveGame(game);
public bool IsInGame() => _gameLifecycle.IsInGame();
public bool IsInCharacterSelect() => _gameLifecycle.IsInCharacterSelect();
public LobbyPlayerInfo PlayerInfo => _gameLifecycle.PlayerInfo;
```

Constructor: `_gameLifecycle = new GameLifecycleModule(this);` before the modules
array; array gains it. `HandleClose`'s `CurrentGame?.OnPlayerDisconnectedFromLobby(...)`
and all other internal reads compile against the delegator — do not rewrite them.

Tests (this batch), new `Tests/GameLifecycleModuleTest.cs`,
`[Collection("ClientNotifierSeam")]`, unique AccountIds:
1. Fresh module: `CurrentGame` null, `IsInGame()` false, `PlayerInfo` null.
2. `LeaveGame(null)` returns false (null-server log path), no sends.
3. `LeaveGame(someGame)` when not in a game — can't construct a `Game` easily
   (abstract, heavy deps); if no cheap way exists, drop this case and note it.

Commit: `Move CurrentGame ownership into GameLifecycleModule`

## Batch 2 — move the 11 handlers

Move verbatim (same substitutions; `ResetReadyState()` → `_conn.ResetReadyState()`,
`SendGameUnassignmentNotification()` → `_conn.SendGameUnassignmentNotification()`;
`JoinGame(`/`LeaveGame(`/`CurrentGame` stay bare — module-own) and register:

| Message type | Handler | Notes |
|---|---|---|
| `JoinGameRequest` | `HandleJoinGameRequest` | `_conn.ResetReadyState()` |
| `CreateGameRequest` | `HandleCreateGameRequest` | cross-conn `JoinGame` loop stays verbatim |
| `GameInfoUpdateRequest` | `HandleGameInfoUpdateRequest` | |
| `BalancedTeamRequest` | `HandleBalancedTeamRequest` | |
| `LeaveGameRequest` | `HandleLeaveGameRequest` | `LeaveGame(game)` is module-own |
| `PreviousGameInfoRequest` | `HandlePreviousGameInfoRequest` | |
| `GameInvitationRequest` | `HandleGameInvitationRequest` | stub |
| `GameInviteConfirmationResponse` | `HandleGameInviteConfirmationResponse` | empty |
| `RankedLeaderboardOverviewRequest` | `HandleRankedLeaderboardOverviewRequest` | stub |
| `CalculateFreelancerStatsRequest` | `HandleCalculateFreelancerStatsRequest` | stub |
| `PlayerPanelUpdatedNotification` | `HandlePlayerPanelUpdatedNotification` | empty |

All verified: no callers outside registrations → `private` in the module. Remove the
11 `RegisterHandler` lines. **Snapshot unchanged.**

Tests added (RecordingClientConnection grows `ResetReadyStateCalls` counter and a
recording `SendGameUnassignmentNotification`):
4. `BalancedTeamRequest` with no game: one `BalancedTeamResponse`, `Success = false`.
5. `PreviousGameInfoRequest` with no game: one response, `PreviousGameInfo == null`.
6. `LeaveGameRequest` with no current game: `LeaveGameResponse` (Success = true) +
   `GameStatusNotification` (Stopped) + one `SendGameUnassignmentNotification` call —
   in that order.
7. `JoinGameRequest` for an unknown process code: failed `JoinGameResponse` with the
   failure payload; `ResetReadyStateCalls == 1`.
8. Stubs: `GameInvitationRequest`, `RankedLeaderboardOverviewRequest`,
   `CalculateFreelancerStatsRequest` each send exactly one failed response;
   `GameInviteConfirmationResponse` and `PlayerPanelUpdatedNotification` no-throw.

Commit: `Move game lifecycle handlers into GameLifecycleModule`

## Batch 3 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1:
- Record GameLifecycle as the sixth module / second state migration (`CurrentGame`
  moved; `Status` and the game callbacks deliberately left).
- Record the Chat deferral decision and rationale (event coupling with `ChatManager`;
  revisit at Stage 3).
- Note what remains on the connection for Stage 1: `HandleRegisterGame` + login pile,
  `HandlePlayerInfoUpdateRequest`, chat/draft/custom-game-subscription handlers.

Commit: `docs: GameLifecycleModule extracted; Chat deferred`

## Acceptance criteria

1. `LobbyServerProtocol` has no `_currentGame` field and no game handler bodies; grep
   for `CurrentGame =` (assignment) in the file returns nothing.
2. Zero diff in the rule-2 external files.
3. Snapshot test unmodified in every batch; full suite green (190 pre-existing + new).
4. Changes limited to: `GameLifecycleModule.cs` (new), `LobbyServerProtocol.cs`,
   `IClientConnection.cs`, `RecordingClientConnection.cs`,
   `GameLifecycleModuleTest.cs` (new), doc 12.
