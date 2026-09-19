# Plan: IGroupRegistry — Stage 3 step 2

Same pattern as `stage3-session-registry.md`: instance class + static shim + DI singleton.
One extra step: `GroupModule` already has constructor injection, so its 31 static
`GroupManager.X(...)` calls migrate to `_groupRegistry.X(...)` in the same pass.

Internal methods `GetGroupInfo`, `UpdateSelectedSubTypes`, and `GetMemberData` call
`SessionManager.GetClientConnection(...)` for `SelectedGameType`, `GetSubTypeMask()`,
`IsReady` — those keep using the static shim for now (no `IClientConnection` growth
needed). The interface covers only the public method surface.

Repository: branch `refactor`; leave `docs/plans/` alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in the acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — `IGroupRegistry` interface

New file `LobbyServer2/LobbyServer/Group/IGroupRegistry.cs`,
namespace `CentralServer.LobbyServer.Group`:

```csharp
public interface IGroupRegistry
{
    object Lock { get; }
    GroupInfo GetGroup(long groupId);
    List<long> GetGroupMembers(long groupId);
    List<GroupInfo> GetGroups();
    GroupInfo GetPlayerGroup(long accountId);
    long CreateGroupRequest(long requesterAccountId, long requesteeAccountId, long groupId,
        GroupConfirmationRequest.JoinType joinType, TimeSpan expirationTime);
    GroupRequestInfo PopGroupRequest(long requestId);
    void PingGroupRequests();
    void CreateGroup(long leader);
    bool LeaveGroup(long accountId, bool warnIfNotInAGroup = true, bool wasKicked = false);
    void JoinGroup(long groupId, long accountId);
    bool PromoteMember(GroupInfo groupInfo, long accountId);
    LobbyPlayerGroupInfo GetGroupInfo(long accountId);
    long GetGroupID(long accountId);
    void OnLeaveQueue(long groupId);
    void Broadcast(GroupInfo group, WebSocketMessage message, long skipAccountId = 0);
    void BroadcastSystemMessage(GroupInfo group, LocalizationPayload message,
        long skipAccountId = 0);
    ushort GetGroupSubTypeMask(long groupId);
    ushort GetGroupSubTypeMask(GroupInfo groupInfo);
    void UpdateSelectedSubTypes(GroupInfo groupInfo, bool resetReadyStateIfUpdated = true);
    void UpdateSelectedSubTypesForAccount(long accountId);
}
```

Commit: `Add IGroupRegistry interface`

---

## Batch 2 — Convert `GroupManager` to instance class + static shim

`LobbyServer2/LobbyServer/Group/GroupManager.cs`:

1. `class GroupManager` → `class GroupManager : IGroupRegistry` (it was already non-public;
   make it `public`).
2. All static state (`ActiveGroups`, `PlayerToGroup`, `GroupRequests`, `_lastGroupId`,
   `_lastGroupRequestId`, `_lock`) become private instance fields (same names, same types).
3. Add `public static GroupManager Instance { get; internal set; } = new GroupManager()`.
4. `public static object Lock => _lock` stays; its body becomes `=> Instance._lock` (or
   keep as an instance property `object IGroupRegistry.Lock => _lock` and have the static
   forward to `Instance.Lock`).
5. Every `public static` method becomes a static forwarder to a private `*Core` instance
   method containing the original body:
   ```csharp
   public static GroupInfo GetPlayerGroup(long accountId) => Instance.GetPlayerGroupCore(accountId);
   private GroupInfo GetPlayerGroupCore(long accountId) { /* original body */ }
   ```
6. Implement `IGroupRegistry` explicitly using the same core methods:
   ```csharp
   GroupInfo IGroupRegistry.GetPlayerGroup(long accountId) => GetPlayerGroupCore(accountId);
   // etc. for all interface members
   ```
7. Private helpers (`GetMemberData`, `OnJoinGroup`, `OnLeaveGroup`, `OnGroupDisbanded`,
   `OnGroupMembersUpdated`) remain private instance methods — no changes to their bodies.
   They already call `ClientNotifier.Get()` and `SessionManager.GetClientConnection()`
   via the existing static shims; leave those as-is.

CRITICAL: Zero diff in any other file. All existing `GroupManager.X(...)` call sites
must compile unchanged after this batch. Grep for `GroupManager\.` across the solution
and confirm no other file changed.

Commit: `Convert GroupManager to instance class with static shim`

---

## Batch 3 — DI registration + inject into protocol and GroupModule

### `LobbyServer2/CentralServer.cs`

After the `AddSingleton<ISessionRegistry, SessionManager>()` line, add:
```csharp
builder.Services.AddSingleton<IGroupRegistry, GroupManager>();
```

After the `SessionManager.Instance = ...` line, add:
```csharp
GroupManager.Instance = (GroupManager)_app.Services.GetRequiredService<IGroupRegistry>();
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Add `IGroupRegistry groupRegistry` as a second constructor parameter (after
`ISessionRegistry sessionRegistry`); store as
`private readonly IGroupRegistry _groupRegistry`.

Update the modules array construction to pass it to `GroupModule`:
```csharp
// before:
new GroupModule(this)
// after:
new GroupModule(this, _groupRegistry)
```

### `LobbyServer2/LobbyServer/Group/GroupModule.cs`

Add `private readonly IGroupRegistry _groupRegistry` field. Add `IGroupRegistry groupRegistry`
as a second constructor parameter; assign `_groupRegistry = groupRegistry`.

Replace all 31 `GroupManager.X(...)` calls with `_groupRegistry.X(...)` — mechanical
find-and-replace. The call shapes are identical; only the prefix changes.

Build and test, then commit:
`Wire IGroupRegistry into DI; GroupModule uses injected registry`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 3:
- Record `IGroupRegistry` introduced; `GroupManager` converted to instance class with static
  shim; DI wired; `GroupModule` migrated to injected registry (31 static call sites removed).
- Note deferred: other modules (`MatchmakingModule`, `GameLifecycleModule`) still call
  `GroupManager.X()` statically; `GetGroupInfo`/`UpdateSelectedSubTypes`/`GetMemberData`
  still use concrete `LobbyServerProtocol` internally via `SessionManager` static shim.
- Update priority order: `SessionManager` ✓ → `GroupManager` ✓ →
  `ServerManager`/`GameManager` → `MatchmakingManager` next.

Commit: `docs: IGroupRegistry wired; Stage 3 step 2 complete`

---

## Acceptance criteria

1. Zero diff in any file outside: `IGroupRegistry.cs` (new), `GroupManager.cs`,
   `CentralServer.cs`, `LobbyServerProtocol.cs`, `GroupModule.cs`, doc 12.
2. `GroupModule` contains zero `GroupManager\.` references — grep to confirm.
3. All existing static `GroupManager.X(...)` call sites in other files compile unchanged.
4. Full test suite passes (212 tests).
5. `GroupManager.Instance` is non-null at runtime (set from DI before any connection lands).
