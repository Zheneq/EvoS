# Plan: Stage 3 step 7 — Subscribe/Unsubscribe → GameLifecycleModule

## Motivation

`HandleSubscribeToCustomGamesRequest` and `HandleUnsubscribeFromCustomGamesRequest` are
still on `LobbyServerProtocol` because `CustomGameManager.Subscribers` is typed
`Dictionary<long, LobbyServerProtocol>`. The only concrete member used on those values
is `value.IsConnected` in `NotifyUpdate` — every other access is `value.Send(...)` or
`value.AccountId`, both already on `IClientConnection`. Adding `IsConnected` to the
interface is the single blocker.

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Add `IsConnected` to `IClientConnection`

`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:
```csharp
bool IsConnected { get; }
```

`Tests/Lib/RecordingClientConnection.cs`:
```csharp
public bool IsConnected { get; set; } = true;
```

Build must pass with zero diff in any other file.

Commit: `Add IsConnected to IClientConnection`

---

## Batch 2 — Widen `CustomGameManager`

`LobbyServer2/LobbyServer/CustomGames/CustomGameManager.cs`:

1. Add `using CentralServer.LobbyServer.Session;` to the using block.

2. Change the `Subscribers` dict type:
   ```csharp
   // Before:
   private static readonly Dictionary<long, LobbyServerProtocol> Subscribers = ...
   // After:
   private static readonly Dictionary<long, IClientConnection> Subscribers = ...
   ```

3. Change `Subscribe` parameter:
   ```csharp
   // Before:
   public static void Subscribe(LobbyServerProtocol client)
   // After:
   public static void Subscribe(IClientConnection client)
   ```

4. Change `Unsubscribe` parameter:
   ```csharp
   // Before:
   public static void Unsubscribe(LobbyServerProtocol client)
   // After:
   public static void Unsubscribe(IClientConnection client)
   ```

5. In `NotifyUpdate`, update the foreach variable type:
   ```csharp
   // Before:
   foreach ((long key, LobbyServerProtocol value) in Subscribers)
   // After:
   foreach ((long key, IClientConnection value) in Subscribers)
   ```

No other changes inside any method body — `value.IsConnected`, `value.Send(notify)`,
`client.AccountId`, `client.Send(...)` all resolve via the interface.

Zero diff in any other file.

Commit: `Widen CustomGameManager.Subscribers to IClientConnection`

---

## Batch 3 — Move handlers to `GameLifecycleModule`

### `LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`

Add two `registry.Register` calls in `Register`:
```csharp
registry.Register<SubscribeToCustomGamesRequest>(HandleSubscribeToCustomGamesRequest);
registry.Register<UnsubscribeFromCustomGamesRequest>(HandleUnsubscribeFromCustomGamesRequest);
```

Add two private handler methods:
```csharp
private void HandleSubscribeToCustomGamesRequest(SubscribeToCustomGamesRequest request)
{
    CustomGameManager.Subscribe(_conn);
}

private void HandleUnsubscribeFromCustomGamesRequest(UnsubscribeFromCustomGamesRequest request)
{
    CustomGameManager.Unsubscribe(_conn);
}
```

Check usings in `GameLifecycleModule`: needs `CentralServer.LobbyServer.CustomGames` for
`CustomGameManager` (already present — `GameLifecycleModule` uses `CustomGameManager.JoinGame`).

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Remove the two registration lines:
```csharp
RegisterHandler<SubscribeToCustomGamesRequest>(HandleSubscribeToCustomGamesRequest);
RegisterHandler<UnsubscribeFromCustomGamesRequest>(HandleUnsubscribeFromCustomGamesRequest);
```

Remove the two handler methods `HandleSubscribeToCustomGamesRequest` and
`HandleUnsubscribeFromCustomGamesRequest` entirely.

Build and test, commit:
`Move Subscribe/UnsubscribeToCustomGamesRequest to GameLifecycleModule`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md`:
- Add Step 7 complete section: `IsConnected` added to `IClientConnection`;
  `CustomGameManager.Subscribers` widened to `IClientConnection`; `Subscribe`/`Unsubscribe`
  widened; `SubscribeToCustomGamesRequest` and `UnsubscribeFromCustomGamesRequest` moved to
  `GameLifecycleModule`.
- Remove those two handlers from the "Blocked on Stage 3" bullet.
- Note remaining blocked: `HandleRegisterGame` and chat handlers.

Commit: `docs: custom games subscribe handlers in GameLifecycleModule; Stage 3 step 7`

---

## Acceptance criteria

1. Zero diff outside: `IClientConnection.cs`, `RecordingClientConnection.cs`,
   `CustomGameManager.cs`, `GameLifecycleModule.cs`, `LobbyServerProtocol.cs`, doc 12.
2. `grep "SubscribeToCustomGames\|UnsubscribeFromCustomGames" LobbyServerProtocol.cs`
   → zero hits.
3. `GameLifecycleModule` registers both `SubscribeToCustomGamesRequest` and
   `UnsubscribeFromCustomGamesRequest`.
4. `CustomGameManager.Subscribe` and `Unsubscribe` take `IClientConnection`.
5. Full test suite passes (212 tests).
