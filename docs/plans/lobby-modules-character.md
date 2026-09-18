# Plan: Extract CharacterModule (first module-to-module dependency)

Seventh module extraction. New architectural element, agreed with the maintainer:
**direct module dependencies** — `CharacterModule` takes references to
`MatchmakingModule` and `GameLifecycleModule` in its constructor instead of growing
`IClientConnection` (which stays reserved for genuinely connection-shaped members:
identity, send, refresh hooks). Pattern references for everything else:
`MatchmakingModule.cs`, `GameLifecycleModule.cs`;
`docs/knowledge-base/12-design-assessment.md` §C Stage 1.

Repository: branch `refactor`; leave `docs/plans/` alone.

## Ground rules

1. No behavior change; verbatim moves with the substitutions listed below.
2. Snapshot test `Tests/LobbyHandlerRegistrationTest.cs` passes **unchanged**.
3. Per batch: `dotnet build EvoS.sln` (0 errors, only the 6 pre-existing Framework
   warnings) + `dotnet test Tests/Tests.csproj` (0 failures), commit with the given
   message (no trailers).
4. **Verify the deletion side**: after moving, grep `LobbyServerProtocol.cs` for every
   moved member name — zero hits expected. Confirm the protocol-file deleted-line count
   in `git show --stat` is consistent with the moved body sizes (~200 lines). (A prior
   extraction left dead copies behind; do not repeat that.)
5. Anything off-plan: stop and report; drop-and-report clause applies to tests.

## Batch 1 — CharacterModule + delegator cleanup + tests

### New file `LobbyServer2/LobbyServer/Character/CharacterModule.cs`

Namespace `CentralServer.LobbyServer.Character` (folder exists, holds the lobby-side
`CharacterManager`). Shape:

```csharp
public class CharacterModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CharacterModule));
    private readonly IClientConnection _conn;
    private readonly MatchmakingModule _matchmaking;
    private readonly GameLifecycleModule _gameLifecycle;

    public CharacterModule(IClientConnection conn, MatchmakingModule matchmaking, GameLifecycleModule gameLifecycle) { ... }
    public void Register(IHandlerRegistry registry) { /* 2 registrations */ }
}
```

IMPORTANT: the handler body calls `CharacterManager.GetCharacterComponent(...)` meaning
`EvoS.DirectoryServer.Character.CharacterManager` — and this module's namespace contains
the *other* `CharacterManager`. Carry over the using-alias from `LobbyServerProtocol.cs`:
`using CharacterManager = EvoS.DirectoryServer.Character.CharacterManager;`

Move these members (all verified: no callers outside registrations/each other):

| Member | Substitutions beyond the standard ones |
|---|---|
| `HandlePlayerInfoUpdateRequest` (`PlayerInfoUpdateRequest`) | `PlayerInfo` → `_gameLifecycle.PlayerInfo`; `CurrentGame` → `_gameLifecycle.CurrentGame`; `SetGameType(` → `_matchmaking.SetGameType(`; `SetAllyDifficulty(` → `_matchmaking.SetAllyDifficulty(`; `SetContextualReadyState(` → `_matchmaking.SetContextualReadyState(`; `SetEnemyDifficulty(` → `_matchmaking.SetEnemyDifficulty(`; `BroadcastRefreshGroup()` → `_conn.BroadcastRefreshGroup()` |
| `ApplyCharacterDataUpdate` (private static helper) | none — moves as-is |
| `HandleUpdateRemoteCharacterRequest` (`UpdateRemoteCharacterRequest`) | standard only (`AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`) |
| `UpdateCharacterSlots` (private helper) | standard only |

Standard substitutions: `AccountId` → `_conn.AccountId`, `Send(` → `_conn.Send(`,
accessibility → `private`.

### `LobbyServerProtocol` changes

- Remove the 2 `RegisterHandler` lines; add
  `new CharacterModule(this, _matchmaking, _gameLifecycle)` to the modules array (after
  the two modules it depends on).
