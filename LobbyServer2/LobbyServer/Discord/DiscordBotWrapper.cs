using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using EvoS.DirectoryServer.Account;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
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
        private const string CMD_REQUEST_NAME = "register";
        private const string CMD_GET_CODE = "code";

        private readonly DiscordSocketClient botClient;
        private static readonly DiscordSocketConfig discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        private readonly ulong? botChannelId;
        private readonly ulong? requestChannelId;

        public DiscordBotWrapper(DiscordBotConfiguration conf)
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
            requestChannelId = conf.RequestChannelId is null or 0 ? null : conf.RequestChannelId;
            botClient.Log += Log;
            botClient.Ready += Ready;
            botClient.SlashCommandExecuted += SlashCommandHandler;
            botClient.MessageReceived += ClientOnMessageReceived;
        }

        public async Task Login(DiscordBotConfiguration conf)
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

            SlashCommandProperties requestNameCommand = new SlashCommandBuilder()
                .WithName(CMD_REQUEST_NAME)
                .WithDescription("Request a username to register an account")
                .AddOption("name", ApplicationCommandOptionType.String, "The username you want", true)
                .Build();

            SlashCommandProperties getCodeCommand = new SlashCommandBuilder()
                .WithName(CMD_GET_CODE)
                .WithDescription("Get the registration code for your approved username request")
                .Build();

            try
            {
                await botClient.CreateGlobalApplicationCommandAsync(infoCommand);
                await botClient.CreateGlobalApplicationCommandAsync(broadcastCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueDisableCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueEnableCommand);
                await botClient.CreateGlobalApplicationCommandAsync(requestNameCommand);
                await botClient.CreateGlobalApplicationCommandAsync(getCodeCommand);
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                log.Error(json);
            }
        }

        private Task ClientOnMessageReceived(SocketMessage socketMessage)
        {
            // Check if Author is not a bot and allow only reading from the discord LobbyChannel
            if (botChannelId == null
                || socketMessage.Author.IsBot
                || socketMessage.Channel.Id != botChannelId
                || socketMessage.Author.IsWebhook)
            {
                return Task.CompletedTask;
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

            return Task.CompletedTask;
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            string handle = $"{command.User.Username} ({command.User.Id})";
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
                    if (await IsNotAdmin(command, handle)) break;
                    string msg = command.Data.Options.First().Value.ToString();
                    log.Info($"CMD /{command.Data.Name} - {handle}: {msg}");
                    ChatManager.Get().Broadcast(msg);
                    await command.RespondAsync($"Broadcast: {msg}", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_DISABLE:
                {
                    if (await IsNotAdmin(command, handle)) break;
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    MatchmakingManager.Enabled = false;
                    await command.RespondAsync("Matchmaking queue is paused", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_ENABLE:
                {
                    if (await IsNotAdmin(command, handle)) break;
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    MatchmakingManager.Enabled = true;
                    await command.RespondAsync("Matchmaking queue is unpaused", ephemeral: true);
                    break;
                }
                case CMD_REQUEST_NAME:
                {
                    await HandleRequestName(command, handle);
                    break;
                }
                case CMD_GET_CODE:
                {
                    await HandleGetCode(command, handle);
                    break;
                }
            }
        }

        // Extra defense-in-depth on top of the command's ManageGuild permission gate.
        // When the allowlist is empty, we rely solely on that gate and let the command through.
        private async Task<bool> IsNotAdmin(SocketSlashCommand command, string handle)
        {
            HashSet<ulong> adminUserIds = DiscordBotConfiguration.Get().AdminUserIds ?? new HashSet<ulong>();
            if (adminUserIds.Count == 0 || adminUserIds.Contains(command.User.Id))
            {
                return false;
            }

            log.Warn($"Rejected management command /{command.Data.Name} from non-admin {handle} ({command.User.Id})");
            await command.RespondAsync("You are not allowed to use this command.", ephemeral: true);
            return true;
        }

        private async Task<bool> IsWrongChannel(SocketSlashCommand command)
        {
            if (requestChannelId.HasValue && command.ChannelId != requestChannelId)
            {
                await command.RespondAsync(
                    $"Please use this command in <#{requestChannelId}>.",
                    ephemeral: true);
                return true;
            }

            return false;
        }

        private async Task HandleRequestName(SocketSlashCommand command, string handle)
        {
            if (await IsWrongChannel(command))
            {
                return;
            }

            string name = command.Data.Options.First().Value.ToString()?.Trim() ?? "";
            log.Info($"CMD /{command.Data.Name} - {handle}: {name}");

            RegistrationCodeDao dao = DB.Get().RegistrationCodeDao;

            // One active request/code per Discord user.
            RegistrationCodeDao.RegistrationCodeEntry existing = dao.FindLatestByDiscordUser(command.User.Id);
            if (existing is { State: RegistrationCodeDao.RegistrationState.Requested })
            {
                await command.RespondAsync(
                    $"You already have a pending request for `{existing.IssuedTo}`. Please wait for it to be reviewed.",
                    ephemeral: true);
                return;
            }
            if (existing is { State: RegistrationCodeDao.RegistrationState.Issued, IsValid: true })
            {
                await command.RespondAsync(
                    $"You already have an approved code waiting. Use `/{CMD_GET_CODE}` to receive it.",
                    ephemeral: true);
                return;
            }

            if (!LoginManager.IsValidUsername(name))
            {
                await command.RespondAsync(LoginManager.InvalidUsername, ephemeral: true);
                return;
            }
            if (!LoginManager.IsAllowedUsername(name))
            {
                await command.RespondAsync(LoginManager.CannotUseThisUsername, ephemeral: true);
                return;
            }
            if (DB.Get().LoginDao.Find(name.ToLower()) is not null)
            {
                await command.RespondAsync(LoginManager.UsernameIsAlreadyUsed, ephemeral: true);
                return;
            }

            SocketGuildUser guildUser = command.User as SocketGuildUser;
            dao.Save(new RegistrationCodeDao.RegistrationCodeEntry
            {
                Code = Guid.NewGuid().ToString(),
                State = RegistrationCodeDao.RegistrationState.Requested,
                IssuedTo = name.ToLower(),
                RequestedAt = DateTime.UtcNow,
                DiscordUserId = command.User.Id,
                DiscordUserName = command.User.Username,
                DiscordDisplayName = guildUser?.DisplayName ?? command.User.Username,
                DiscordAvatarUrl = command.User.GetAvatarUrl() ?? command.User.GetDefaultAvatarUrl(),
                DiscordCreatedAt = command.User.CreatedAt.UtcDateTime,
                DiscordJoinedAt = guildUser?.JoinedAt?.UtcDateTime
            });

            await command.RespondAsync(
                $"Your request for `{name}` has been submitted for review. " +
                "You will be pinged here once it is approved.",
                ephemeral: true);
        }

        private async Task HandleGetCode(SocketSlashCommand command, string handle)
        {
            if (await IsWrongChannel(command))
            {
                return;
            }

            log.Info($"CMD /{command.Data.Name} - {handle}");
            RegistrationCodeDao dao = DB.Get().RegistrationCodeDao;
            RegistrationCodeDao.RegistrationCodeEntry entry = dao.FindLatestByDiscordUser(command.User.Id);

            if (entry is null || entry.State == RegistrationCodeDao.RegistrationState.Requested)
            {
                await command.RespondAsync(
                    $"You do not have an approved code yet. Use `/{CMD_REQUEST_NAME}` first, then wait for approval.",
                    ephemeral: true);
                return;
            }

            if (entry.State == RegistrationCodeDao.RegistrationState.Declined)
            {
                await command.RespondAsync(
                    $"Your username request was declined: {entry.DeclineReason}",
                    ephemeral: true);
                return;
            }

            if (entry.IsUsed)
            {
                await command.RespondAsync("You have already registered an account.", ephemeral: true);
                return;
            }

            if (entry.HasExpired)
            {
                // Re-queue the request so an admin can approve it again.
                entry.State = RegistrationCodeDao.RegistrationState.Requested;
                entry.ExpiresAt = default;
                entry.RequestedAt = DateTime.UtcNow;
                dao.Save(entry);
                await command.RespondAsync(
                    "Your registration code has expired. Your request has been sent back for review.",
                    ephemeral: true);
                return;
            }

            await command.RespondAsync(
                $"Your registration code for `{entry.IssuedTo}` is:\n`{entry.Code}`\n" +
                "Enter this username and code on the registration screen.",
                ephemeral: true);
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

        private async Task PingRequestChannel(ulong discordUserId, string message)
        {
            if (!requestChannelId.HasValue)
            {
                return;
            }

            try
            {
                await SendMessageAsync(
                    text: message,
                    allowedMentions: new AllowedMentions(AllowedMentionTypes.Users),
                    channelIdOverride: requestChannelId);
            }
            catch (Exception e)
            {
                log.Error($"Failed to ping user {discordUserId} in the request channel", e);
            }
        }
        
        public async Task PingUsernameRequestApproved(ulong discordUserId, string username)
        {
            await PingRequestChannel(
                discordUserId,
                $"<@{discordUserId}> your username request for `{username}` has been approved! " +
                $"Use `/{CMD_GET_CODE}` to receive your registration code.");
        }

        public async Task PingUsernameRequestDeclined(ulong discordUserId, string reason)
        {
            await PingRequestChannel(
                discordUserId,
                $"<@{discordUserId}> your username request has been declined: {reason}");
        }
    }
}