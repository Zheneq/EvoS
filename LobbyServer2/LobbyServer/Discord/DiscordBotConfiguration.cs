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

    // Discord user IDs allowed to invoke management commands (broadcast, qoff, qon).
    // When empty, the commands fall back to Discord's ManageGuild permission gate only.
    public HashSet<ulong> AdminUserIds = new();

    public static DiscordBotConfiguration Get() => Config.Get();
}