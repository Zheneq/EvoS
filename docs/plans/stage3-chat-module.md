# Plan: Stage 3 step 9 — ChatNotification/GroupChatRequest → ChatModule

## Motivation

`HandleChatNotification` and `HandleGroupChatRequest` are the last non-draft handlers
on `LobbyServerProtocol`. They are blocked by the per-connection event subscription
pattern: `ChatManager.Register(LobbyServerProtocol)` hooks `conn.OnChatNotification`
and `conn.OnGroupChatRequest` (events typed `Action<LobbyServerProtocol, T>`), which
fire when the protocol's one-liner handlers raise them.

The only concrete-type dependency in `ChatManager.HandleChatNotification` and
`HandleGroupChatRequest` is `conn.PlayerInfo?.CharacterType` — the character the
player has selected for their current game. Everything else (`conn.AccountId`,
`conn.CurrentGame`, `conn.Send`, `conn.SendSystemMessage`) is already on
`IClientConnection`.

Strategy: add `CharacterType? ActiveCharacterType { get; }` to `IClientConnection`,
create `ChatModule` that calls `ChatManager` directly (no events), and dissolve the
subscription mechanism entirely. The per-connection events
(`OnChatNotification`, `OnGroupChatRequest`) disappear with no replacement — the
dispatch table now does what they did.

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Add `ActiveCharacterType` to `IClientConnection`

`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:

Add `using EvoS.Framework.Constants.Enums;` to the using block.

Add member:
```csharp
CharacterType? ActiveCharacterType { get; }
```

`LobbyServer2/LobbyServer/LobbyServerProtocol.cs`:

Add implementation:
```csharp
public CharacterType? ActiveCharacterType => _gameLifecycle.PlayerInfo?.CharacterType;
```

`Tests/Lib/RecordingClientConnection.cs`:

Add `using EvoS.Framework.Constants.Enums;` to the using block.

Add stub:
```csharp
public CharacterType? ActiveCharacterType { get; set; }
```

Build must pass with zero diff in any other file.

Commit: `Add ActiveCharacterType to IClientConnection`

---

## Batch 2 — Widen `ChatManager`

`LobbyServer2/LobbyServer/Chat/ChatManager.cs`:

1. Change `HandleChatNotification` signature:
```csharp
// Before:
public void HandleChatNotification(LobbyServerProtocol conn, ChatNotification notification)
// After:
public void HandleChatNotification(IClientConnection conn, ChatNotification notification)
```
Inside the body replace `conn.PlayerInfo?.CharacterType` with `conn.ActiveCharacterType`
in the message initializer (two occurrences — one in the message builder, one in
`HandleGroupChatRequest`; see below). All other accesses (`conn.AccountId`,
`conn.CurrentGame`, `conn.Send`, `conn.SendSystemMessage`) already compile against
`IClientConnection`.

2. Change `HandleGroupChatRequest` signature:
```csharp
// Before:
public void HandleGroupChatRequest(LobbyServerProtocol conn, GroupChatRequest request)
// After:
public void HandleGroupChatRequest(IClientConnection conn, GroupChatRequest request)
```
Inside the body replace `conn.PlayerInfo?.CharacterType` with `conn.ActiveCharacterType`.

3. Remove `Register` and `Unregister` methods entirely:
```csharp
// Remove:
private void Register(LobbyServerProtocol conn)
{
    conn.OnChatNotification += HandleChatNotification;
    conn.OnGroupChatRequest += HandleGroupChatRequest;
}

private void Unregister(LobbyServerProtocol conn)
{
    conn.OnChatNotification -= HandleChatNotification;
    conn.OnGroupChatRequest -= HandleGroupChatRequest;
}
```

4. Remove `OnPlayerConnected`/`OnPlayerDisconnected` subscriptions from constructor
   and destructor:
```csharp
// Remove from constructor:
SessionManager.OnPlayerConnected += Register;
SessionManager.OnPlayerDisconnected += Unregister;

// Remove from destructor:
SessionManager.OnPlayerConnected -= Register;
SessionManager.OnPlayerDisconnected -= Unregister;
```

After these removals the constructor and destructor may be empty — remove them if so,
or keep empty stubs if other initialization is present (check before deciding).

5. Add `using CentralServer.LobbyServer.Session;` if not already present (needed
   for `IClientConnection`).

Zero diff in any other file.

Commit: `Widen ChatManager.HandleChatNotification/HandleGroupChatRequest to IClientConnection`

---

## Batch 3 — Create `ChatModule`, clean up `LobbyServerProtocol`, widen events

### New file `LobbyServer2/LobbyServer/Chat/ChatModule.cs`

```csharp
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Network.NetworkMessages;
using log4net;

namespace CentralServer.LobbyServer.Chat;

