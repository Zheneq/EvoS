# Plan: Extract FriendModule from LobbyServerProtocol (eighth module)

Eighth module extraction. Stateless — no state migration needed. `HandlePlayerUpdateStatusRequest`
deliberately left on the connection (it calls `FriendManager.OnPlayerUpdateStatusRequest(this, request)`
where `this` is typed `LobbyServerProtocol`; `FriendManager` reads/writes `client.Status` on the
concrete type — moving it without growing `IClientConnection` with `Status` requires Stage 3 manager
refactoring). Read `LobbyServerProtocol.cs`, `FriendManager.cs`, and
`docs/knowledge-base/12-design-assessment.md` §C Stage 1 first.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Verbatim moves with the substitutions listed below.
2. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` passes **unchanged**.
3. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), commit with the given
   message (no trailers).
4. **Verify the deletion side**: after moving, grep `LobbyServerProtocol.cs` for
   `HandleFriendUpdate` and `Unblock` → zero hits expected. Confirm the protocol-file
   deleted-line count in `git show --stat` is consistent with the moved body sizes
   (~265 lines). (A prior extraction left dead copies behind; do not repeat that.)
5. Anything off-plan: stop and report; drop-and-report clause applies to tests.

## Batch 1 — FriendModule + tests

### New file `LobbyServer2/LobbyServer/Friend/FriendModule.cs`

Namespace `CentralServer.LobbyServer.Friend` (folder exists, holds `FriendManager.cs`).
Shape:

```csharp
public class FriendModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FriendModule));
    private readonly IClientConnection _conn;

    public FriendModule(IClientConnection conn) { _conn = conn; }
    public void Register(IHandlerRegistry registry) { /* 1 registration */ }
}
```

Move these members verbatim with the substitutions below:

| Member | Notes |
|---|---|
| `HandleFriendUpdate` (handler for `FriendUpdateRequest`) | Standard substitutions; `RefreshFriendList()` → `_conn.Send(FriendManager.GetFriendStatusNotification(_conn.AccountId))` |
| `Unblock` (private helper) | Standard substitutions; same `RefreshFriendList()` inline |

Standard substitutions: `AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
`SendSystemMessage(` → `_conn.SendSystemMessage(`, accessibility unchanged (handler
`private`, helper `private`).

**Do NOT move:**
- `HandlePlayerUpdateStatusRequest` — stays on the connection; requires `FriendManager.OnPlayerUpdateStatusRequest(this, request)` with the concrete `LobbyServerProtocol` type (accesses `client.Status`)
- `Status` field (`PlayerOnlineStatus`) — friend-adjacent but connection-level presence state; leave for Stage 3

### `LobbyServerProtocol` changes

- Remove the 1 `RegisterHandler<FriendUpdateRequest>(...)` line.
- Remove the `HandleFriendUpdate` method body.
- Remove the `Unblock` method body.
- Add `new FriendModule(this)` to the modules array (order: after `GroupModule`, before `_matchmaking`).
- Zero other changes. `HandlePlayerUpdateStatusRequest`, `RefreshFriendList()`, and `Status` remain unchanged on the connection.

### Tests

New `Tests/FriendModuleTest.cs`, `[Collection("ClientNotifierSeam")]` (uses `FriendManager`
which triggers friend list updates), unique AccountIds.

Setup: two accounts created via `DB.Get().AccountDao.GetAccount(accountId)` pattern (same as other
tests) — ensure accounts exist before the handler is invoked. Use `GroupManager.CreateGroup` for
each account.

Test cases:

1. `FriendUpdateRequest` with unknown `FriendHandle` and `FriendAccountId = 0`:
   one `FriendUpdateResponse` sent with a localized error (PlayerNotFound path), nothing else.
2. `FriendOperation.Add` from AccountA targeting AccountB (handle resolves, no prior relationship):
   `FriendUpdateResponse` success (no error), no sends from the `FriendModule` connection itself
   beyond the response (the RefreshFriendList side effects go to both parties via `ClientNotifier`
   — do not assert on those from this connection).
3. `FriendOperation.Remove` when AccountA and AccountB are not friends:
   `FriendUpdateResponse` with a `NotFriendsWithPlayer` localized failure.
4. `FriendOperation.Block` from AccountA targeting AccountB:
   `FriendUpdateResponse` success + a `FriendStatusNotification` (RefreshFriendList) sent to
   the same connection — in that order.
5. Self-operation: `FriendOperation.Add` targeting own `AccountId`:
   `FriendUpdateResponse` with `CannotFriendYourself` failure.

Commit: `Extract FriendModule from LobbyServerProtocol`

## Batch 2 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1:
- Record FriendModule as the eighth extraction; note it is stateless (no state migration).
- Note `HandlePlayerUpdateStatusRequest` and `Status` deliberately left on the connection
  (requires `FriendManager` to accept `IClientConnection` — Stage 3 concern).
- Update the "what remains on the connection" list: remove `HandleFriendUpdate`; keep
  `HandleRegisterGame`, `HandlePlayerUpdateStatusRequest`, chat events, overcon/GG pack,
  custom-game subscribe/rejoin, ranked draft handlers, `DEBUG_AdminSlashCommandNotification`.

Commit: `docs: FriendModule extracted; PlayerUpdateStatus deferred to Stage 3`

## Acceptance criteria

1. `HandleFriendUpdate` and `Unblock` exist only in `FriendModule`; grep `LobbyServerProtocol.cs`
   for `HandleFriendUpdate|private.*Unblock` → zero hits.
2. `git show --stat` deletion count on the protocol file consistent with ~265 moved lines.
3. Snapshot test unmodified; full suite green (207 pre-existing + new).
4. Changes limited to: `FriendModule.cs` (new), `LobbyServerProtocol.cs`,
   `FriendModuleTest.cs` (new), doc 12.
   (`IClientConnection.cs` must NOT change; `HandlePlayerUpdateStatusRequest` stays on connection.)
