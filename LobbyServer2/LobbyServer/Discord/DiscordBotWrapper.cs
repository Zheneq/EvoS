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
        private const string CMD_APPROVE = "approve";
        private const string CMD_DECLINE = "decline";

        private readonly DiscordSocketClient botClient;
        private static readonly DiscordSocketConfig discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        private const string BTN_APPROVE = "req_approve";
        private const string BTN_DECLINE = "req_decline";
        private const string MODAL_DECLINE = "req_decline_modal";
        private const string MODAL_REASON_INPUT = "reason";

        public DiscordBotWrapper(DiscordBotConfiguration conf)
        {
            log.Info("Discord bot is enabled");
            botClient = new DiscordSocketClient(discordConfig);
            botClient.Log += Log;
            botClient.Ready += Ready;
            botClient.SlashCommandExecuted += SlashCommandHandler;
            botClient.ButtonExecuted += ButtonHandler;
            botClient.ModalSubmitted += ModalHandler;
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

            SlashCommandProperties approveCommand = new SlashCommandBuilder()
                .WithName(CMD_APPROVE)
                .WithDescription("Approve a username request")
                .AddOption("user", ApplicationCommandOptionType.User, "The user who made the request", true)
                .AddOption("name", ApplicationCommandOptionType.String, "The requested username", true)
                .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                .Build();

            SlashCommandProperties declineCommand = new SlashCommandBuilder()
                .WithName(CMD_DECLINE)
                .WithDescription("Decline a username request")
                .AddOption("user", ApplicationCommandOptionType.User, "The user who made the request", true)
                .AddOption("name", ApplicationCommandOptionType.String, "The requested username", true)
                .AddOption("reason", ApplicationCommandOptionType.String, "Reason shown to the user", true)
                .WithDefaultMemberPermissions(GuildPermission.ManageGuild)
                .Build();

            try
            {
                await botClient.CreateGlobalApplicationCommandAsync(infoCommand);
                await botClient.CreateGlobalApplicationCommandAsync(broadcastCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueDisableCommand);
                await botClient.CreateGlobalApplicationCommandAsync(queueEnableCommand);
                await botClient.CreateGlobalApplicationCommandAsync(requestNameCommand);
                await botClient.CreateGlobalApplicationCommandAsync(getCodeCommand);
                await botClient.CreateGlobalApplicationCommandAsync(approveCommand);
                await botClient.CreateGlobalApplicationCommandAsync(declineCommand);
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                log.Error(json);
            }
        }

        private Task ClientOnMessageReceived(SocketMessage socketMessage)
        {
            var botChannelId = DiscordBotConfiguration.Get().BotChannelId;
            
            // Check if Author is not a bot and allow only reading from the discord LobbyChannel
            if (botChannelId == null
                || botChannelId == 0
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
                    if (!await VerifyAdmin(command, handle)) break;
                    string msg = command.Data.Options.First().Value.ToString();
                    log.Info($"CMD /{command.Data.Name} - {handle}: {msg}");
                    ChatManager.Get().Broadcast(msg);
                    await command.RespondAsync($"Broadcast: {msg}", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_DISABLE:
                {
                    if (!await VerifyAdmin(command, handle)) break;
                    log.Info($"CMD /{command.Data.Name} - {handle}");
                    MatchmakingManager.Enabled = false;
                    await command.RespondAsync("Matchmaking queue is paused", ephemeral: true);
                    break;
                }
                case CMD_QUEUE_ENABLE:
                {
                    if (!await VerifyAdmin(command, handle)) break;
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
                case CMD_APPROVE:
                {
                    if (!await VerifyAdmin(command, handle)) break;
                    await HandleApprove(command, handle);
                    break;
                }
                case CMD_DECLINE:
                {
                    if (!await VerifyAdmin(command, handle)) break;
                    await HandleDecline(command, handle);
                    break;
                }
            }
        }

        // Extra defense-in-depth on top of the command's ManageGuild permission gate.
        // When the allowlist is empty, we rely solely on that gate and let the command through.
        private static async Task<bool> VerifyAdmin(SocketInteraction interaction, string handle)
        {
            if (IsAdmin(interaction.User.Id))
            {
                return true;
            }

            log.Warn($"Rejected {interaction.Type} interaction from non-admin {handle}");
            await interaction.RespondAsync("You are not allowed to use this command.", ephemeral: true);
            return false;
        }

        private static bool IsAdmin(ulong discordUserId)
        {
            Dictionary<ulong, long> adminUserIds = DiscordBotConfiguration.Get().AdminUserIds;
            return adminUserIds is null
                   || adminUserIds.Count == 0
                   || adminUserIds.ContainsKey(discordUserId);
        }

        private static long GetAdminAccountId(ulong discordUserId)
        {
            return DiscordBotConfiguration.Get().AdminUserIds?.GetValueOrDefault(discordUserId) ?? 0;
        }

        private async Task<bool> VerifyChannel(SocketSlashCommand command)
        {
            var requestChannelId = DiscordBotConfiguration.Get().RequestChannelId;
            if (command.ChannelId != requestChannelId)
            {
                await command.RespondAsync(
                    $"Please use this command in <#{requestChannelId}>.",
                    ephemeral: true);
                return false;
            }

            return true;
        }

        private async Task HandleRequestName(SocketSlashCommand command, string handle)
        {
            if (!await VerifyChannel(command))
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
            ulong? approvedRoleId = DiscordBotConfiguration.Get().ApprovedRoleId;
            bool requesterHasApprovedRole = approvedRoleId is > 0
                && guildUser?.Roles.Any(r => r.Id == approvedRoleId.Value) == true;
            RegistrationCodeDao.RegistrationCodeEntry entry = new RegistrationCodeDao.RegistrationCodeEntry
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
            };
            dao.Save(entry);

            await SendUsernameRequestNotification(entry, requesterHasApprovedRole);

            await command.RespondAsync(
                $"Your request for `{name}` has been submitted for review. " +
                "You will be pinged here once it is approved.",
                ephemeral: true);
        }

        private async Task HandleGetCode(SocketSlashCommand command, string handle)
        {
            if (!await VerifyChannel(command))
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

        private async Task HandleApprove(SocketSlashCommand command, string handle)
        {
            ulong discordUserId = ((IUser)command.Data.Options.First(o => o.Name == "user").Value).Id;
            string username = command.Data.Options.First(o => o.Name == "name").Value.ToString()?.Trim().ToLower() ?? "";
            log.Info($"CMD /{command.Data.Name} - {handle}: {discordUserId} `{username}`");

            UsernameRequestManager.Result result = UsernameRequestManager.Approve(
                discordUserId, username, GetAdminAccountId(command.User.Id), handle,
                out RegistrationCodeDao.RegistrationCodeEntry entry);

            string response = result switch
            {
                UsernameRequestManager.Result.Success =>
                    $"Approved `{entry.IssuedTo}` — the user has been pinged.",
                UsernameRequestManager.Result.UsernameTaken =>
                    "That username is already in use.",
                UsernameRequestManager.Result.NotFound => "No pending request from that user for that username.",
                _ => throw new ArgumentOutOfRangeException()
            };
            await command.RespondAsync(response, ephemeral: true);
        }

        private async Task HandleDecline(SocketSlashCommand command, string handle)
        {
            ulong discordUserId = ((IUser)command.Data.Options.First(o => o.Name == "user").Value).Id;
            string username = command.Data.Options.First(o => o.Name == "name").Value.ToString()?.Trim().ToLower() ?? "";
            string reason = command.Data.Options.First(o => o.Name == "reason").Value.ToString()?.Trim() ?? "";
            log.Info($"CMD /{command.Data.Name} - {handle}: {discordUserId} `{username}` ({reason})");

            UsernameRequestManager.Result result = UsernameRequestManager.Decline(
                discordUserId, username, reason, GetAdminAccountId(command.User.Id), handle,
                out RegistrationCodeDao.RegistrationCodeEntry entry);

            string response = result switch
            {
                UsernameRequestManager.Result.Success => $"Declined `{entry.IssuedTo}` — the user has been pinged.",
                UsernameRequestManager.Result.NotFound => "No pending request from that user for that username.",
                _ => throw new ArgumentOutOfRangeException()
            };
            await command.RespondAsync(response, ephemeral: true);
        }

        private async Task ButtonHandler(SocketMessageComponent component)
        {
            string customId = component.Data.CustomId;
            if (!customId.StartsWith($"{BTN_APPROVE}:") && !customId.StartsWith($"{BTN_DECLINE}:"))
            {
                return;
            }

            string handle = $"{component.User.Username} ({component.User.Id})";
            if (!await VerifyAdmin(component, handle))
            {
                return;
            }

            try
            {
                if (customId.StartsWith($"{BTN_APPROVE}:"))
                {
                    (ulong discordUserId, string username) = ParseRequestId(customId[(BTN_APPROVE.Length + 1)..]);
                    log.Info($"BTN approve - {handle}: {discordUserId} `{username}`");
                    UsernameRequestManager.Result result = UsernameRequestManager.Approve(
                        discordUserId, username, GetAdminAccountId(component.User.Id), handle,
                        out RegistrationCodeDao.RegistrationCodeEntry entry);

                    switch (result)
                    {
                        case UsernameRequestManager.Result.Success:
                            await component.UpdateAsync(m => ResolveMessage(
                                m, component.Message, $"✅ Approved by {DisplayName(component.User)}", Color.Green));
                            break;
                        case UsernameRequestManager.Result.UsernameTaken:
                            await component.RespondAsync(
                                "That username is already in use.",
                                ephemeral: true);
                            break;
                        case UsernameRequestManager.Result.NotFound:
                            await component.RespondAsync(
                                "No pending request from that user for that username.",
                                ephemeral: true);
                            break;
                        default:
                            await component.RespondAsync(
                                "Server encountered an unexpected error.",
                                ephemeral: true);
                            break;
                    }
                }
                else if (customId.StartsWith($"{BTN_DECLINE}:"))
                {
                    string requestId = customId[(BTN_DECLINE.Length + 1)..];
                    // Carry the notification's message id so the modal handler can edit it once submitted.
                    Modal modal = new ModalBuilder()
                        .WithTitle("Decline username request")
                        .WithCustomId($"{MODAL_DECLINE}:{component.Message.Id}:{requestId}")
                        .AddTextInput("Reason (shown to the user)", MODAL_REASON_INPUT,
                            TextInputStyle.Paragraph, required: true)
                        .Build();
                    await component.RespondWithModalAsync(modal);
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to handle button {customId} from {handle}", e);
            }
        }

        private async Task ModalHandler(SocketModal modal)
        {
            string customId = modal.Data.CustomId;
            if (!customId.StartsWith($"{MODAL_DECLINE}:"))
            {
                return;
            }

            string handle = $"{modal.User.Username} ({modal.User.Id})";
            if (!await VerifyAdmin(modal, handle))
            {
                return;
            }

            string[] parts = customId[(MODAL_DECLINE.Length + 1)..].Split(':', 3);
            ulong messageId = ulong.TryParse(parts[0], out ulong id) ? id : 0;
            (ulong discordUserId, string username) = parts.Length > 2
                ? ParseRequestId($"{parts[1]}:{parts[2]}")
                : (0UL, "");
            string reason = modal.Data.Components
                .FirstOrDefault(c => c.CustomId == MODAL_REASON_INPUT)?.Value?.Trim() ?? "";
            log.Info($"MODAL decline - {handle}: {discordUserId} `{username}` ({reason})");

            try
            {
                UsernameRequestManager.Result result = UsernameRequestManager.Decline(
                    discordUserId, username, reason, GetAdminAccountId(modal.User.Id), handle,
                    out RegistrationCodeDao.RegistrationCodeEntry entry);

                if (result == UsernameRequestManager.Result.Success)
                {
                    await modal.RespondAsync($"Declined `{entry.IssuedTo}` — the user has been pinged.", ephemeral: true);
                    await ResolveMessage(modal.Channel, messageId, $"❌ Declined by {DisplayName(modal.User)}: {reason}", Color.Red);
                }
                else
                {
                    await modal.RespondAsync("No pending request from that user for that username.", ephemeral: true);
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to handle decline modal {discordUserId} `{username}` from {handle}", e);
            }
        }

        private static string DisplayName(IUser user) =>
            (user as IGuildUser)?.Nickname ?? user.Username;

        private static void ResolveMessage(MessageProperties m, IUserMessage original, string status, Color color)
        {
            EmbedBuilder builder = original?.Embeds.FirstOrDefault()?.ToEmbedBuilder() ?? new EmbedBuilder();
            builder.Color = color;
            builder.Footer = new EmbedFooterBuilder { Text = status };
            m.Embed = builder.Build();
            m.Components = new ComponentBuilder().Build();
        }

        private static async Task ResolveMessage(IMessageChannel channel, ulong messageId, string status, Color color)
        {
            if (channel is null
                || messageId == 0
                || await channel.GetMessageAsync(messageId) is not IUserMessage original)
            {
                return;
            }

            await original.ModifyAsync(m => ResolveMessage(m, original, status, color));
        }

        private async Task SendUsernameRequestNotification(
            RegistrationCodeDao.RegistrationCodeEntry entry,
            bool requesterHasApprovedRole)
        {
            var adminRequestChannelId = DiscordBotConfiguration.Get().AdminNotificationChannelId;
            if (adminRequestChannelId is null or 0)
            {
                return;
            }

            string joined = entry.DiscordJoinedAt.HasValue
                ? $"<t:{ToUnix(entry.DiscordJoinedAt.Value)}:D> (<t:{ToUnix(entry.DiscordJoinedAt.Value)}:R>)"
                : "Unknown";

            EmbedBuilder builder = new EmbedBuilder
            {
                Title = "New username request",
                Color = Color.Gold,
                Fields =
                {
                    new EmbedFieldBuilder { Name = "Requested username", Value = $"`{entry.IssuedTo}`" },
                    new EmbedFieldBuilder { Name = "Discord user", Value = $"<@{entry.DiscordUserId}>" },
                    new EmbedFieldBuilder
                    {
                        Name = "Registered",
                        Value = $"<t:{ToUnix(entry.DiscordCreatedAt)}:D> (<t:{ToUnix(entry.DiscordCreatedAt)}:R>)"
                    },
                    new EmbedFieldBuilder { Name = "Joined server", Value = joined },
                }
            };

            if (requesterHasApprovedRole)
            {
                builder.Color = Color.Orange;
                builder.AddField(
                    "⚠️ Already registered",
                    $"Already has the <@&{DiscordBotConfiguration.Get().ApprovedRoleId}> role");
            }

            Embed embed = builder.Build();

            string requestId = $"{entry.DiscordUserId}:{entry.IssuedTo}";
            MessageComponent components = new ComponentBuilder()
                .WithButton("Approve", $"{BTN_APPROVE}:{requestId}", ButtonStyle.Success)
                .WithButton("Decline", $"{BTN_DECLINE}:{requestId}", ButtonStyle.Danger)
                .Build();

            try
            {
                await SendMessageAsync(embed: embed, components: components, channelIdOverride: adminRequestChannelId);
            }
            catch (Exception e)
            {
                log.Error($"Failed to post username request {entry.Code} to the admin channel", e);
            }
        }

        // Request identity carried in button/modal custom ids: "<discordUserId>:<username>".
        private static (ulong, string) ParseRequestId(string requestId)
        {
            string[] parts = requestId.Split(':', 2);
            ulong discordUserId = ulong.TryParse(parts[0], out ulong id) ? id : 0;
            string username = parts.Length > 1 ? parts[1] : "";
            return (discordUserId, username);
        }

        private static long ToUnix(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
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
            ulong? _channelId = channelIdOverride ?? DiscordBotConfiguration.Get().BotChannelId;
            if (_channelId.Value == 0) return null;
            IMessageChannel chnl = botClient.GetChannel(_channelId.Value) as IMessageChannel;
            return chnl.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);
        }

        private async Task PingRequestChannel(ulong discordUserId, string message)
        {
            var requestChannelId = DiscordBotConfiguration.Get().RequestChannelId;
            if (requestChannelId is null or 0)
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

        public async Task GrantApprovedRole(ulong discordUserId)
        {
            ulong? roleId = DiscordBotConfiguration.Get().ApprovedRoleId;
            if (roleId is null or 0)
            {
                return;
            }

            try
            {
                foreach (SocketGuild guild in botClient.Guilds)
                {
                    if (guild.GetRole(roleId.Value) is null)
                    {
                        continue;
                    }

                    IGuildUser user = (IGuildUser)guild.GetUser(discordUserId)
                                      ?? await botClient.Rest.GetGuildUserAsync(guild.Id, discordUserId);
                    if (user is null)
                    {
                        continue;
                    }

                    if (!user.RoleIds.Contains(roleId.Value))
                    {
                        await user.AddRoleAsync(roleId.Value);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                log.Error($"Failed to grant approved role to {discordUserId}", e);
            }
        }
    }
}