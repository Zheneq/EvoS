namespace CentralServer.LobbyServer.Discord
{
    public class DiscordConfiguration
    {
        public bool Enabled = false;

        public string BotToken = "";

        public ulong? AdminChannel;
        public ulong? GameLogChannel;
        public ulong? LobbyChannel;

        public bool AdminEnableUserReports;
        public ulong? AdminUserReportChannelId;
        public bool AdminEnableChatAudit;
        public ulong? AdminChatAuditChannelId;
        
        public bool LobbyEnableChat;
        public bool LobbyEnableServerStatus;
        public int LobbyChannelUpdatePeriodSeconds = 300;
        public bool LobbyChannelUpdateOnChangeOnly = true;
    }
}
