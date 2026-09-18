# Plan: Continue IClientNotifier rollout

Goal: migrate the remaining *pure notification* call sites from
`SessionManager.GetClientConnection(id)?.X(...)` to `ClientNotifier.Get().X(id, ...)`,
without any behavior change. This continues the work described in
`docs/knowledge-base/12-design-assessment.md` §A4 (Stage 2 of the roadmap).

## Existing infrastructure (already merged — do not recreate)

- `LobbyServer2/LobbyServer/Session/IClientNotifier.cs` — the outbound port interface.
- `LobbyServer2/LobbyServer/Session/ClientNotifier.cs` — static access point
  (`Get`/`Set`/`Reset`) + default impl `SessionManagerClientNotifier` (thin delegation to
  `SessionManager.GetClientConnection`; every method is a no-op for offline accounts).
- `Tests/Lib/RecordingClientNotifier.cs` — recording fake + `ClientNotifierScope`.
- `Tests/GroupManagerNotifierTest.cs` — example tests; note the
  `[Collection("ClientNotifierSeam")]` requirement for any test that swaps the notifier.

Reference conversions: see how `GroupManager.cs`, `MatchmakingManager.cs`,
`MatchmakingQueue.cs`, `QueuePenaltyManager.cs` use `ClientNotifier.Get()` today.

## Ground rules

1. **No behavior change.** Every migrated site must keep its exact semantics. The
   `?.` null-conditional maps to the notifier's built-in "no-op when offline". Do NOT
   add null checks where there were none, and do NOT remove semantically meaningful
   null-check branches (logging, `continue`, error paths) — express them via `IsOnline`.
2. **Only migrate the sites listed in this plan.** The "Deferred" list at the bottom is
   deliberate — those sites read state off the connection, mutate it, or pass the
   connection object to helpers. They belong to later roadmap stages. Leave them alone
   even if they look easy.
3. Line numbers below are from the commit this plan was written at; locate sites by the
   quoted code pattern, not by line number.
4. After each batch: `dotnet build EvoS.sln` and `dotnet test Tests/Tests.csproj` must
   pass (149+ tests, 0 failures). Commit each batch separately (messages suggested per
   batch).
5. Remove `using CentralServer.LobbyServer.Session;`-adjacent dead usings only if the
   compiler flags them; don't do unrelated cleanup.

---

## Batch 1 — extend the interface

Two capabilities are needed by the sites below and are missing from `IClientNotifier`:

1. `void SendSystemMessage(long accountId, string text);` — overload delegating to
   `LobbyServerProtocolBase.SendSystemMessage(string)`. Needed by `MapPickBanSession`.
2. `void RefreshFriendList(long accountId);` — delegates to
   `LobbyServerProtocol.RefreshFriendList()`, which **immediately sends** the friend
   status notification. This is distinct from the existing `MarkFriendListForUpdate`
   (which defers via `FriendManager.MarkForUpdate`). Document that distinction in the
   XML doc comment on the interface.

Update in the same commit:
- `SessionManagerClientNotifier` — add the two delegating implementations.
- `RecordingClientNotifier` — record both (e.g. `SystemMessageTexts` as
  `List<(long AccountId, string Text)>`, `FriendListRefreshes` as `List<long>`).

Commit: `Add SendSystemMessage(string) and RefreshFriendList to IClientNotifier`

## Batch 2 — simple manager/util files (11 sites)

All are pure sends; each converts to a single `ClientNotifier.Get().X(accountId, ...)` line.

**`LobbyServer2/LobbyServer/Chat/ChatManager.cs`** — 3 sites:
- `SessionManager.GetClientConnection(player)?.Send(message);` (in the global-chat loop,
  behind `if (!isBlocked)`)
- `SessionManager.GetClientConnection(accountId)?.Send(message);` (before
  `ChatHistoryDao.Save`)
- `SessionManager.GetClientConnection(recipientAccountId)?.Send(message);` (before the
  other `ChatHistoryDao.Save`)

