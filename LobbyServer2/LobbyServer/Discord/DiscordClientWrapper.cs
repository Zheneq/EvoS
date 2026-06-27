using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;
using DiscordHttpException = Discord.Net.HttpException;
using EvoS.Framework.Misc;
using log4net;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordClientWrapper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordClientWrapper));

        private readonly DiscordWebhookClient client;
        private readonly ulong? threadId;
        private readonly ulong? pingRoleId;
        private readonly string pingRoleHandle;
        private readonly int retryCount;
        private readonly int retryDelayMs;

        public DiscordClientWrapper(DiscordChannel conf, int retryCount, int retryDelayMs)
        {
            client = new DiscordWebhookClient(conf.Webhook);
            client.Log += Log;
            threadId = conf.ThreadId;
            pingRoleId = conf.PingRoleId;
            pingRoleHandle = conf.PingRoleHandle;
            this.retryCount = retryCount;
            this.retryDelayMs = retryDelayMs;
        }

        private static Task Log(LogMessage msg) => DiscordUtils.Log(log, msg);

        private async Task<T> WithRetry<T>(Func<Task<T>> action)
        {
            for (int attempt = 0;; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception e) when (attempt < retryCount && IsRetryable(e))
                {
                    log.Warn($"Discord API call failed (attempt {attempt + 1}/{retryCount + 1}), retrying in {retryDelayMs}ms: {e.Message}");
                    await Task.Delay(retryDelayMs);
                }
            }
        }

        private static bool IsRetryable(Exception e) => e switch
        {
            DiscordHttpException http =>
                http.HttpCode == HttpStatusCode.TooManyRequests || (int)http.HttpCode >= 500,
            HttpRequestException => true,
            TimeoutException => true,
            _ => false
        };

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

            if (pingRoleId != null)
            {
                text = text?
                    .Replace("@here", $"<@&{pingRoleId}>")
                    .Replace("@everyone", $"<@&{pingRoleId}>");

                if (!pingRoleHandle.IsNullOrEmpty())
                {
                    text = text?.Replace($"@{pingRoleHandle}", $"<@&{pingRoleId}>");
                }
            }

            return WithRetry(() => client.SendMessageAsync(
                text,
                isTTS,
                embeds,
                username,
                avatarUrl,
                options,
                allowedMentions,
                components,
                flags,
                _threadId));
        }

        public Task<ulong> SendFileAsync(
            FileAttachment attachment,
            string text = null,
            bool isTTS = false,
            IEnumerable<Embed> embeds = null,
            string username = null,
            string avatarUrl = null,
            RequestOptions options = null,
            ulong? threadIdOverride = null)
        {
            ulong? _threadId = threadIdOverride ?? threadId;
            if (_threadId == 0) _threadId = null;
            return WithRetry(() => client.SendFileAsync(
                attachment,
                text,
                isTTS,
                embeds,
                username,
                avatarUrl,
                options,
                threadId: _threadId));
        }
    }
}