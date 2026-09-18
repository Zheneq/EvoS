# Plan: Extract AccountModule from LobbyServerProtocol

Third module extraction. Pattern references: `StoreModule` (commit `d41e559`) and
`TelemetryModule` (commit `9d1ca93`) in `LobbyServer2/LobbyServer/`; design in
`docs/knowledge-base/12-design-assessment.md` §C Stage 1. Read all three first.

Repository: branch `refactor`; leave `docs/plans/` files alone.

## Ground rules (unchanged from previous extractions)

1. No behavior change; handler bodies move verbatim. Allowed substitutions only:
   `AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
   `OnAccountVisualsUpdated()` → `_conn.OnAccountVisualsUpdated()`,
   accessibility → `private`.
2. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` must pass **unchanged**.
3. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), then commit with the given
   message (no trailers).
4. Anything off-plan: stop and report.

## Batch 1 — AccountModule + tests

### Grow `IClientConnection` (one member)

The three customization selects call the connection's visuals-refresh hook. Add to
`IClientConnection`:

```csharp
void OnAccountVisualsUpdated();
```

`LobbyServerProtocol.OnAccountVisualsUpdated()` is currently `private` (it calls
`BroadcastRefreshFriendList` + `BroadcastRefreshGroup` + `CurrentGame?.OnAccountVisualsUpdated`);
make it `public` to satisfy the interface. Verified: no external callers today.

### New file `LobbyServer2/LobbyServer/Account/AccountModule.cs`

New folder; namespace `CentralServer.LobbyServer.Account`. Same shape as
`TelemetryModule`. Move these 15 handlers (all verified: no callers outside their own
registrations, so `private` in the module is safe):

| Message type | Handler | Notes |
|---|---|---|
| `OptionsNotification` | `HandleOptionsNotification` | delegates to `HandleEvosOptionsNotification` — both move, keep the internal call |
| `EvosOptionsNotification` | `HandleEvosOptionsNotification` | `UserMetadataDao` |
| `EvosOptionsNotificationLegacy` | `HandleEvosOptionsNotificationLegacy` | |
| `CustomKeyBindNotification` | `HandleCustomKeyBindNotification` | |
| `SetDevTagRequest` | `HandleSetDevTagRequest` | |
| `UpdateUIStateRequest` | `HandleUpdateUIStateRequest` | |
| `SetRegionRequest` | `HandleSetRegionRequest` | empty stub |
| `LoadingScreenToggleRequest` | `HandleLoadingScreenToggleRequest` | |
| `CheckRAFStatusRequest` | `HandleCheckRAFStatusRequest` | stub response |
| `SendRAFReferralEmailsRequest` | `HandleSendRAFReferralEmailsRequest` | stub response |
| `CheckAccountStatusRequest` | `HandleCheckAccountStatusRequest` | uses `LobbyConfiguration.IsTrustWarEnabled` + `TrustWarManager` statics — fine |
| `PlayerMatchDataRequest` | `HandlePlayerMatchDataRequest` | `MatchHistoryDao` |
| `SelectBannerRequest` | `HandleSelectBannerRequest` | uses `_conn.OnAccountVisualsUpdated()` |
| `SelectTitleRequest` | `HandleSelectTitleRequest` | same |
| `SelectRibbonRequest` | `HandleSelectRibbonRequest` | same |

Explicitly NOT in this module (game-coupled, deferred to GameLifecycle):
`UseOverconRequest`, `UseGGPackRequest` (they iterate `CurrentGame.GetClients()` and
mutate game state), `PlayerUpdateStatusRequest` (passes `this` to `FriendManager` —
Friend module later), `PreviousGameInfoRequest`, `UpdateRemoteCharacterRequest`.

Constructor: remove the 15 `RegisterHandler` lines; module array becomes
`{ new StoreModule(this), new TelemetryModule(this), new AccountModule(this) }`.

### Tests

- `RecordingClientConnection`: add `public int VisualsUpdates;` and
  `OnAccountVisualsUpdated() => VisualsUpdates++;`
- New `Tests/AccountModuleTest.cs` (extends `EvosTest`, unique AccountIds, no special
  collection). Cases — set up accounts via `DB.Get().AccountDao` with the relevant
  component collections populated:
  1. `SelectTitleRequest` with an unlocked title id: `SelectedTitleID` persisted,
     `VisualsUpdates == 1`, response carries the new id.
  2. `SelectTitleRequest` with a locked id: `SelectedTitleID` unchanged,
     `VisualsUpdates == 0`, response carries the old id (note: the handler still sends a
     response — that asymmetry is existing behavior, keep it).
  3. `SelectRibbonRequest` with a locked id: `Success = false` response, no visuals
     update.
  4. `SelectBannerRequest`: foreground vs background routing via
     `InventoryManager.BannerIsForeground` (pick one real foreground and one background
     id by querying `InventoryManager` in the test, don't hardcode).
  5. `SetDevTagRequest` for a non-dev account: `Success = false`.
  6. `LoadingScreenToggleRequest` for a known id: state flipped, `Success = true`;
     for an unknown id: `Success = false`.
  7. `UpdateUIStateRequest`: `UIStates[key]` persisted.
  8. `PlayerMatchDataRequest`: sends a `PlayerMatchDataResponse` (mock
     `MatchHistoryDao.Find` result — likely empty list; assert response sent with
     matching `ResponseId`).
  9. Stubs (`CheckRAFStatusRequest`, `SendRAFReferralEmailsRequest`,
     `SetRegionRequest`): expected response or no-op, no exception.

Commit: `Extract AccountModule from LobbyServerProtocol`

## Batch 2 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1: note Account is the third
module extracted; adjust the module-map wording if it listed the deferred handlers
(overcon/GG pack moved conceptually to GameLifecycle). Keep the edit small.

Commit: `docs: AccountModule extracted`

## Acceptance criteria

1. The 15 handlers exist only in `AccountModule`; registrations removed from the
   constructor; module array composes Store + Telemetry + Account.
2. Snapshot test passes unmodified; full suite green (163 pre-existing + new tests).
3. Changes limited to: `AccountModule.cs` (new), `LobbyServerProtocol.cs`,
   `IClientConnection.cs`, `RecordingClientConnection.cs`, `AccountModuleTest.cs`
   (new), doc 12.
