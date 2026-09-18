# 05 – Matchmaking

## Responsibilities

Queue groups per game type, periodically try to assemble balanced matches, compute/update
Elo, and apply queue penalties (dodging, leaving) and priorities.

## Key classes

| Class | Location | Notes |
|-------|----------|-------|
| `MatchmakingManager` | `Matchmaking/MatchmakingManager.cs` | Static facade: queue map (`Coop`, `PvP` active; Practice/Ranked/Custom commented out), add/remove group, `StartGameAsync`, global `Enabled` flag |
| `MatchmakingTask` | `Matchmaking/MatchmakingTask.cs` | `PeriodicRunner` calling `MatchmakingManager.Update()` |
| `MatchmakingQueue` | `Matchmaking/MatchmakingQueue.cs` | 788 lines. Per-game-type queue: queued groups w/ timestamps, sub-type mask handling, asymmetric sub-types (e.g. fourlancer), config hot-reload, match scoring across sub-types, queue-status notifications to clients |
| `Matchmaker` (abstract) | `Matchmaking/Matchmaker.cs` | Contract: `GetMatchesRanked(queuedGroups)` → scored matches; defines `MatchmakingGroup`, `Match`, `ScoredMatch` |
| `MatchmakerBase` / `MatchmakerRanked` / `MatchmakerFifo` / `MatchmakerSingleGroup` | `Matchmaking/` | Strategies. `MatchmakerRanked` is Elo-balance-scored with many weighted criteria from `MatchmakingConfiguration`; **takes `AccountDao` + config `Func` via constructor** (the testable pattern), with a convenience ctor defaulting to `DB.Get()` |
| `Elo` | `Matchmaking/Elo.cs` | Static, but parameterized with `IAccountProvider`/`IMatchHistoryProvider`/`IAccountUpdater` delegates (`EvoS.Framework/DataAccess/EvosDelegates.cs`) — testable |
| `QueuePenaltyManager` | `Matchmaking/QueuePenaltyManager.cs` | Dodge/leave penalties, escalating durations, persisted in account admin component |
| `QueuePriorityManager` | `Matchmaking/QueuePriorityManager.cs` | Priority windows (e.g. compensating players whose match was dodged) |
| `MatchmakingConfiguration` / `MatchmakingConfigBundle` | `Matchmaking/` | Per-sub-type tunables loaded from `LobbyServer2/Config/Matchmaking/PvP.json` (hot-reloadable) |

## Flow

1. Client ready-up → `HandleJoinMatchmakingQueueRequest` → `MatchmakingManager.AddGroupToQueue`
   (penalty check is re-done here as the single choke point) → notifications to group members.
2. Every tick: `MatchmakingQueue.Update()` → `TryMatch`:
   - `GetAndConvertQueuedGroups` per sub-type (respecting each group's sub-type mask and
     asymmetric descriptors),
   - each sub-type's `Matchmaker` scores candidate matches,
   - `StartBestMatch` picks the globally best `ScoredMatchWithSubType`, removes the groups,
     and calls `MatchmakingManager.StartGameAsync` (checks `ServerManager` availability first).
3. Post-game: `MatchmakingQueue.OnGameEnded` → `Elo.OnGameEnded` updates per-character and
   composite Elo values (`ELOKeyComponent` / `EloValues` in Framework).

## Observations

- This subsystem has the clearest seams in the codebase: matchmaker strategies and Elo are
  already dependency-injected and covered by tests (`MatchmakerTest`, `EloTest`,
  `QueuePenaltyManagerTest`, `QueuePriorityManagerTest`).
- `MatchmakingQueue` still talks directly to `SessionManager`/`GroupManager`/`ServerManager`
  statics and composes client notifications itself — queue logic and notification fan-out
  are entangled.
- Group data for matchmaking is pulled through `GroupManager` statics; queue holds group ids
  and re-resolves membership each tick (robust to group edits, but O(n) lookups everywhere).
