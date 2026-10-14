# 08 – Framework & Wire Protocol

`EvoS.Framework` (~544 files) is mostly a port of the original game's data model and
network protocol; the majority of files are passive DTOs. The genuinely shared *logic* is a
small fraction (auth, data access, config, a few utilities).

## Network (`EvoS.Framework/Network/`)

| Area | Contents |
|------|----------|
| `NetworkMessages/` (165 files) | Client↔lobby message classes (`RegisterGameClientRequest`, `GameInfoNotification`, purchase requests, ...). Marked with `[EvosMessage(id)]` attributes |
| `Static/` (120 files) | Persisted/shared data structures from the game: `PersistedAccountData` and its components (`AccountComponent`, `CharacterComponent`, `ExperienceComponent`, `AdminComponent`, `SocialComponent`, ...), `LobbyGameInfo`, `LobbyServerPlayerInfo`, `GameSubType`, `EloValues`, plus `BinarySerializer` |
| `EvosSerializer.cs` | Reflection-based binary serializer replicating the game's wire format; discovers `[EvosMessage]` types at startup (this is why Docker publish must not trim) |
| `WebSocket/` | `WebSocketMessage` base + response base for the lobby protocol |
| `Unity/` (10 files) | Minimal ports of Unity networking primitives (`NetworkReader/Writer`, hlapi bits) needed by the wire format |
| `ProtocolVersion.cs` | Supported client protocol versions |

Bridge (lobby↔game server) messages are separate:
`LobbyServer2/BridgeServer/Messages/` + `BridgeMessageSerializer`, sharing
`AllianceMessageBase` conventions with the game.

## Queue requirements & protocol versions

`QueueRequirement` subclasses (`Network/Static/QueueRequirement_*.cs`) gate game types and
subtypes on the client. `RequirementCollection` stores each requirement as NetSerializer bytes
using the type list in `QueueRequirement.MessageTypes`. A client whose list differs cannot
deserialize the collection.

- `QueueRequirement_ProtocolDependent` is the abstract base for requirements only some clients
  understand. Each subclass declares `SupportedProtocolVersions` (per class, not serialized).
- `RequirementCollection.ForProtocolVersion(pv)` replaces unsupported ones with
  `QueueRequirement_Never` (AdminDisabled) and returns the same collection when nothing changes.
  A null protocol version is logged as an error with a stack trace.
- `ProtocolPatcher` (`LobbyServer2/LobbyServer/`) applies this to every client-bound message that
  can carry requirements: from `LobbyServerProtocol.SendImpl` with the client's session protocol
  version, and from `BroadcastImpl` with null (one payload must be safe for every client).
  Copy-on-write: shared subtypes are never modified. `ProtocolPatcherTest` fails if a message
  type that can carry a `RequirementCollection` is missing from `ProtocolPatcher.Mapping`.

Adding a concrete protocol-dependent requirement:
1. Add it to the **start** of `QueueRequirement.MessageTypes`. Each serialized requirement begins
   with a NetSerializer type ID (e.g. `QueueRequirement_Never` is `05-12`: type 5, AdminDisabled).
   These IDs are not the `[EvosMessage]` ones: NetSerializer assigns them at runtime, walking the
   list from the end and numbering each type along with the field types it pulls in. Appending a
   type renumbers every existing requirement and breaks vanilla clients; prepending leaves their
   IDs unchanged. The supporting clients must have the same list as the server.
2. Give it an `[EvosMessage]` ID.
3. Handle it in `QueueRequirement.ExtractRequirementFromReader` if it should be loadable from JSON.

## Game data (`EvoS.Framework/GameData/`)

Static singletons loading JSON snapshots of game content:
`GameWideData` (from `LobbyServer2/Config/GameData/GameWideData.json`), `CharacterResourceLink`
& skins/taunts/colors, `GGPackData`, `LootMatrixPackData`, `BannedWords`, `MapData`, keybinds.
Accessed via `X.Get()` everywhere (character validation, store pricing, map selection).

## Constants (`EvoS.Framework/Constants/Enums/`, 65 files)

Game enums: `CharacterType`, `GameType`, `GameStatus`, `GameResult`, `Team`, `ActionType`, currency, etc.
`GameStatus` ordering is load-bearing (`game.GameStatus is >= GameStatus.Launched and < GameStatus.Stopped`).

## Misc (`EvoS.Framework/Misc/`)

`CompilerExtensions` (IsNullOrEmpty etc.), `DefaultJsonSerializer`, `LogRedaction`
(masks sensitive fields in logged JSON — tested), `Loc/` (I2 localization port),
`GameBalanceVars`, quest/reward utilities, `Mathf`/`ColorRGBA` Unity ports.

## Misplaced logic (namespace ≠ location)

These live in Framework but declare DirectoryServer/CentralServer namespaces — evidence of
past moves without cleanup, and of Framework absorbing app logic:

- `DataAccess/LoginManager.cs` → `EvoS.DirectoryServer.Account`
- `DataAccess/AccountManager.cs` → `EvoS.DirectoryServer.Account`
- `Character/CharacterManager.cs` → `EvoS.DirectoryServer.Character`
- `Server/Inventory/InventoryManager.cs` → `EvoS.DirectoryServer.Inventory`
- `Auth/EvosAuth.cs`, `Auth/SteamWebApiConnector.cs` → partially `EvoS.DirectoryServer`
- Two files in `CentralServer.LobbyServer.Config` namespaces (e.g. lobby config types used
  by `EvosConfiguration`)

Consequence: the dependency direction is muddled — Framework (bottom layer) contains code
that conceptually belongs to the apps above it, and `LobbyServer2` has a second
`CharacterManager` (`CentralServer.LobbyServer.Character`) shadowing the Framework one
(disambiguated by using-aliases at call sites).
