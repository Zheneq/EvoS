# Plan: Extract GroupModule from LobbyServerProtocol

Fourth module extraction — the first one touching group-related flows. Pattern
references: `StoreModule`, `TelemetryModule`, `AccountModule` in
`LobbyServer2/LobbyServer/`; design in `docs/knowledge-base/12-design-assessment.md`
§C Stage 1. Read all first.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Scope decision (read before coding)

This extraction moves the group **request handlers** only. It deliberately does NOT
move the group/ready **state machinery** — `IsReady`, `RefreshGroup`,
`BroadcastRefreshGroup`, `UpdateGroupReadyState`, and the `OnJoinGroup` /
`OnLeaveGroup` / `OnGroupDisbanded` notifier callbacks all stay on
`LobbyServerProtocol`. Rationale: `IsReady` is written primarily by matchmaking code
(`SetContextualReadyState`, queue join/leave, game start), so its owner will be the
future Matchmaking module, not Group; moving it now would be churn. The module reaches
the state machinery through `IClientConnection`.

Also NOT in scope (different owners): `SetGameSubTypeRequest` (writes
`SelectedSubTypeMask` — Matchmaking), `GroupChatRequest` (Chat module; raises the
`OnGroupChatRequest` event), `GameInvitationRequest` / `GameInviteConfirmationResponse`
(game invites — GameLifecycle).

## Ground rules

1. No behavior change; handler bodies move verbatim. Allowed substitutions only:
   `AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
   `Handle` → `_conn.Handle` (bare property reads only — NOT `request.FriendHandle`,
   `account.Handle`, etc.), `SendSystemMessage(` → `_conn.SendSystemMessage(`,
   `BroadcastRefreshFriendList()` → `_conn.BroadcastRefreshFriendList()`,
   `BroadcastRefreshGroup()` → `_conn.BroadcastRefreshGroup()`,
   `CurrentGame` → `_conn.CurrentGame`, accessibility → `private`.
2. **Cross-connection calls stay verbatim.** The handlers fetch *other* players'
   connections via `SessionManager.GetClientConnection(...)` and call
   `friend.Send(...)`, `friend.IsInGroup()`, `leaderSession.Send(...)`,
   `requester.SendSystemMessage(...)`, `requester.BroadcastRefreshFriendList()`,
   `SetGameType(...)` on them. Keep these exactly as they are — typed as
   `LobbyServerProtocol`, fetched from `SessionManager`. This is pre-existing coupling
   already catalogued in doc 12 §A4 as deferred to Stage 3; relocating it unchanged is
   fine, converting it is not in scope.
3. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` must pass **unchanged**.
4. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), commit with the given
   message (no trailers).
5. Anything off-plan: stop and report.

## Batch 1 — GroupModule + tests

### Grow `IClientConnection` (four members)

```csharp
string Handle { get; }
void SendSystemMessage(LocalizationPayload message);
void BroadcastRefreshFriendList();
void BroadcastRefreshGroup(bool resetReadyState = false);
```

All four already exist as public members on `LobbyServerProtocol` (`Handle` is a
computed property; the rest are methods) — no accessibility changes needed, just the
interface declarations. Default parameter values on interface methods are valid C#.

### New file `LobbyServer2/LobbyServer/Group/GroupModule.cs`

Namespace `CentralServer.LobbyServer.Group` (next to `GroupManager`). Same shape as
previous modules. Move these 8 handlers (all verified: no external callers):

| Message type | Handler | Notes |
|---|---|---|
| `GroupInviteRequest` | `HandleGroupInviteRequest` | cross-conn: `friend`, `leaderSession` — keep verbatim (rule 2) |
| `GroupJoinRequest` | `HandleGroupJoinRequest` | cross-conn: `friend`, `leaderSession` |
| `GroupConfirmationResponse` | `HandleGroupConfirmationResponse` | cross-conn: `requester`; uses `_conn.CurrentGame` + `GameManager` in the active-opponent check |
| `GroupSuggestionResponse` | `HandleGroupSuggestionResponse` | |
| `GroupLeaveRequest` | `HandleGroupLeaveRequest` | |
| `GroupKickRequest` | `HandleGroupKickRequest` | |
| `GroupPromoteRequest` | `HandleGroupPromoteRequest` | uses `_conn.BroadcastRefreshGroup()` |
| `PlayerGroupInfoUpdateRequest` | `HandlePlayerGroupInfoUpdateRequest` | cross-conn `SetGameType` loop — keep verbatim |

Constructor: remove the 8 `RegisterHandler` lines; module array becomes
`{ new StoreModule(this), new TelemetryModule(this), new AccountModule(this), new GroupModule(this) }`.

### Tests

- `RecordingClientConnection`: add `Handle` (settable, default e.g. `"Test#1"`),
  `List<LocalizationPayload> SystemMessages`, `int FriendListRefreshes`,
  `List<bool> GroupRefreshes`, recording implementations for the four new members.
- New `Tests/GroupModuleTest.cs` extends `EvosTest` and **joins
  `[Collection("ClientNotifierSeam")]`** — not because it swaps the notifier, but
  because `GroupManager` state is process-global and the existing
  `GroupManagerNotifierTest` (same collection) also mutates it; serializing them avoids
  cross-talk. Use unique account IDs regardless.
- `GroupManager.CreateGroup(accountId)` is the setup primitive for "player has a
  group". With no sessions registered, the default notifier no-ops — safe.
- Cases (all runnable without live sessions):
  1. `GroupPromoteRequest` while solo: response `Success = false`,
     `LocalizedFailure = GroupMessages.NotInGroupMember`.
  2. `GroupKickRequest` while solo: same failure shape.
  3. `GroupInviteRequest` for an unknown handle: `Success = false`,
     `PlayerNotFound` failure, exactly one response sent.
  4. `GroupJoinRequest` for an unknown handle: `PlayerNotFound` failure.
  5. `GroupLeaveRequest`: player ends up in a solo group
     (`GroupManager.GetPlayerGroup(id).IsSolo()`), `FriendListRefreshes == 1`.
  6. `GroupConfirmationResponse` with an unknown confirmation number while in a solo
     group: one system message recorded on the fake connection (the
     invite-expired path).
  If a case turns out to need infrastructure the test environment can't provide (e.g.
  handle resolution through a DAO the mock doesn't implement), drop that case, note it
  in the report, and keep the rest — don't build scaffolding.

Commit: `Extract GroupModule from LobbyServerProtocol`

## Batch 2 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1: note Group is the fourth
module extracted, and record the state-ownership decision: `IsReady` + refresh/ready
machinery intentionally left on the connection pending the Matchmaking extraction.
Small edit.

Commit: `docs: GroupModule extracted`

## Acceptance criteria

1. The 8 handlers exist only in `GroupModule`; registrations removed; module array
   composes Store + Telemetry + Account + Group.
2. `IsReady`, `RefreshGroup`, `BroadcastRefreshGroup`, `UpdateGroupReadyState`,
   `OnJoinGroup`/`OnLeaveGroup`/`OnGroupDisbanded` are untouched on
   `LobbyServerProtocol`.
3. Snapshot test passes unmodified; full suite green (176 pre-existing + new tests).
4. Changes limited to: `GroupModule.cs` (new), `LobbyServerProtocol.cs`,
   `IClientConnection.cs`, `RecordingClientConnection.cs`, `GroupModuleTest.cs` (new),
   doc 12.
