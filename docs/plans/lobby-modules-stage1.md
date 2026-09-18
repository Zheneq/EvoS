# Plan: Stage-1 kickoff — Base merge, snapshot test, StoreModule pilot

Goal: start decomposing `LobbyServerProtocol` into composed handler modules, per the
design in `docs/knowledge-base/12-design-assessment.md` §C Stage 1 (read it first).
Three code batches + one docs batch, each independently buildable, tested, and committed.

Repository: current branch `refactor` — stay on it.

## Ground rules

1. **No behavior change.** Handler bodies move verbatim; only the mechanical
   substitutions listed below are allowed inside them. Wire compatibility with the
   unmodifiable game client is the hard constraint.
2. Locate code by the quoted patterns/member names, not line numbers.
3. After each batch: `dotnet build EvoS.sln` (0 errors, no new warnings — the 6
   pre-existing warnings are all in `EvoS.Framework`) and
   `dotnet test Tests/Tests.csproj` (0 failures). Commit per batch with the message
   given.
4. If something doesn't match this plan's description (an unexpected external caller, a
   handler that touches state this plan says it doesn't, a test you can't make pass
   within scope), stop and report instead of improvising.

## Batch 1 — merge `LobbyServerProtocolBase` into `LobbyServerProtocol`

`LobbyServer2/LobbyServer/LobbyServerProtocolBase.cs` has exactly one subclass
(`LobbyServerProtocol`) and one other reference: the parameter type of
`MatchmakingManager.StartPractice` (dead code — body commented out).

1. Move **all** members of `LobbyServerProtocolBase` into `LobbyServerProtocol`
   verbatim. Change the class declaration to
   `public class LobbyServerProtocol : WebSocketBehaviorBase<WebSocketMessage>`.
   - Both classes declare `private static readonly ILog log` — keep only
     `LobbyServerProtocol`'s (`typeof(LobbyServerProtocol)`); the moved members use it.
   - Merge the `using` lists; drop duplicates and anything the compiler flags unused.
   - Keep member accessibility as-is (`public` fields stay public for now).
2. Change `StartPractice(LobbyServerProtocolBase client)` in
   `LobbyServer2/LobbyServer/Matchmaking/MatchmakingManager.cs` to take
   `LobbyServerProtocol`.
3. Delete `LobbyServerProtocolBase.cs`.
4. Doc references to the deleted class are handled in Batch 4, not here.

Commit: `Merge LobbyServerProtocolBase into LobbyServerProtocol`

## Batch 2 — handler-set snapshot test

Purpose: a regression net proving the set of handled message types never changes while
handlers move between files.

1. In `LobbyServer2/WebSocketBehaviorBase.cs` add, next to the `messageHandlers` field:
   `internal IReadOnlyCollection<Type> RegisteredMessageTypes => messageHandlers.Keys;`
   (`CentralServer.csproj` already has `InternalsVisibleTo("Tests")`.)
2. New test `Tests/LobbyHandlerRegistrationTest.cs` (extends `EvosTest`, standard ctor
   with `ITestOutputHelper`; no special collection needed):
   - `new LobbyServerProtocol()` is safe in-process: the socket is only assigned in
     `RunConnection`, and the constructor only registers handlers.
   - Assert that the sorted list of `RegisteredMessageTypes.Select(t => t.FullName)`
     equals a hardcoded expected list. Generate that list from the current constructor's
     `RegisterHandler<T>` calls (~75 entries; count them — the test must match exactly,
     and `Assert.Equal` on sorted lists gives a readable diff on failure). Add a comment
     in the test: "This is the wire contract. If this test fails, a handler registration
     was added, removed, or lost in a refactor — update the list only for deliberate
     contract changes."

Commit: `Add handler registration snapshot test`

## Batch 3 — module seam + `StoreModule` pilot

### 3a. Seam contracts (new files in `LobbyServer2/LobbyServer/Session/`)

- `IClientConnection.cs`:
  `public interface IClientConnection { long AccountId { get; } void Send(WebSocketMessage message); }`
  Grow it only when a handler being moved needs another member — do not add speculative
  members.
- `IHandlerRegistry.cs`:
  `public interface IHandlerRegistry { void Register<T>(Action<T> handler) where T : WebSocketMessage; }`
- `ILobbyModule.cs`:
  `public interface ILobbyModule { void Register(IHandlerRegistry registry); }`
  (Lifecycle hooks like `OnDisconnect` are deliberately absent — added when the first
  module needs them.)

`LobbyServerProtocol` implements both `IClientConnection` and `IHandlerRegistry`:
- `AccountId` is currently a public **field** (from the Batch-1 merge); convert it to a
  public auto-property `public long AccountId { get; set; }` so it can satisfy the
  interface. First grep the solution for `ref ` / `out ` usage of `AccountId` on this
  type — expected none; if found, stop and report.
- `IHandlerRegistry.Register<T>` delegates to the protected `RegisterHandler<T>`
  (explicit interface implementation is fine).

### 3b. `StoreModule`

New file `LobbyServer2/LobbyServer/Store/StoreModule.cs` (namespace
`CentralServer.LobbyServer.Store`, next to the existing `StoreManager`):

```csharp
public class StoreModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(StoreModule));
    private readonly IClientConnection _conn;
    public StoreModule(IClientConnection conn) { _conn = conn; }
    public void Register(IHandlerRegistry registry) { /* 13 Register calls */ }
    // handler methods, private
}
```