**`LobbyServer2/LobbyServer/TrustWar/TrustWarManager.cs`** — 2 sites:
- `session?.Send(notification)` loop over `notificationsToSend` → inline to
  `ClientNotifier.Get().Send(notification.AccountID, notification);` (drop the local).
- `player?.Send(new FactionCompetitionNotification ...)` loop over online players →
  `ClientNotifier.Get().Send(playerAccountId, new FactionCompetitionNotification ...);`

**`LobbyServer2/LobbyServer/Chat/MapPickBanSession.cs`** — 1 site (uses Batch 1 overload):
- `SessionManager.GetClientConnection(memberId)?.SendSystemMessage(text);` →
  `ClientNotifier.Get().SendSystemMessage(memberId, text);`

**`LobbyServer2/LobbyServer/Utils/CrashReportManager.cs`** — 1 site:
- In `ProcessClientErrorSummary`: `conn ??= SessionManager.GetClientConnection(accountId);
  conn?.Send(new ErrorReportSummaryRequest ...);` → delete the `LobbyServerProtocol conn`
  local entirely and call `ClientNotifier.Get().Send(accountId, new ErrorReportSummaryRequest
  { CrashReportHash = stackTraceHash });` inside the `if (entry is null)` block. (The
  `??=` was only memoizing the lookup; per-iteration lookup is equivalent.)

**`LobbyServer2/LobbyServer/Friend/FriendsTask.cs`** — 1 site (uses Batch 1 method):
- `SessionManager.GetClientConnection(accId)?.RefreshFriendList();` →
  `ClientNotifier.Get().RefreshFriendList(accId);`

**`LobbyServer2/LobbyServer/Friend/FriendManager.cs`** — 2 sites (uses Batch 1 method):
- The pair `SessionManager.GetClientConnection(accountA.AccountId)?.RefreshFriendList();`
  / same for `accountB` → `ClientNotifier.Get().RefreshFriendList(...)`.
- NOTE: the *other* `GetClientConnection` in this file (inside the `FriendInfo`
  dictionary builder, where `conn` feeds `IsOnline = conn != null` and status fields) is
  **deferred** — do not touch.

Commit: `Migrate chat, trust war, friend, and crash report notifications to IClientNotifier`

## Batch 3 — game classes, pure sends only (6 sites)

**`LobbyServer2/LobbyServer/CustomGames/CustomGame.cs`** — 4 sites:
- `LobbyServerProtocol client = ...; if (client != null) { SendGameAssignmentNotification(groupAccountId); }`
  → `if (ClientNotifier.Get().IsOnline(groupAccountId)) { SendGameAssignmentNotification(groupAccountId); }`
  (the null check only gates a call that takes the account id, not the connection).
- `SessionManager.GetClientConnection(accountId)?.Send(notification);` (in
  `SendGameAssignmentNotification(long accountId)`).
- `SessionManager.GetClientConnection(data.AccountId)?.Send(notification);` (in the
  `SendGameAssignmentNotification(MatchPlayerData ...)` override).
- `playerConnection?.Send(new GameAssignmentNotification { ... GameResult.ClientKicked ... })`
  → `ClientNotifier.Get().Send(player.AccountId, new GameAssignmentNotification ...)`
  (drop the local).
- Do NOT touch: the `playerConnection?.CurrentGame == this` / `LeaveGame(this)` sites
  and `DisperseCustomGame` (they mutate connection state — deferred), nor the
  commented-out block near `usedFillCharacters`.

**`LobbyServer2/BridgeServer/Game.cs`** — 2 sites:
- `SessionManager.GetClientConnection(data.AccountId)?.Send(notification);` (end of the
  game-assignment notification method).
- The `EnterFreelancerResolutionPhaseNotification` loop:
  `playerConnection != null` check + `playerConnection.Send(...)` →
  `ClientNotifier.Get().Send(player, new EnterFreelancerResolutionPhaseNotification ...)`
  (the null check had no side effect — silent skip — so plain notifier call is identical).
