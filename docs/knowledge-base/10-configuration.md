# 10 – Configuration

## Layers

| Source | Loaded by | Contents | Reloadable |
|--------|-----------|----------|------------|
| `Config/settings.yaml` | `EvosConfiguration` (`EvoS.Framework/EvosConfiguration.cs`) | Ports, addresses, DB, auth keys, registration policy, game-server executable/pick order | No (static `Lazy`, read once from CWD) |
| `Config/lobby.yaml` | `LobbyConfiguration` (`LobbyServer2/LobbyServer/Config/LobbyConfiguration.cs`) | Motd/messages, group sizes, GG/shutdown timings, Trust War toggle, server reserve size | No |
| `Config/discordBot.yaml` | `DiscordBotConfiguration` (`LobbyServer2/LobbyServer/Discord/DiscordBotConfiguration.cs`) | Bot token, channel IDs, admin user IDs, welcome message | **Yes** (file-watch + debounce via `ReloadableConfig`) |
| `Config/proxy.yaml` | `ProxyConfiguration` (`LobbyServer2/Proxy/`) | Known proxies → alternate lobby addresses | **Yes** (via `POST /api/admin/proxy/reload`) |
| `Config/storeSettings.yaml` | `EvosStoreConfiguration` (Framework) | What's free/unlocked by default | No |
| `Config/Matchmaking/PvP.json` | `MatchmakingConfigBundle` (read by `MatchmakingQueue`) | Matchmaker weights per sub-type | **Yes** (re-read on every matchmaking cycle) |
| `Config/GameSubTypes/*` | game mode definitions (`GameModeManager`) | Sub-type composition | — |
| `Config/GameData/GameWideData.json`, `defaultEquip.json` | `GameWideData` etc. | Game content snapshot | No |
| `AtlasReactorConfig.json` (repo root) | game client/server | Points client at directory server | — |

## Patterns

- `EvosConfiguration` exposes ~35 static getters (`GetLobbyServerPort()` etc.) over a
  YAML-deserialized instance; startup validation (`ValidateConfiguration`) enforces
  DevMode-vs-persistent-DB exclusivity and API-key strength (min 32 chars). Tests
  (`EvosConfigurationTest`) target the internal validation overloads because the static
  instance itself can't be substituted.
- `ReloadableConfig` (`LobbyServer2/LobbyServer/Utils/ReloadableConfig.cs`) is the modern
  pattern: file-watcher, debounce, retries, registry with `ShutdownAll` on exit. Currently
  used by `DiscordBotConfiguration`; matchmaking config pre-dates it and re-reads on every
  Update() cycle instead.
- `DiscordConfiguration` (webhooks, channel handles) is a distinct POJO embedded inside
  `LobbyConfiguration` — its settings live in `lobby.yaml`, not `discordBot.yaml`.
- `DebugParameters` (Framework Misc) — runtime-tweakable debug flags.

## Concerns

- Config access is static and read from the *current working directory* — processes must be
  launched from the right folder; tests need real files or must avoid touching config paths.
- Five different config file formats/loaders with inconsistent reload semantics.
- Some knobs live in code (e.g. matchmaking queue map in `MatchmakingManager`).
