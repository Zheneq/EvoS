# Plan: Stage 3 step 6 — HandleRejoinGameRequest → GameLifecycleModule

## Motivation

`HandleRejoinGameRequest` is still on `LobbyServerProtocol` because
`Game.ReconnectPlayer(LobbyServerProtocol)` takes the concrete type. `ReconnectPlayer`
uses three things from the connection: `conn.JoinGame(this)`, `conn.OnStartGame(this)`,
and passes `conn` to `SendGameInfo(conn)`. All three are widening-friendly:

- `JoinGame(Game)` — delegates to `_gameLifecycle.JoinGame`, a game-lifecycle operation,
  natural on `IClientConnection`.
- `OnStartGame(Game)` — calls `_matchmaking.Unready()`, a state reset, also natural.
- `SendGameInfo(LobbyServerProtocol, ...)` — uses only `conn.AccountId` and `conn.Send`;
  both already on the interface.

Widening these allows `ReconnectPlayer` to accept `IClientConnection`, which lets the
handler move to `GameLifecycleModule` (its natural home).

Not in scope this step: `HandleSubscribeToCustomGamesRequest`/`HandleUnsubscribeFromCustomGamesRequest`
(require deeper `CustomGameManager.Subscribers` dict analysis) and `HandleRegisterGame`
(requires refactoring `SessionManager.OnPlayerConnect` field-mutation pattern).

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Add `JoinGame` and `OnStartGame` to `IClientConnection`

`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:

Add two members (the file already imports `CentralServer.BridgeServer`):
```csharp
void JoinGame(Game game);
void OnStartGame(Game game);
```

`Tests/Lib/RecordingClientConnection.cs`:

Add `using CentralServer.BridgeServer;` if not present.
Add stub implementations:
```csharp
public void JoinGame(Game game) { }
public void OnStartGame(Game game) { }
```

Build must pass with zero diff in any other file.

Commit: `Add JoinGame and OnStartGame to IClientConnection`

---

## Batch 2 — Widen `Game.ReconnectPlayer` and `Game.SendGameInfo`

`LobbyServer2/BridgeServer/Game.cs`:

Change `SendGameInfo` signature:
```csharp
// Before:
public void SendGameInfo(LobbyServerProtocol playerConnection, GameStatus gamestatus = GameStatus.None)
// After:
public void SendGameInfo(IClientConnection playerConnection, GameStatus gamestatus = GameStatus.None)
```

The body only accesses `playerConnection.AccountId` and `playerConnection.Send(...)` — both
on the interface. No other changes inside the method.

Check all callers of `SendGameInfo`: `SendGameInfoNotifications()` calls it with a
`LobbyServerProtocol` obtained from `SessionManager.GetClientConnection(player)` — this
still compiles since `LobbyServerProtocol : IClientConnection`. Grep the solution to
confirm no other external callers need fixing.

Change `ReconnectPlayer` signature:
```csharp
// Before:
public bool ReconnectPlayer(LobbyServerProtocol conn)
// After:
public bool ReconnectPlayer(IClientConnection conn)
```

The body uses `conn.AccountId` ✓, `conn.JoinGame(this)` ✓, `conn.OnStartGame(this)` ✓,
`SendGameInfo(conn)` ✓ — all now on the interface. No other changes inside the method.

`Game.cs` needs `using CentralServer.LobbyServer.Session;` for `IClientConnection`.
Check if already present; if not, add it.

Zero diff in any other file after this batch.

Commit: `Widen Game.ReconnectPlayer and Game.SendGameInfo to IClientConnection`

---

## Batch 3 — Move `HandleRejoinGameRequest` to `GameLifecycleModule`

### `LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`

Add a `registry.Register` call in `Register`:
```csharp
registry.Register<RejoinGameRequest>(HandleRejoinGameRequest);
```

Add the private handler method, adapting `AccountId`/`UserName`/`Send`/`ResetReadyState`
to use `_conn.X` and replacing `game.ReconnectPlayer(this)` with
`game.ReconnectPlayer(_conn)`:

```csharp
private void HandleRejoinGameRequest(RejoinGameRequest request)
{
    if (request.PreviousGameInfo == null || request.Accept == false)
    {
        _conn.Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
        return;
    }

    log.Info($"{_conn.UserName} wants to reconnect to game {request.PreviousGameInfo.GameServerProcessCode}");

    Game game = GameManager.GetGameWithPlayer(_conn.AccountId);

    if (game == null || game.Server == null || !game.Server.IsConnected)
    {
        _conn.Send(new RejoinGameResponse() { ResponseId = request.RequestId, Success = false });
        log.Info($"Game {request.PreviousGameInfo.GameServerProcessCode} not found");
        return;
    }

    LobbyServerPlayerInfo playerInfo = game.GetPlayerInfo(_conn.AccountId);
    if (playerInfo == null)
    {
        _conn.Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = false });
        log.Info($"{_conn.UserName} was not in game {request.PreviousGameInfo.GameServerProcessCode}");
        return;
    }

    _conn.Send(new RejoinGameResponse { ResponseId = request.RequestId, Success = true });
    log.Info($"Reconnecting {_conn.UserName} to game {game.GameInfo.GameServerProcessCode} ({game.ProcessCode})");
    _conn.ResetReadyState();
    game.ReconnectPlayer(_conn);
}
```

Check usings in `GameLifecycleModule`: needs `EvoS.Framework.Network.NetworkMessages` for
`RejoinGameResponse/Request` (already present). `GameManager` is in `CentralServer.BridgeServer`
(already present).

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Remove the registration line:
```csharp
RegisterHandler<RejoinGameRequest>(HandleRejoinGameRequest);
```

Remove the `HandleRejoinGameRequest` method entirely.

Build and test, commit:
`Move HandleRejoinGameRequest to GameLifecycleModule`

---

## Batch 4 — Docs

`docs/knowledge-base/12-design-assessment.md`:
- Add Step 6 complete section: `JoinGame(Game)` and `OnStartGame(Game)` added to
  `IClientConnection`; `Game.ReconnectPlayer` and `Game.SendGameInfo` widened to
  `IClientConnection`; `HandleRejoinGameRequest` moved to `GameLifecycleModule`.
- Remove `HandleRejoinGameRequest` from the "Blocked on Stage 3" bullet.
- Note deferred: `SubscribeToCustomGamesRequest`/`UnsubscribeFromCustomGamesRequest`
  (pending `CustomGameManager.Subscribers` dict analysis), `HandleRegisterGame` (pending
  `OnPlayerConnect` refactor), chat handlers (pending `ChatManager` event redesign).

Commit: `docs: HandleRejoinGameRequest in GameLifecycleModule; Stage 3 step 6`

---

## Acceptance criteria

1. Zero diff outside: `IClientConnection.cs`, `RecordingClientConnection.cs`, `Game.cs`,
   `GameLifecycleModule.cs`, `LobbyServerProtocol.cs`, doc 12.
2. `grep "RejoinGameRequest\|HandleRejoin" LobbyServerProtocol.cs` → zero hits.
3. `GameLifecycleModule` registers `RejoinGameRequest`.
4. `Game.ReconnectPlayer` and `Game.SendGameInfo` take `IClientConnection`.
5. Full test suite passes (212 tests).
