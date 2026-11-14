# 06 – Social Systems

## Groups (`LobbyServer2/LobbyServer/Group/`)

| Class | Notes |
|-------|-------|
| `GroupManager` | Static registry: `ActiveGroups`, `PlayerToGroup`, `GroupRequests` (invites), guarded by a private `_lock`. Create/join/leave/kick/promote/disband, group broadcast helpers. Raises `OnMembersChanged` when a group's members or leader change; calls nothing in matchmaking |
| `GroupInfo` | Group state: id, leader, members, selected sub-types |
| `GroupRequestInfo` | Pending invite/join request with expiry |
| `GroupsTask` | `PeriodicRunner`: pings/expires pending group requests |
| `GroupConfiguration` | Group size limits etc. (part of lobby config) |
| `GroupMessages` | Localization payload factory for the many group-related system messages |

Everyone is always in a group: the group manager creates a solo group when a session
starts (`ISessionRegistry.OnSessionStarting`), quietly, since the player is not online yet;
a player who reconnects leaves their previous group. A join, a leave or a promotion
raises `IGroupRegistry.OnMembersChanged`: matchmaking takes the group out of the queue
and filters its sub types again for the new members or leader (doc 05), then the group
manager refreshes the group with a ready-state reset. (Until 2026-11-14 a promotion only
re-filtered the sub types, so a queued group stayed queued when the mask did not change.)
Client notifications go back through
`LobbyServerProtocol.OnJoinGroup/OnLeaveGroup/OnGroupDisbanded` callbacks and broadcasts,
creating a manager ↔ connection call cycle.

Invite flow is split awkwardly: `GroupModule.HandleGroupInviteRequest` (validation,
blocked users, pings) → `GroupManager.CreateGroupRequest` → recipient's
`GroupModule.HandleGroupConfirmationResponse` (~175 lines) → `GroupManager.JoinGroup`.
The checks shared with custom game invites (doc 04) live in `Utils/InviteRules`: handle
cleanup, self-invite, blocks (an inviter blocked by the invitee gets a fake success), and
the invitee being online.

## Friends (`LobbyServer2/LobbyServer/Friend/`)

- `FriendManager`: friend list assembly (`FriendStatusNotification`), online status,
  friend-list refresh fan-out. Subscribes to session connect/disconnect events.
- `FriendsTask`: periodic refresh.
- Friend mutations (add/accept/block/note) live in
  `LobbyServerProtocol.HandleFriendUpdate`, mutating `SocialComponent` on accounts.

## Chat (`LobbyServer2/LobbyServer/Chat/`)

- `ChatManager` (singleton via `Get()`, instantiated in `CentralServer.Init`): subscribes to
  session events, registers per-connection `OnChatNotification`/`OnGroupChatRequest`.
  Routes global/whisper/group/game chat, blocked-account filtering, dev/mentor handle
  prefixes, chat history persistence (`ChatHistoryDao`), Discord mirroring, muted checks.
- Whisper interception hooks (`RegisterWhisperHandler`) allow admin bots to capture replies.
- `MapPickBanSession`: map pick/ban voting session driven through chat commands.
- `AdminMessageManager`: pending admin messages delivered on login.

## Discord (`LobbyServer2/LobbyServer/Discord/`)

- `DiscordManager` (singleton): channels for game log, lobby chat mirror, admin
  notifications, user/system/client reports. Uses `DiscordClientWrapper` (webhook) and
  `DiscordBotWrapper` (Discord.NET bot for two-way lobby↔Discord chat bridge).
- `DiscordLogAppender`: log4net appender forwarding warnings/errors to a channel.
- Configured via `Config/discordBot.yaml` + `DiscordConfiguration`.

## Trust War (`LobbyServer2/LobbyServer/TrustWar/`)

`TrustWarManager`: seasonal faction competition — per-player faction XP from games, ribbon
selection, global scores in `MiscDao`; toggled by `LobbyConfiguration.IsTrustWarEnabled`.

## Store & inventory

- `StoreManager` (`LobbyServer2/LobbyServer/Store/`): thin — purchase handlers actually live
  in `LobbyServerProtocol`; `EvosStoreConfiguration` (Framework) decides what is free.
- `InventoryManager` (`EvoS.Framework/Server/Inventory/`, namespace `EvoS.DirectoryServer.Inventory`):
  unlock computations (emojis, banners, titles, overcons) used by both directory login
  patching and store logic.
