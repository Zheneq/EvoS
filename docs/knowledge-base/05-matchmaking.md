# 05 – Matchmaking

## Responsibilities

Queue groups per game type, periodically try to assemble balanced matches, compute/update
Elo, and apply queue penalties (dodging, leaving) and priorities.

## Key classes

| Class | Location | Notes                                                                                                                                                                                                                                                                        |
|-------|----------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `MatchmakingManager` | `Matchmaking/MatchmakingManager.cs` | Static facade: queue map (`Coop`, `PvP` active; Practice/Ranked/Custom commented out), add/remove group, `StartGameAsync`, global `Enabled` flag                                                                                                                             |
| `MatchmakingTask` | `Matchmaking/MatchmakingTask.cs` | `PeriodicRunner` calling `MatchmakingManager.Update()`                                                                                                                                                                                                                       |
| `MatchmakingQueue` | `Matchmaking/MatchmakingQueue.cs` | 788 lines. Per-game-type queue: queued groups w/ timestamps, sub-type mask handling, asymmetric sub-types (e.g. one player controlling 3 characters while others control one each), config hot-reload, match scoring across sub-types, queue-status notifications to clients |
| `Matchmaker` (abstract) | `Matchmaking/Matchmaker.cs` | Contract: `GetMatchesRanked(queuedGroups)` → scored matches; defines `MatchmakingGroup`, `Match`, `ScoredMatch`                                                                                                                                                              |
| `MatchmakerBase` / `MatchmakerRanked` / `MatchmakerFifo` / `MatchmakerSingleGroup` | `Matchmaking/` | Strategies. `MatchmakerRanked` is Elo-balance-scored with many weighted criteria from `MatchmakingConfiguration`; **takes `AccountDao` + config `Func` via constructor** (the testable pattern), with a convenience ctor defaulting to `DB.Get()`                            |
| `Elo` | `Matchmaking/Elo.cs` | Static, but parameterized with `IAccountProvider`/`IMatchHistoryProvider`/`IAccountUpdater` delegates (`EvoS.Framework/DataAccess/EvosDelegates.cs`) — testable                                                                                                              |
| `QueuePenaltyManager` | `Matchmaking/QueuePenaltyManager.cs` | Leave/dodge penalties and pardons (see [Queue penalties](#queue-penalties)), persisted in account admin component; raises `OnPenalty`/`OnPardon` for the Discord audit                                                                                                      |
| `QueuePriorityManager` | `Matchmaking/QueuePriorityManager.cs` | Priority windows (e.g. compensating players whose match was dodged)                                                                                                                                                                                                          |
| `MatchmakingConfiguration` / `MatchmakingConfigBundle` | `Matchmaking/` | Per-sub-type tunables loaded from `LobbyServer2/Config/Matchmaking/PvP.json` (hot-reloadable)                                                                                                                                                                                |

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

## Queue penalties

`QueuePenaltyManager` penalizes wasting other players' time by leaving a PvP game (Draft is a
PvP sub-type with `RankedFreelancerSelection`). Switched by `MatchAbandoningPenalty` in
`lobby.yaml`. State lives in `AdminComponent.ActiveQueuePenalties[PvP]` (block timeout,
offense count, parole timeout); `CheckQueuePenalties` refuses queueing for the player and
tells their group.

**When leaving is penalized** — `IssueQueuePenalties`, called from `Game.OnPlayerDisconnect`
(the game server reports a lost connection) and `Game.CancelMatch` (the player whose
disconnect/AFK cancels the match):

- Normal PvP: only once the match has started. `Game.MatchStarted` is set when the game server
  reports `Started`, i.e. every player has loaded or timed out loading. `GameStatus` can't be
  used for this: the lobby sets `Started` itself right after launching. Leaving character
  select or dropping while loading is free — more likely a technical issue than malice.
- Draft: also during the draft itself (`FreelancerSelecting`…`Launching`), but not while loading.
- Collapse: once `QueuePenaltyCollapseThreshold` (default 2) other players are replaced with
  bots, further leavers are not penalized.
- One offense per game: leaving the same game again (after reconnecting) re-applies the block
  recorded in `Game.PenalizedPlayers`, without escalating.
- Right after the game ended (until `Game.StopTime`, 8 s after the summary): a short
  non-offense block of up to 30 s, only if the game has a result.

**Escalation** — `EvaluatePenalty` (pure, unit-tested): base duration
(`QueuePenaltyPvPBaseDuration` 200 s, `QueuePenaltyDraftBaseDuration` 5 min) ×
`QueuePenaltyEscalationRatio` (4) ^ (offense − 1), capped at `QueuePenaltyEscalationCap`
(24 h). The offense count resets once `QueuePenaltyParoleWindow` (7 days) passes without a
new offense.

**Pardons** — `PardonQueuePenalties` forgives players penalized in that game (each once): the
offense no longer counts, and the block is cut to 15 s. Reasons (`PardonReason`):

- `EveryoneLeft`: every human (not bots or proxies) left a running match, so it broke rather
  than was abandoned.
- `NoResult`: a started match ended without a win or a tie (`Game.HasResult`) — the game server
  was lost (no summary, recorded as a tie), an admin ended it with no result
  (`Game.AdminEndGame(GameResult.NoResult)`, sent to the game server as a tie), or the lobby
  shut down (`GameManager.StopAllGames`). Canceled matches never started, so their dodger stays
  penalized.
- `CameBack`: the game ended with a result, and the leaver had reconnected and stayed until
  the end.

A pardon can't restore the parole window that the pardoned offense refreshed.

## Observations

- This subsystem has the clearest seams in the codebase: matchmaker strategies and Elo are
  already dependency-injected and covered by tests (`MatchmakerTest`, `EloTest`,
  `QueuePenaltyManagerTest`, `QueuePenaltyIssueTest`, `QueuePriorityManagerTest`).
- `MatchmakingQueue` still talks directly to `SessionManager`/`GroupManager`/`ServerManager`
  statics and composes client notifications itself — queue logic and notification fan-out
  are entangled.
- Group data for matchmaking is pulled through `GroupManager` statics; queue holds group ids
  and re-resolves membership each tick (robust to group edits, but O(n) lookups everywhere).