- Do NOT touch the other 7 `GetClientConnection` sites in this file (they collect
  connection objects, pass them to helpers like `SendGameInfo`/`NotifyCharacterChange`/
  `SetPlayerReady`/`GetStatusString`, or read `IsConnected`/`CurrentGame`; one of them
  logs an error and skips domain logic on null — all deferred to Stage 4).

Commit: `Migrate CustomGame and Game pure notification sites to IClientNotifier`

## Batch 4 — docs

Update `docs/knowledge-base/12-design-assessment.md` §A4 "Partially fixed" paragraph:
- Add the newly migrated files/counts (Batches 2–3: 17 sites).
- Replace the "~44 sites in other files are follow-up PRs" sentence with an accurate
  statement of what remains and why, e.g.: remaining `GetClientConnection` sites are
  read-then-use or connection-mutating and are deferred — `LobbyServerProtocol` (10,
  Stage 1 service extraction), `Game.cs` (7, Stage 4 split), `CustomGame.cs` (3, Stage
  3/4), `GroupManager` (4), `AdminManager` (1, `CloseConnection` = session control),
  `StatusController` (2), Discord classes (4), `FriendManager` (1) — Stage 3
  `ISessionRegistry`.
- Verify the counts with
  `grep -rn "GetClientConnection" LobbyServer2 --include=*.cs` before writing them.

Commit: `docs: update IClientNotifier rollout status`

---

## Deferred sites — DO NOT MIGRATE (for reference)

| File | Site pattern | Why deferred |
|---|---|---|
| `LobbyServerProtocol.cs` (all 10) | various | The class *is* a connection; handlers will move to services in Stage 1. Two sites (`leaderSession.Send(...)` in suggestion/join paths) have **no null check** — converting would silently turn a potential NRE into a no-op, i.e. a behavior change. Leave them. |
| `Game.cs` (7 remaining) | conn collected into lists / passed to `SendGameInfo`, `NotifyCharacterChange`, `SetPlayerReady`, `FriendManager.GetStatusString`; reads `IsConnected`, `CurrentGame`; error-log-and-skip null branches | Stage 4 (`Game` split) |
| `CustomGame.cs` (3 remaining) | `playerConnection?.CurrentGame == this`, `LeaveGame(this)`, `DisperseCustomGame` | Mutates connection state |
| `GroupManager.cs` (4) | `GetMemberData`, `GetGroupInfo` ×2, `UpdateSelectedSubTypes` | Read connection state; Stage 3 `ISessionRegistry` |
| `AdminManager.cs` (1) | `conn.CloseConnection()` with else-branch logging | Session control, not notification |
| `StatusController.cs` (2) | `FriendManager.GetStatusString(GetClientConnection(...))` | State read |
| `DiscordManager.cs` (1), `DiscordBotWrapper.cs` (1), `DiscordLobbyUtils.cs` (2) | read `CurrentGame`, `IsInQueue`, `IsInGame` | State read |
| `FriendManager.cs` (1) | `FriendInfo` builder using `conn` for `IsOnline`/status | State read |

## Acceptance criteria

1. `dotnet build EvoS.sln` — 0 errors, 0 new warnings.
2. `dotnet test Tests/Tests.csproj` — all tests pass.
3. `grep -c GetClientConnection` per file matches: `ChatManager` 0, `TrustWarManager` 0,
   `MapPickBanSession` 0, `CrashReportManager` 0, `FriendsTask` 0, `FriendManager` 1,
   `CustomGame` 3 (+1 in a comment), `Game.cs` 7, and the deferred files unchanged.
4. No test class outside `[Collection("ClientNotifierSeam")]` calls `ClientNotifier.Set`.
   (New tests are optional for this mechanical migration; if you add any that swap the
   notifier, they must join that collection.)
5. Diff contains only the changes described here — no drive-by refactoring.
