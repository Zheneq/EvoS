# Plan: Stage 1 remaining handlers — Overcon/GGPack + AdminModule

Two independent handler groups still on `LobbyServerProtocol` that the docs categorised as
"Could still move in Stage 1 (deferred, not blocked)":

- `UseOverconRequest` / `UseGGPackRequest` — natural fit in `GameLifecycleModule`; already
  has `CurrentGame` and all required imports.
- `DEBUG_AdminSlashCommandNotification` — self-contained ~30 lines; new `AdminModule`.

Zero interface changes needed. `IClientConnection` is not grown.

Note on the fan-out loop in both Overcon and GGPack handlers:
`Game.GetClients()` returns `IEnumerable<LobbyServerProtocol>`. Because `IEnumerable<T>`
is covariant (`out T`), a `foreach (IClientConnection client in ...)` loop compiles without
a concrete-type import in the module.

Repository: branch `refactor`. Leave `docs/plans/` files alone.

## Ground rules

1. No behavior change. Zero diff in any file not listed in acceptance criteria.
2. `dotnet build EvoS.sln` (0 errors) + `dotnet test Tests/Tests.csproj` (0 failures)
   after every batch before committing.
3. Anything off-plan: stop and report.

---

## Batch 1 — Overcon + GGPack → `GameLifecycleModule`

### `LobbyServer2/LobbyServer/GameLifecycle/GameLifecycleModule.cs`

Add two `registry.Register` calls to `Register`:
```csharp
registry.Register<UseOverconRequest>(HandleUseOverconRequest);
registry.Register<UseGGPackRequest>(HandleUseGGPackRequest);
```

Add two private handler methods (copied verbatim from `LobbyServerProtocol`, replacing
`AccountId` / `CurrentGame` with `_conn.AccountId` / `CurrentGame`, and using
`IClientConnection` for the fan-out variable):

```csharp
private void HandleUseOverconRequest(UseOverconRequest request)
{
    UseOverconResponse response = new UseOverconResponse()
    {
        ActorId = request.ActorId,
        OverconId = request.OverconId,
        ResponseId = request.RequestId
    };

    _conn.Send(response);

    if (CurrentGame != null)
    {
        response.ResponseId = 0;
        foreach (IClientConnection client in CurrentGame.GetClients())
        {
            if (client.AccountId != _conn.AccountId)
            {
                client.Send(response);
            }
        }
    }
}

private void HandleUseGGPackRequest(UseGGPackRequest request)
{
    PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
    UseGGPackResponse response = new UseGGPackResponse()
    {
        GGPackUserName = account.Handle,
        GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
        GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
        GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
        GGPackUserTitle = account.AccountComponent.SelectedTitleID,
        GGPackUserTitleLevel = 1,
        ResponseId = request.RequestId
    };
    _conn.Send(response);

    if (CurrentGame != null)
    {
        CurrentGame.OnPlayerUsedGGPack(_conn.AccountId);
        foreach (IClientConnection client in CurrentGame.GetClients())
        {
            if (client.AccountId != _conn.AccountId)
            {
                UseGGPackNotification useGGPackNotification = new UseGGPackNotification()
                {
                    GGPackUserName = account.Handle,
                    GGPackUserBannerBackground = account.AccountComponent.SelectedBackgroundBannerID,
                    GGPackUserBannerForeground = account.AccountComponent.SelectedForegroundBannerID,
                    GGPackUserRibbon = account.AccountComponent.SelectedRibbonID,
                    GGPackUserTitle = account.AccountComponent.SelectedTitleID,
                    GGPackUserTitleLevel = 1,
                    NumGGPacksUsed = CurrentGame.GameInfo.ggPackUsedAccountIDs[_conn.AccountId]
                };
                client.Send(useGGPackNotification);
            }
        }
    }
}
```

No new `using` directives needed — `GameLifecycleModule` already imports
`EvoS.Framework.DataAccess`, `EvoS.Framework.Network.NetworkMessages`, and
`EvoS.Framework.Network.Static`.

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Remove the two registration lines:
```csharp
RegisterHandler<UseOverconRequest>(HandleUseOverconRequest);
RegisterHandler<UseGGPackRequest>(HandleUseGGPackRequest);
```

