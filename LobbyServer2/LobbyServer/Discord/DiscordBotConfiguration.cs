using System.Collections.Generic;
using CentralServer.LobbyServer.Utils;

namespace CentralServer.LobbyServer.Discord;

public class DiscordBotConfiguration
{
    private static readonly ReloadableConfig<DiscordBotConfiguration> Config =
        new("discordBot.yaml");

    public bool Enabled = false;
    public string BotToken = "";
    public ulong? BotChannelId;
    public ulong? RequestChannelId;
    public ulong? AdminNotificationChannelId;

    // Maps a Discord user ID to the admin's in-game account ID for users allowed to invoke
    // management commands (broadcast, qoff, qon, approve, decline). The account ID is recorded as
    // IssuedBy when the admin approves a username request via bot commands (0 if unknown).
    // When empty, the commands fall back to Discord's ManageGuild permission gate only.
    public Dictionary<ulong, long> AdminUserIds = new();

    public static DiscordBotConfiguration Get() => Config.Get();
}