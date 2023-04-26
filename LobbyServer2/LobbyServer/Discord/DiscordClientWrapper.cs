using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;
using log4net;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordClientWrapper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordClientWrapper));
        
        private readonly DiscordWebhookClient client;
        private readonly ulong? threadId;
        
        public DiscordClientWrapper(DiscordChannel conf)
        {
            client = new DiscordWebhookClient(conf.Webhook);
            client.Log += Log;
            threadId = conf.ThreadId;
        }

        private static Task Log(LogMessage msg)
        {
            return DiscordUtils.Log(log, msg);
        }
        
        public Task<ulong> SendMessageAsync(
            string text = null,
            bool isTTS = false,
            IEnumerable<Embed> embeds = null,
            string username = null,
            string avatarUrl = null,
            RequestOptions options = null,
            AllowedMentions allowedMentions = null,
            MessageComponent components = null,
            MessageFlags flags = MessageFlags.None,
            ulong? threadIdOverride = null)
        {
            ulong? _threadId = threadIdOverride ?? threadId;
            if (_threadId == 0) _threadId = null;
            return client.SendMessageAsync(
                text, isTTS, embeds, username, avatarUrl, options, allowedMentions, components, flags, _threadId);
        }
    }
}