Remove the two handler methods `HandleUseOverconRequest` and `HandleUseGGPackRequest`
in their entirety.

Build and test, commit:
`Move UseOvercon and UseGGPack handlers to GameLifecycleModule`

---

## Batch 2 — `DEBUG_AdminSlashCommandNotification` → `AdminModule`

### New file `LobbyServer2/LobbyServer/Admin/AdminModule.cs`

Namespace `CentralServer.LobbyServer.Admin`. No folder needs creating — the file itself
defines the namespace; the project picks it up automatically.

```csharp
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Session;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Admin;

public class AdminModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(AdminModule));
    private readonly IClientConnection _conn;

    public AdminModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<DEBUG_AdminSlashCommandNotification>(HandleDEBUG_AdminSlashCommandNotification);
    }

    private void HandleDEBUG_AdminSlashCommandNotification(DEBUG_AdminSlashCommandNotification notification)
    {
        log.Info($"DEBUG_AdminSlashCommandNotification: {notification.Command}");
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        if (account == null)
        {
            return;
        }
        log.Info($"DEBUG_AdminSlashCommandNotification: dev={account.AccountComponent.IsDev()} devMode={EvosConfiguration.GetDevMode()}");
        if (account.AccountComponent.IsDev() || EvosConfiguration.GetDevMode())
        {
            Game game = GameManager.GetGameWithPlayer(_conn.AccountId);
            if (game != null)
            {
                Team team = game.TeamInfo.TeamPlayerInfo.Find(x => x.AccountId == _conn.AccountId).TeamId;
                switch (notification.Command)
                {
                    case "End Game (Win)":
                        game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamAWon : GameResult.TeamBWon);
                        break;
                    case "End Game (Loss)":
                        game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamBWon : GameResult.TeamAWon);
                        break;
                    case "End Game (No Result)":
                    case "End Game (With Parameters)":
                    case "End Game (Tie)":
                        game.Server.AdminShutdown(GameResult.TieGame);
                        break;
                    case "Cooldowns":
                        game.Server.AdminClearCooldown();
                        break;
                }
            }
            else
            {
                log.Info("DEBUG_AdminSlashCommandNotification: game not found");
            }
        }
    }
}
```

### `LobbyServer2/LobbyServer/LobbyServerProtocol.cs`

Add `using CentralServer.LobbyServer.Admin;` to the using block.

In the constructor, add after the last `new XModule(this).Register(this)` line:
```csharp
new AdminModule(this).Register(this);
```

Remove the registration line:
```csharp
RegisterHandler<DEBUG_AdminSlashCommandNotification>(HandleDEBUG_AdminSlashCommandNotification);
```

Remove the `HandleDEBUG_AdminSlashCommandNotification` method in its entirety.

Build and test, commit:
`Extract DEBUG_AdminSlashCommandNotification into AdminModule`

---

## Batch 3 — Docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1:
- Update the "Could still move in Stage 1 (deferred, not blocked)" bullet: strike both items
  as done.
- Extend the module list to record `GameLifecycleModule` now owns `UseOverconRequest` and
  `UseGGPackRequest`; `AdminModule` (1 handler, new file `Admin/AdminModule.cs`) extracted.
- Update the "What remains on the connection — categorized" section: all "deferred" items
  are now resolved; only "Blocked on Stage 3" and "Stage 4" remain.
- Note: Stage 1 is now fully complete.

Commit: `docs: Stage 1 complete; Overcon/GGPack in GameLifecycleModule; AdminModule`

---

## Acceptance criteria

1. Zero diff outside: `GameLifecycleModule.cs`, `Admin/AdminModule.cs` (new),
   `LobbyServerProtocol.cs`, doc 12.
2. `grep "UseOvercon\|UseGGPack\|DEBUG_Admin" LobbyServerProtocol.cs` → zero hits.
3. `GameLifecycleModule` registers `UseOverconRequest` and `UseGGPackRequest`.
4. `AdminModule` registers `DEBUG_AdminSlashCommandNotification`.
5. Full test suite passes (212 tests; handler-set snapshot test confirms wire contract
   unchanged).
