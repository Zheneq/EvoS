﻿using System.Xml.XPath;
using WebSocketSharp;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordConfiguration
    {
        public bool Enabled = false;
        
        public DiscordChannel AdminChannel;
        public DiscordChannel GameLogChannel;
        public DiscordChannel LobbyChannel;

        public string BotToken = "";
        public ulong? BotChannelId;

        public bool AdminEnableUserReports;
        public ulong? AdminUserReportThreadId;
        public bool AdminEnableChatAudit;
        public ulong? AdminChatAuditThreadId;
        public bool AdminEnableLog;
        public ulong? AdminLogThreadId;
        public bool AdminEnableAdminAudit = true;
        
        public bool LobbyEnableChat;
        public bool LobbyEnableServerStatus;
        public int LobbyChannelUpdatePeriodSeconds = 300;
        public bool LobbyChannelUpdateOnChangeOnly = true;
    }

    public class DiscordChannel
    {
        public string Webhook = "";
        public ulong? ThreadId = null;
        public ulong? PingRoleId = null;
        public string? PingRoleHandle = null;
    }

    public static class DiscordConfigExtensions
    {
        public static bool IsChannel(this DiscordChannel channel)
        {
            return channel != null
                   && channel.Webhook != null
                   && EvoS.Framework.Extensions.MaybeUri(channel.Webhook);
        }
    }
}
