# Plan: Extract TelemetryModule from LobbyServerProtocol

Second module extraction, following the pattern established by `StoreModule`
(`LobbyServer2/LobbyServer/Store/StoreModule.cs`, commit `d41e559`) and the Stage-1
design in `docs/knowledge-base/12-design-assessment.md` §C — read both first.

Repository: branch `refactor`, working tree clean except `docs/plans/` (leave that
directory's files alone).

## Ground rules (same as the Store extraction)

1. No behavior change; handler bodies move verbatim. Allowed substitutions only:
   `AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
   `CurrentGame` → `_conn.CurrentGame`, accessibility → `private`.
2. The snapshot test `Tests/LobbyHandlerRegistrationTest.cs` must pass **unchanged** —
   it is the wire-contract proof. If it fails, fix the extraction, never the snapshot.
3. After each batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing
   EvoS.Framework warnings) and `dotnet test Tests/Tests.csproj` (0 failures), then
   commit with the message given (plain `git commit`, no trailers).
4. Anything off-plan (unexpected external caller, state dependency not listed here,
   blocked test): stop and report, don't improvise.

## Batch 1 — TelemetryModule + tests

### Grow `IClientConnection` (minimal)

`HandleClientFeedbackReport` reads `CurrentGame`. Add to
`LobbyServer2/LobbyServer/Session/IClientConnection.cs`:

```csharp
Game CurrentGame { get; }   // using CentralServer.BridgeServer;
```

`LobbyServerProtocol.CurrentGame` is already a public property with a private setter —
it satisfies the read-only interface member as-is. Add nothing else.

### New file `LobbyServer2/LobbyServer/Utils/TelemetryModule.cs`

Namespace `CentralServer.LobbyServer.Utils` (next to `CrashReportManager`). Same shape
as `StoreModule`: `ILobbyModule`, own `log`, ctor takes `IClientConnection`. Move these
8 handlers from `LobbyServerProtocol` and register them:

| Message type | Handler | Notes |
|---|---|---|
| `UIActionNotification` | `HandleUIActionNotification` | empty stub — move as-is |
| `CrashReportArchiveNameRequest` | `HandleCrashReportArchiveNameRequest` | uses `SessionManager.GetSessionInfo(AccountId)` — a static lookup, stays as-is |
| `ClientStatusReport` | `HandleClientStatusReport` | |
| `ClientErrorSummary` | `HandleClientErrorSummary` | currently `public`; verified no external callers — make `private` |
| `ClientErrorReport` | `HandleClientErrorReport` | |
| `ErrorReportSummaryResponse` | `HandleErrorReportSummaryResponse` | |
| `ClientFeedbackReport` | `HandleClientFeedbackReport` | reads `CurrentGame` (see above); `GameIdString` needs `using static EvoS.Framework.Misc.GameUtils;` |
| `ClientPerformanceReport` | `HandleClientPerformanceReport` | log-only |

In the `LobbyServerProtocol` constructor: delete those 8 `RegisterHandler` lines and
extend the existing composition array:

```csharp
ILobbyModule[] modules = { new StoreModule(this), new TelemetryModule(this) };
```

### Tests

- Extend `Tests/Lib/RecordingClientConnection.cs` with a settable
  `Game CurrentGame { get; set; }` (defaults to null) to satisfy the grown interface.
- New `Tests/TelemetryModuleTest.cs` (extends `EvosTest`, no special collection;
  unique AccountIds per test — mock DAO state is process-global):
  1. `CrashReportArchiveNameRequest` with no session registered in `SessionManager`
     (the test default): exactly one `CrashReportArchiveNameResponse` sent, with
     `Success = false`.
  2. `ClientFeedbackReport` with `CurrentGame = null`: no exception; feedback persisted
     via `DB.Get().UserFeedbackDao` (assert through the DAO if it exposes a read method;
     if it is write-only, assert no-throw and note it in the test).
     `DiscordManager.SendPlayerFeedback` early-returns when no Discord channel is
     configured — safe in tests.
  3. `ClientErrorReport` and `ClientErrorSummary`: no exception; the summary path for an
     unknown hash routes an `ErrorReportSummaryRequest` through `ClientNotifier` — the
     account is offline in tests, so a no-op. (If you assert on it via the recording
     notifier instead, the test class must join `[Collection("ClientNotifierSeam")]` —
     simpler to just assert no-throw.)
  4. `ClientPerformanceReport` and `UIActionNotification`: no-throw smoke.
- The snapshot test passing unchanged remains the primary regression net.

Commit: `Extract TelemetryModule from LobbyServerProtocol`

## Batch 2 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1: in the module map / order
item, note Telemetry is extracted (second module after Store). Keep it to a few words —
the structure of the doc should not change.

Commit: `docs: TelemetryModule extracted`

## Acceptance criteria

1. The 8 handlers exist only in `TelemetryModule`; their registrations are gone from the
   `LobbyServerProtocol` constructor; the module array composes Store + Telemetry.
2. Snapshot test passes without modification; full suite green.
3. No changes outside: `TelemetryModule.cs` (new), `LobbyServerProtocol.cs`,
   `IClientConnection.cs`, `RecordingClientConnection.cs`, `TelemetryModuleTest.cs`
   (new), and doc 12.
