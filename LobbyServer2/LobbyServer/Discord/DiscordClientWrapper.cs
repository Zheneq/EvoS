using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Session;
using Discord;
using Discord.Net;
using Discord.Webhook;
using Discord.WebSocket;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using log4net;
using Newtonsoft.Json;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordClientWrapper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordClientWrapper));
        
        private readonly DiscordWebhookClient client;
        private readonly DiscordSocketClient botClient;
        private static readonly DiscordSocketConfig discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        private readonly ulong? botChannelId;
        private readonly ulong? threadId;
        
        public DiscordClientWrapper(DiscordChannel conf)
        {
            client = new DiscordWebhookClient(conf.Webhook);
            client.Log += Log;
            threadId = conf.ThreadId;
        }

        public DiscordClientWrapper(DiscordConfiguration conf)
        {
            botClient = new DiscordSocketClient(discordConfig);
            botChannelId = conf.BotChannelId;
            botClient.LoginAsync(TokenType.Bot, conf.BotToken);
            botClient.StartAsync();
            botClient.SetGameAsync("Atlas Reactor");
            botClient.Log += Log;
            botClient.Ready += Ready;
            botClient.SlashCommandExecuted += SlashCommandHandler;
            botClient.MessageReceived += ClientOnMessageReceived;
        }

        public async Task Ready()
        {
            SlashCommandBuilder infoCommand = new SlashCommandBuilder();
            infoCommand.WithName("info");
            infoCommand.WithDescription("Retrieve info from Atlas Reactor");

            SlashCommandBuilder broadcastCommand = new SlashCommandBuilder();
            broadcastCommand.WithName("broadcast");
            broadcastCommand.WithDescription("Send a broadcast to atlas reactor lobby");
            broadcastCommand.AddOption("message", ApplicationCommandOptionType.String, "Message to send", true);
            broadcastCommand.WithDefaultMemberPermissions(GuildPermission.ManageGuild);

            try
            {
                await botClient.CreateGlobalApplicationCommandAsync(infoCommand.Build());
                await botClient.CreateGlobalApplicationCommandAsync(broadcastCommand.Build());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                log.Info(json);
            }
        }

        private async Task ClientOnMessageReceived(SocketMessage socketMessage)
        {
            await Task.Run(() =>
            {
                // Check if Author is not a bot and allow only reading from the discord LobbyChannel
                if (!socketMessage.Author.IsBot && socketMessage.Channel.Id == botChannelId && !socketMessage.Author.IsWebhook)
                {
                    ChatNotification message = new ChatNotification
                    {
                        SenderHandle = $"(Discord) {socketMessage.Author.Username}",
                        ConsoleMessageType = ConsoleMessageType.GlobalChat,
                        Text = socketMessage.Content,
                    };
                    foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
                    {
                        LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                        if (player != null && player.CurrentServer == null)
                        {
                            player.Send(message);
                        }
                    }
                }
            });
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.Data.Name == "info")
            {
                DiscordLobbyUtils.Status status = DiscordLobbyUtils.GetStatus();
                await command.RespondAsync(embed:
                                new EmbedBuilder
                                {
                                    Title = DiscordLobbyUtils.BuildPlayerCountSummary(status),
                                    Color = Color.Green
                                }.Build(), ephemeral: true);
            }
            if (command.Data.Name == "broadcast")
            {
                ChatNotification message = new ChatNotification
                {
                    SenderHandle = command.User.Username,
                    ConsoleMessageType = ConsoleMessageType.BroadcastMessage,
                    Text = command.Data.Options.First().Value.ToString(),
                };
                foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
                {
                    LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                    if (player != null)
                    {
                        player.Send(message);
                    }
                }
                await command.RespondAsync("Broadcast send", ephemeral: true);
            }
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