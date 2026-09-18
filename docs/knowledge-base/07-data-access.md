# 07 – Data Access

## Architecture

Clean three-layer DAO pattern in `EvoS.Framework/DataAccess/`:

```
DB (singleton, DB.Get())
 ├─ interface per aggregate:   Daos/*Dao.cs          (AccountDao, LoginDao, MatchHistoryDao, ...)
 ├─ Mongo implementations:     Mongo/*MongoDao.cs    (via MongoDB.Driver; MongoDB.cs builds the client)
 ├─ In-memory implementations: Mock/*MockDao.cs      (used when Database.Type = None; no persistence)
 └─ Caching decorators:        Daos/*DaoCached.cs    (wrap either impl; LRU/dictionary caches)
```

`DB`'s constructor wires the graph based on `EvosConfiguration.GetDBConfig().Type`
(`Mongo` | `None`). The Mock DAOs double as test fakes and as the "no database" runtime mode.

## Aggregates

| DAO | Data |
|-----|------|
| `AccountDao` | `PersistedAccountData` — the big player aggregate (account/character/experience/bank/social/admin components, from the original game's data model). Partial-update methods like `UpdateAdminComponent` exist alongside whole-account `UpdateAccount` |
| `LoginDao` | Username → accountId + password hash + linked accounts |
| `MatchHistoryDao` | Per-player match history entries |
| `GameServerKeyDao` | Game-server public-key approval records |
| `RegistrationCodeDao` | Invite codes |
| `AdminMessageDao`, `ChatHistoryDao`, `UserFeedbackDao`, `ClientErrorDao`, `ClientErrorReportDao`, `UserMetadataDao`, `MiscDao` | Moderation, telemetry, misc key-value (Trust War scores etc.) |

`AccountManager` (`DataAccess/AccountManager.cs`, namespace `EvoS.DirectoryServer.Account`)
creates default accounts; distinct from `LoginManager` which owns credentials.

## Caching

`*DaoCached` decorators keep hot entities in memory (accounts are read constantly by chat,
friends, matchmaking). Consequence: **`GetAccount` returns a shared mutable object** — code
all over the lobby mutates the cached instance and then calls `UpdateAccount`/`Update*`;
there is no unit-of-work or concurrency control beyond convention.

## Concerns

- `DB.Get()` is referenced from ~everywhere (handlers, managers, Framework, DirectoryServer),
  making most logic untestable without either Mongo or the process-wide mock mode.
- The **memory note** (`flaky-log4net-test-teardown`) records a past race in `DB.Get()`
  affecting tests — singletons initialized lazily under concurrency are fragile.
- Schema migration is ad-hoc: `DirectoryServer.PatchAccountData` runs on every login and
  rewrites the account unconditionally.
- Tests exist for a few DAOs (`Tests/DataAccess/*`: mongo DAOs run against a test framework
  in `DbTestFramework.cs`, cached decorators tested in isolation).