- Delete the moved members.
- **Delegator cleanup** (their only callers moved): delete the now-unused delegators
  `SetAllyDifficulty`, `SetEnemyDifficulty`, `SetContextualReadyState` (all `protected`,
  verified called only from `HandlePlayerInfoUpdateRequest`). Keep `SetGameType`
  (public — `GroupModule` calls it cross-connection) and `ResetReadyState` (on
  `IClientConnection`). If the compiler or a grep reveals another caller, stop and
  report instead of keeping the delegator silently.

### Tests

New `Tests/CharacterModuleTest.cs`, `[Collection("ClientNotifierSeam")]` (uses
`GroupManager.CreateGroup`), unique AccountIds. Note:
`HandlePlayerInfoUpdateRequest` assumes the player has a group
(`GroupManager.GetPlayerGroup(...).IsSolo()` would NRE otherwise — existing behavior,
sessions always get a group at connect); tests must call
`GroupManager.CreateGroup(accountId)` in setup.

1. `UpdateRemoteCharacterRequest` with empty `RemoteSlotIndexes`/`Characters`: one
   failed `UpdateRemoteCharacterResponse`, nothing else sent.
2. `UpdateRemoteCharacterRequest` setting a valid character into slot 0 (account whose
   `LastRemoteCharacters` is empty): `LastRemoteCharacters[0]` updated,
   `PlayerAccountDataUpdateNotification` + success response sent.
3. `UpdateRemoteCharacterRequest` with a forbidden character (`PendingWillFill`):
   `ChatNotification` warning sent, slot unchanged, failed response.
4. `HandlePlayerInfoUpdateRequest` selecting a character (update with `CharacterType`
   only, no game): `AccountComponent.LastCharacter` persisted,
   `PlayerAccountDataUpdateNotification` + `PlayerInfoUpdateResponse` sent, one group
   refresh recorded on the fake connection.
5. `HandlePlayerInfoUpdateRequest` with `request.GameType` set for a solo-group player:
   `_matchmaking.SelectedGameType` updated (assert on the module instance).
6. `HandlePlayerInfoUpdateRequest` with `ContextualReadyState = Ready` (no game, no
   penalties): `_matchmaking.IsReady` becomes true.

Account setup needs `CharacterData` populated for the selected character — follow
whatever existing tests/`CharacterManager` provide (e.g. character data creation used
by login paths); if populating it requires unavailable game data, drop cases 4–6 with a
note and keep 1–3.

Commit: `Extract CharacterModule with direct module dependencies`

## Batch 2 — docs

`docs/knowledge-base/12-design-assessment.md` §C Stage 1:
- Record CharacterModule as the seventh extraction and the **module-to-module
  dependency precedent**: modules receive other modules via constructor from the
  composition root; `IClientConnection` stays connection-shaped. Adjust the Stage-1
  step-2 wording if it implied all module access goes through `IClientConnection`.
- Update the "what remains on the connection" note: `HandleRegisterGame`/`HandleClose`
  bootstrap, chat events, overcon/GG pack, custom-game subscribe/rejoin, draft
  handlers, `DEBUG_AdminSlashCommandNotification`, `HandleFriendUpdate` +
  `HandlePlayerUpdateStatusRequest` (Friend module candidate, `Status` ownership).

Commit: `docs: CharacterModule extracted; module-to-module wiring precedent`

## Acceptance criteria

1. The 2 handlers + 2 helpers exist only in `CharacterModule`; grep the protocol file
   for `HandlePlayerInfoUpdateRequest|ApplyCharacterDataUpdate|HandleUpdateRemoteCharacterRequest|UpdateCharacterSlots|SetAllyDifficulty|SetEnemyDifficulty|SetContextualReadyState`
   → zero hits.
2. `git show --stat` deletion count on the protocol file consistent with ~200 moved
   lines plus the deleted delegators.
3. Snapshot test unmodified; full suite green (201 pre-existing + new).
4. Changes limited to: `CharacterModule.cs` (new), `LobbyServerProtocol.cs`,
   `CharacterModuleTest.cs` (new), doc 12. (`IClientConnection` must NOT change — that
   is the point of this design.)
