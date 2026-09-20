# Plan: Stage 3 step 5 — Status on IClientConnection; complete FriendModule

## Motivation

`FriendModule` was declared done after extracting `FriendUpdateRequest`, but
`HandlePlayerUpdateStatusRequest` was deliberately left on `LobbyServerProtocol` because
`FriendManager.OnPlayerUpdateStatusRequest(LobbyServerProtocol, ...)` takes the concrete
type and writes `client.Status`. This is the smallest remaining blocker in the
"Blocked on Stage 3" list.

Fix: add `PlayerOnlineStatus Status { get; set; }` to `IClientConnection` — the only new
member needed — then widen `OnPlayerUpdateStatusRequest`'s parameter type, and move the
handler into `FriendModule`.

Scope is deliberately narrow. `FriendManager.GetStatusString(LobbyServerProtocol)` also
takes the concrete type (reads `IsInGame`, `IsInCharacterSelect`, `IsInQueue`, `IsInGroup`
which are not on the interface), but it is NOT called from `HandlePlayerUpdateStatusRequest`
and its call sites all have a concrete `LobbyServerProtocol` from
`SessionManager.GetClientConnection`. It is left unchanged.

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Add `Status` to `IClientConnection`

`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:

```csharp
PlayerOnlineStatus Status { get; set; }
```

`LobbyServerProtocol` already has `public PlayerOnlineStatus Status = PlayerOnlineStatus.Online;`
— make it a property (`{ get; set; }`) so it satisfies the interface; change the field to a
property with a backing field initialised to `PlayerOnlineStatus.Online`.

`Tests/Lib/RecordingClientConnection.cs`:

Add:
```csharp
public PlayerOnlineStatus Status { get; set; } = PlayerOnlineStatus.Online;
```

Build must pass with zero diff in any other file.

Commit: `Add Status to IClientConnection`

---

## Batch 2 — Widen `FriendManager.OnPlayerUpdateStatusRequest`

`LobbyServer2/LobbyServer/Friend/FriendManager.cs`:

Change:
```csharp
public static PlayerUpdateStatusResponse OnPlayerUpdateStatusRequest(LobbyServerProtocol client, PlayerUpdateStatusRequest request)
```
to:
```csharp
public static PlayerUpdateStatusResponse OnPlayerUpdateStatusRequest(IClientConnection client, PlayerUpdateStatusRequest request)
```

Inside the method body, replace:
```csharp
MarkForUpdate(client);
```
with:
```csharp
MarkForUpdate(client.AccountId);
```
(`MarkForUpdate(long accountId)` already exists; the `MarkForUpdate(LobbyServerProtocol)`
overload just called `MarkForUpdate(client.AccountId)` anyway — it is now unused and can be
deleted.)

No other change to `FriendManager.cs`. `GetStatusString(LobbyServerProtocol)` is untouched.

The call site in `LobbyServerProtocol.HandlePlayerUpdateStatusRequest`:
```csharp
PlayerUpdateStatusResponse response = FriendManager.OnPlayerUpdateStatusRequest(this, request);
```
still compiles because `LobbyServerProtocol` implements `IClientConnection`.

Build must pass with zero diff in any other file.

Commit: `Widen FriendManager.OnPlayerUpdateStatusRequest to IClientConnection`

---

## Batch 3 — Move `HandlePlayerUpdateStatusRequest` to `FriendModule`

### `LobbyServer2/LobbyServer/Friend/FriendModule.cs`

Add a private handler method:
```csharp
private void HandlePlayerUpdateStatusRequest(PlayerUpdateStatusRequest request)
{
    log.Info($"{_conn.UserName} is now {request.StatusString}");
    PlayerUpdateStatusResponse response = FriendManager.OnPlayerUpdateStatusRequest(_conn, request);
    _conn.Send(response);
}
```

Add `Register` call in `FriendModule.Register`:
```csharp
registry.Register<PlayerUpdateStatusRequest>(HandlePlayerUpdateStatusRequest);
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Remove the handler registration:
```csharp
RegisterHandler<PlayerUpdateStatusRequest>(HandlePlayerUpdateStatusRequest);
```

Remove the handler method `HandlePlayerUpdateStatusRequest` entirely.

Note: `Status` field → property change from Batch 1 may need the declaration updated:
```csharp
// before (field):
public PlayerOnlineStatus Status = PlayerOnlineStatus.Online;
// after (auto-property):
public PlayerOnlineStatus Status { get; set; } = PlayerOnlineStatus.Online;
```

Build and test, then commit:
`Move HandlePlayerUpdateStatusRequest to FriendModule`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md`:
- Record Step 5 complete: `Status` added to `IClientConnection`; `FriendManager.OnPlayerUpdateStatusRequest`
  widened to `IClientConnection`; `HandlePlayerUpdateStatusRequest` moved to `FriendModule`.
- Remove `HandlePlayerUpdateStatusRequest` from the "Blocked on Stage 3" bullet; update
  `FriendModule` entry (now fully extracted — both handlers owned by the module).
- Note deferred: `FriendManager.GetStatusString(LobbyServerProtocol)` still takes the
  concrete type (reads `IsInGame`, `IsInCharacterSelect`, `IsInQueue`, `IsInGroup`).

Commit: `docs: Status on IClientConnection; FriendModule complete; Stage 3 step 5`

---

## Acceptance criteria

1. Zero diff outside: `IClientConnection.cs`, `RecordingClientConnection.cs`,
   `FriendManager.cs`, `FriendModule.cs`, `LobbyServerProtocol.cs`, doc 12.
2. `grep "HandlePlayerUpdateStatusRequest\|PlayerUpdateStatusRequest" LobbyServerProtocol.cs`
   → zero hits.
3. `FriendModule` registers both `FriendUpdateRequest` and `PlayerUpdateStatusRequest`.
4. `FriendManager.OnPlayerUpdateStatusRequest` parameter type is `IClientConnection`.
5. Full test suite passes (212 tests).