public class ChatModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ChatModule));
    private readonly IClientConnection _conn;

    public ChatModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<ChatNotification>(HandleChatNotification);
        registry.Register<GroupChatRequest>(HandleGroupChatRequest);
    }

    private void HandleChatNotification(ChatNotification notification)
    {
        ChatManager.Get().HandleChatNotification(_conn, notification);
    }

    private void HandleGroupChatRequest(GroupChatRequest request)
    {
        ChatManager.Get().HandleGroupChatRequest(_conn, request);
    }
}
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

1. Remove the two registration lines from the constructor:
```csharp
RegisterHandler<ChatNotification>(HandleChatNotification);
RegisterHandler<GroupChatRequest>(HandleGroupChatRequest);
```

2. Add `new ChatModule(this)` to the `ILobbyModule[]` array. Position it after
   `LoginModule` and before `StoreModule` (chat is a core concern, not store-level).

3. Remove the two event declarations:
```csharp
public event Action<LobbyServerProtocol, ChatNotification> OnChatNotification = delegate { };
public event Action<LobbyServerProtocol, GroupChatRequest> OnGroupChatRequest = delegate { };
```

4. Remove the two handler methods:
```csharp
public void HandleChatNotification(ChatNotification notification)
{
    OnChatNotification(this, notification);
}

public void HandleGroupChatRequest(GroupChatRequest request)
{
    OnGroupChatRequest(this, request);
}
```

5. Add `using CentralServer.LobbyServer.Chat;` if not already present (needed for
   `ChatModule`).

6. Remove any using directives that become unused after these deletions.

### `LobbyServer2/LobbyServer/Session/SessionManager.cs`

`OnPlayerConnected` and `OnPlayerDisconnected` now have no subscribers. Change their
types from `Action<LobbyServerProtocol>` to `Action<IClientConnection>` and remove
the explicit cast that was added in Step 8:

```csharp
// Before:
public static event Action<LobbyServerProtocol> OnPlayerConnected = delegate {};
public static event Action<LobbyServerProtocol> OnPlayerDisconnected = delegate {};
// ...
OnPlayerConnected((LobbyServerProtocol)client);   // explicit cast from Step 8
OnPlayerDisconnected(client);

// After:
public static event Action<IClientConnection> OnPlayerConnected = delegate {};
public static event Action<IClientConnection> OnPlayerDisconnected = delegate {};
// ...
OnPlayerConnected(client);   // no cast needed; client is already IClientConnection
OnPlayerDisconnected(client);
```

`OnPlayerDisconnect(LobbyServerProtocol client)` still takes the concrete type —
only the event signature changes, not the static lifecycle method.

Build and test, commit:
`Move ChatNotification/GroupChatRequest to ChatModule; dissolve event subscription`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md`:
- Add Step 9 complete section: `ActiveCharacterType` added to `IClientConnection`;
  `ChatManager.HandleChatNotification/HandleGroupChatRequest` widened to
  `IClientConnection`; `ChatManager.Register/Unregister` removed; per-connection
  events `OnChatNotification`/`OnGroupChatRequest` removed; `ChatModule` created;
  `OnPlayerConnected`/`OnPlayerDisconnected` events widened to `Action<IClientConnection>`.
- Update "Blocked on Stage 3": remove chat handlers — now empty.
- Note: **Stage 3 is fully complete.** Remaining on `LobbyServerProtocol`: only the
  four ranked draft handlers (`RankedTradeRequest`, `RankedSelectionRequest`,
  `RankedBanRequest`, `RankedHoverClickRequest`) — these belong in `DraftController`
  and are Stage 4 work.

Commit: `docs: ChatModule extracted; Stage 3 complete`

---

## Acceptance criteria

1. Zero diff outside: `IClientConnection.cs`, `RecordingClientConnection.cs`,
   `ChatManager.cs`, `Chat/ChatModule.cs` (new), `LobbyServerProtocol.cs`,
   `SessionManager.cs`, doc 12.
2. `grep "OnChatNotification\|OnGroupChatRequest\|HandleChatNotification\|HandleGroupChatRequest" LobbyServerProtocol.cs`
   → zero hits.
3. `grep "Register\|Unregister\|OnPlayerConnected\|OnPlayerDisconnected" ChatManager.cs`
   → zero hits (only the `HandleChatNotification`/`HandleGroupChatRequest` public
   methods remain, plus `OnGlobalChatMessage`/`OnChatMessage`/`OnBroadcastMessage`
   which are unrelated outbound events).
4. `ChatModule` registers both `ChatNotification` and `GroupChatRequest`.
5. `SessionManager.OnPlayerConnected` and `OnPlayerDisconnected` are
   `Action<IClientConnection>`.
6. Full test suite passes (212 tests).
