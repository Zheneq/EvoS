using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using log4net;
using Newtonsoft.Json;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordBotWrapper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordBotWrapper));

        private const string CMD_INFO = "info";
        private const string CMD_BROADCAST = "broadcast";
        private const string CMD_QUEUE_DISABLE = "qoff";
        private const string CMD_QUEUE_ENABLE = "qon";
        
        private readonly DiscordSocketClient botClient;
        private static readonly DiscordSocketConfig discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        private readonly ulong? botChannelId;
        
        public DiscordBotWrapper(DiscordConfiguration conf)
        {
            log.Info("Discord bot is enabled");
            botClient = new DiscordSocketClient(discordConfig);
            if (!conf.BotChannelId.HasValue || conf.BotChannelId == 0)
            {
                botChannelId = null;
            }
            else
            {
                log.Info("Discord bot lobby channel is enabled");
                botChannelId = conf.BotChannelId;
            }
            botClient.Log += Log;
            botClient.Ready += Ready;
            botClient.SlashCommandExecuted += SlashCommandHandler;
            botClient.MessageReceived += ClientOnMessageReceived;
        }

        public async Task Login(DiscordConfiguration conf)
        {
            await botClient.LoginAsync(TokenType.Bot, conf.BotToken);
            await botClient.StartAsync();
            await botClient.SetGameAsync("Atlas Reactor");
        }

        public async Task Ready()
        {
            SlashCommandProperties infoCommand = new SlashCommandBuilder()
                .WithName(CMD_INFO)
                .WithDescription("Get lobby status")
                .Build();

            SlashCommandProperties broadcastCommand = new SlashCommandBuilder()
                .WithName(CMD_BROADCAST)
                .WithDescription("Send a fullscreen notification to all online players")
                .AddOption("message", ApplicationCommandOptionType.String, "Message to send", true)
                .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                .Build();

            SlashCommandProperties queueDisableCommand = new SlashCommandBuilder()
                .WithName(CMD_QUEUE_DISABLE)
                .WithDescription("Pause matchmaking queue")
                .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                .Build();

            SlashCommandProperties queueEnableCommand = new SlashCommandBuilder()
                .WithName(CMD_QUEUE_ENABLE)
                .WithDescription("Unpause matchmaking queue")
                .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                .Build();

            try
            {
                await botClient.CreateGlobalApplicationCommandAsync(infoCommand);
                await botClient.CreateGlobalApplicationCommandAsync(broadcastCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueDisableCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueEnableCommand);
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                log.Error(json);
            }
        }

        private async Task ClientOnMessageReceived(SocketMessage socketMessage)
        {
            // Check if Author is not a bot and allow only reading from the discord LobbyChannel
            if (botChannelId == null
                || socketMessage.Author.IsBot
                || socketMessage.Channel.Id != botChannelId
                || socketMessage.Author.IsWebhook)
            {
                return;
            }

            log.Info($"Discord message from {socketMessage.Author.Username}: {socketMessage.Content}");
            
            ChatNotification message = new ChatNotification
            {
                SenderHandle = $"(Discord) {socketMessage.Author.Username}",
                ConsoleMessageType = ConsoleMessageType.GlobalChat,
                Text = socketMessage.Content,
            };
            foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
            {
                LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                if (player != null && player.CurrentGame == null)
                {
                    player.Send(message);
                }
            }
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            string handle = $"{command.User.Username}#{command.User.Discriminator}";
            switch (command.Data.Name)
            {
                case CMD_INFO:
                {
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    DiscordLobbyUtils.Status status = DiscordLobbyUtils.GetStatus();
                    await command.RespondAsync(embed:
                        new EmbedBuilder
                        {
                            Title = DiscordLobbyUtils.BuildPlayerCountSummary(status),
                            Color = Color.Green
                        }.Build(), ephemeral: true);
                    break;
                }
                case CMD_BROADCAST:
                {
                    string msg = command.Data.Options.First().Value.ToString();
                    log.Info($"CMD /{command.Data.Name} - {handle}: {msg}");
                    ChatNotification message = new ChatNotification
                    {
                        SenderHandle = handle,
                        ConsoleMessageType = ConsoleMessageType.BroadcastMessage,
                        Text = msg,
                    };
                    SessionManager.Broadcast(message);
                    await command.RespondAsync($"Broadcast: {msg}", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_DISABLE:
                {
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    MatchmakingManager.Enabled = false;
                    await command.RespondAsync("Matchmaking queue is paused", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_ENABLE:
                {
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    MatchmakingManager.Enabled = true;
                    await command.RespondAsync("Matchmaking queue is unpaused", ephemeral: true);
                    break;
                }
            }
        }

        private static Task Log(LogMessage msg)
        {
            return DiscordUtils.Log(log, msg);
        }

        public Task<IUserMessage> SendMessageAsync(
            string text = null,
            bool isTTS = false,
            Embed embed = null,
            RequestOptions options = null,
            AllowedMentions allowedMentions = null,
            MessageReference messageReference = null,
            MessageComponent components = null,
            ISticker[] stickers = null,
            Embed[] embeds = null,
            MessageFlags flags = MessageFlags.None,
            ulong? channelIdOverride = null)
        {
            ulong? _channelId = channelIdOverride ?? botChannelId;
            if (_channelId.Value == 0) return null;
            IMessageChannel chnl = botClient.GetChannel(_channelId.Value) as IMessageChannel;
            return chnl.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);
        }
    }
}