Move these 13 handlers from `LobbyServerProtocol` (bodies verbatim; only allowed edits:
`AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`, accessibility → `private`):

| Message type | Handler |
|---|---|
| `PricesRequest` | `HandlePricesRequest` (currently `public` — verified no external callers) |
| `PurchaseTintRequest` | `HandlePurchaseTintRequest` (currently `public` — same) |
| `PurchaseModRequest` | `HandlePurchaseModRequest` |
| `PurchaseTitleRequest` | `HandlePurchaseTitleRequest` |
| `PurchaseTauntRequest` | `HandlePurchaseTauntRequest` |
| `PurchaseChatEmojiRequest` | `HandlePurchaseChatEmojiRequest` |
| `PurchaseLoadoutSlotRequest` | `HandlePurchaseLoadoutSlotRequest` |
| `PaymentMethodsRequest` | `HandlePaymentMethodsRequest` (empty body — move as-is) |
| `StoreOpenedMessage` | `HandleStoreOpenedMessage` (empty body — move as-is) |
| `PurchaseBannerForegroundRequest` | `HandlePurchaseEmblemRequest` |
| `PurchaseBannerBackgroundRequest` | `HandlePurchaseBannerRequest` |
| `PurchaseAbilityVfxRequest` | `HandlePurchasAbilityVfx` (may fix the typo to `HandlePurchaseAbilityVfx` — private method, safe) |
| `PurchaseInventoryItemRequest` | `HandlePurchaseInventoryItemRequest` |

Do NOT move `HandleUIActionNotification` — it is registered near the store block but is
not store logic; it stays.

In the `LobbyServerProtocol` constructor: delete the 13 corresponding
`RegisterHandler<...>` lines and add at the end:

```csharp
ILobbyModule[] modules = { new StoreModule(this) };
foreach (ILobbyModule module in modules)
{
    module.Register(this);
}
```

**The Batch-2 snapshot test must pass unchanged** — that is the proof the wire surface
is intact. If it fails, a registration was lost; fix the module, never the snapshot.

### 3c. Tests

- `Tests/Lib/RecordingClientConnection.cs`: implements `IClientConnection`; settable
  `AccountId`, `public readonly List<WebSocketMessage> Sent`, `Send` appends.
- `Tests/StoreModuleTest.cs` (extends `EvosTest`; no `ClientNotifierSeam` collection —
  these tests don't touch the notifier). `DB.Get()` falls back to in-memory mock DAOs in
  the test environment; **use a unique AccountId per test** (mock DAO state is
  process-global and tests run in parallel). Build accounts inline or extend
  `TestAccountHelper` — the store tests need `BankComponent` (balances),
  `AccountComponent.UnlockedBannerIDs`, and `CharacterData` populated; persist via
  `DB.Get().AccountDao` following existing test patterns (see `PatchAccountDataTest`).
  Invoke handlers by calling the module's `Register` into a tiny registry fake (or make
  the handlers `internal` — your choice; keep it simple).
  Cases:
  1. Emblem purchase, sufficient funds: banner id added to `UnlockedBannerIDs`, balance
     reduced by `InventoryManager.GetBannerCost(...)` (read the cost from
     `InventoryManager` in the test — don't hardcode it), and exactly
     `PurchaseBannerForegroundResponse` (Success) + `PlayerAccountDataUpdateNotification`
     sent, in that order.
  2. Emblem purchase, insufficient funds: Failed response only; no unlock, no deduction.
  3. Loadout slot purchase below cap: loadout added; response Success +
     `PlayerCharacterDataUpdateNotification` + `PlayerAccountDataUpdateNotification`.
  4. Loadout slot at cap (10 loadouts): Failed response, no loadout added.
  5. Stub purchases (`Mod`, `Title`, `Taunt`, `ChatEmoji`, `InventoryItem`): each sends
     a single response with `Success = false` / `PurchaseResult.Failed`.
  If `InventoryManager` cost lookups require game data unavailable in the test
  environment, check how the existing `InventoryManagerTest` handles it and follow that;
  if genuinely blocked, report instead of stubbing.

Commit: `Extract StoreModule from LobbyServerProtocol behind module seam`

## Batch 4 — docs

1. `docs/knowledge-base/03-lobby-and-sessions.md`: the table row for
   `LobbyServerProtocolBase` — merge its description into the `LobbyServerProtocol` row
   (class is gone). Grep `docs/knowledge-base/` for other `LobbyServerProtocolBase`
   mentions and fix them.
2. `docs/knowledge-base/12-design-assessment.md`:
   - §C Stage 1: mark step 1 (Base merge) done; note the snapshot test exists and the
     Store pilot is extracted (first module of the map in step 3).
   - "Suggested first PRs" item 2: strike through, mark **Done.**
3. Double-check counts/claims against the code before writing them.

Commit: `docs: record Stage-1 kickoff (Base merge, snapshot test, StoreModule)`

## Acceptance criteria

1. `LobbyServerProtocolBase.cs` no longer exists; solution builds; all tests pass.
2. The snapshot test exists, passes, and was NOT modified in Batch 3.
3. `LobbyServerProtocol` contains none of the 13 store handlers and no store-related
   `RegisterHandler` lines; `StoreModule` registers exactly those 13 message types.
4. New store tests cover at least the 5 listed cases and pass.
5. No changes outside the files this plan names (plus the two docs).
