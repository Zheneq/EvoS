using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.ApiServer;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using Discord;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using LobbyGameClientMessages;
using log4net;
using log4net.Core;
using Game = CentralServer.BridgeServer.Game;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordManager
    {
        private static DiscordManager _instance;
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordManager));


        private static readonly string LINE =
            "\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_";
        private static readonly string LINE_LONG = LINE + "\\_\\_\\_\\_\\_\\_\\_" + LINE;

        private readonly DiscordConfiguration conf;

        private readonly DiscordClientWrapper gameLogChannel;
        private readonly DiscordClientWrapper adminChannel;
        private readonly DiscordClientWrapper lobbyChannel;
        private readonly DiscordClientWrapper adminSystemReportChannel;
        private readonly DiscordClientWrapper adminUserReportChannel;
        private readonly DiscordClientWrapper adminClientReportChannel;
        private readonly DiscordClientWrapper adminClientErrorChannel;
        private readonly DiscordClientWrapper adminChatLogChannel;
        private readonly DiscordClientWrapper adminActionLogChannel;
        private readonly DiscordClientWrapper adminErrorLogChannel;
        private DiscordBotWrapper discordBot;

        private readonly CancellationTokenSource cancelTokenSource = new();

        private static readonly DiscordLobbyUtils.Status NO_STATUS = new()
            { totalPlayers = -1, inGame = -1, inQueue = -1 };
        private DiscordLobbyUtils.Status lastStatus = NO_STATUS;


        public DiscordManager()
        {
            conf = LobbyConfiguration.GetDiscordConfiguration();
            if (!conf.Enabled)
            {
                log.Info("Discord is not enabled");
                return;
            }

            gameLogChannel = MakeChannel("game log", conf.GameLogChannel);
            adminChannel = MakeChannel("admin", conf.AdminChannel);
            adminSystemReportChannel = MakeChannel("admin system report", conf.AdminSystemReportChannel, adminChannel);
            adminUserReportChannel = MakeChannel("admin user report", conf.AdminUserReportChannel, adminChannel);
            adminClientReportChannel = MakeChannel(
                "admin client report",
                conf.AdminClientReportChannel,
                adminUserReportChannel);
            adminClientErrorChannel = MakeChannel("admin client error", conf.AdminClientErrorChannel);
            adminChatLogChannel = MakeChannel("admin chat log", conf.AdminChatLogChannel, adminChannel);
            adminActionLogChannel = MakeChannel("admin action log", conf.AdminActionLogChannel, adminChannel);
            adminErrorLogChannel = MakeChannel("admin error log", conf.AdminErrorLogChannel, adminChannel);
            lobbyChannel = MakeChannel("lobby", conf.LobbyChannel);
        }

        private DiscordClientWrapper MakeChannel(
            string label,
            DiscordChannel channel,
            DiscordClientWrapper fallback = null)
        {
            if (channel.IsChannel())
            {
                log.Info($"Discord {label} channel is enabled");
                return new DiscordClientWrapper(channel, conf.RetryCount, conf.RetryDelayMs);
            }

            return fallback;
        }

        public static DiscordManager Get()
        {
            return _instance ??= new DiscordManager();
        }

        public async Task Start()
        {
            if (lobbyChannel != null)
            {
                _ = SendServerStatusLoop(cancelTokenSource.Token);
                ChatManager.Get().OnGlobalChatMessage += SendGlobalChatMessage;
                ChatManager.Get().OnBroadcastMessage += SendBroadcastToLobbyChannel;
            }

            if (adminChatLogChannel is not null)
            {
                ChatManager.Get().OnChatMessage += SendChatMessageAudit;
            }

            if (adminActionLogChannel is not null)
            {
                AdminManager.Get().OnAdminAction += SendAdminActionAudit;
                AdminManager.Get().OnAdminMessage += SendAdminMessageAudit;
                AdminController.OnAdminPauseQueue += SendAdminPauseQueueAudit;
                ChatManager.Get().OnBroadcastMessage += SendBroadcastMessageAudit;
            }

            if (adminSystemReportChannel is not null)
            {
                AdminController.OnAdminScheduleShutdown += SendAdminScheduleShutdownAudit;
            }

            if (adminClientReportChannel is not null)
            {
                CrashReportManager.OnArchive += SendCrashReportAsync;
                CrashReportManager.OnStatusReport += SendStatusReportAsync;
                CrashReportManager.OnErrorReport += SendErrorReportAsync;
                CrashReportManager.OnNewError += SendNewErrorReportAsync;
            }

            await StartBot();
        }

        private async Task StartBot()
        {
            if (discordBot != null)
            {
                log.Error("Attempting to start bot when it is already started!");
                return;
            }

            if (conf.BotToken.IsNullOrEmpty())
            {
                log.Info("Discord bot is not enabled");
                return;
            }

            if (conf.BotToken.Length < 70)
            {
                log.Error("Discord bot token is invalid");
                return;
            }

            // Init bot but we dont use it for anything not yet anyway we just want chat from discord to atlas and commands
            discordBot = new DiscordBotWrapper(conf);
            await discordBot.Login(conf);
        }

        public void Shutdown()
        {
            if (lobbyChannel != null)
            {
                ChatManager.Get().OnGlobalChatMessage -= SendGlobalChatMessage;
                ChatManager.Get().OnBroadcastMessage -= SendBroadcastToLobbyChannel;
            }

            if (adminChatLogChannel is not null)
            {
                ChatManager.Get().OnChatMessage -= SendChatMessageAudit;
            }

            if (adminActionLogChannel is not null)
            {
                AdminManager.Get().OnAdminAction -= SendAdminActionAudit;
                AdminManager.Get().OnAdminMessage -= SendAdminMessageAudit;
                AdminController.OnAdminPauseQueue -= SendAdminPauseQueueAudit;
                ChatManager.Get().OnBroadcastMessage -= SendBroadcastMessageAudit;
            }

            if (adminSystemReportChannel is not null)
            {
                AdminController.OnAdminScheduleShutdown -= SendAdminScheduleShutdownAudit;
            }

            if (adminClientReportChannel is not null)
            {
                CrashReportManager.OnArchive -= SendCrashReportAsync;
                CrashReportManager.OnStatusReport -= SendStatusReportAsync;
                CrashReportManager.OnErrorReport -= SendErrorReportAsync;
                CrashReportManager.OnNewError -= SendNewErrorReportAsync;
            }

            cancelTokenSource.Cancel();
            cancelTokenSource.Dispose();
        }

        private async Task SendServerStatusLoop(CancellationToken cancelToken)
        {
            while (true)
            {
                if (cancelToken.IsCancellationRequested) return;
                await SendServerStatus();
                await Task.Delay(conf.LobbyChannelUpdatePeriodSeconds * 1000, cancelToken);
            }
        }

        public async void SendGameReport(
            LobbyGameInfo gameInfo,
            string serverName,
            string serverVersion,
            LobbyGameSummary gameSummary)
        {
            if (gameLogChannel == null)
            {
                return;
            }

            try
            {
                if (gameSummary.GameResult != GameResult.TeamAWon
                    && gameSummary.GameResult != GameResult.TeamBWon)
                {
                    return;
                }

                await gameLogChannel.SendMessageAsync(
                    null,
                    false,
                    embeds:
                    [
                        MakeGameReportEmbed(gameInfo, serverName, serverVersion, gameSummary)
                    ],
                    "Atlas Reactor");
            }
            catch (Exception e)
            {
                log.Error("Failed to send game report to discord webhook", e);
            }
        }

        private async Task SendServerStatus()
        {
            if (lobbyChannel == null || !conf.LobbyEnableServerStatus)
            {
                return;
            }

            DiscordLobbyUtils.Status status = DiscordLobbyUtils.GetStatus();
            if (conf.LobbyChannelUpdateOnChangeOnly && lastStatus.Equals(status))
            {
                return;
            }

            try
            {
                await lobbyChannel.SendMessageAsync(
                        embeds:
                        [
                            new EmbedBuilder
                            {
                                Title = DiscordLobbyUtils.BuildPlayerCountSummary(status),
                                Color = Color.Green
                            }.Build()
                        ],
                        username: "Atlas Reactor")
                    .ContinueWith(_ => lastStatus = status);
            }
            catch (Exception e)
            {
                log.Error("Failed to send status to discord webhook", e);
            }
        }

        private async void SendGlobalChatMessage(ChatNotification notification)
        {
            if (lobbyChannel == null || !conf.LobbyEnableChat)
            {
                return;
            }

            try
            {
                await lobbyChannel.SendMessageAsync(
                    notification.Text,
                    username: notification.SenderHandle);
            }
            catch (Exception e)
            {
                log.Error("Failed to send lobby chat message to discord webhook", e);
            }
        }

        private async void SendBroadcastToLobbyChannel(ChatNotification notification)
        {
            if (lobbyChannel == null || notification.RecipientHandle != null)
            {
                return;
            }

            try
            {
                await lobbyChannel.SendMessageAsync(
                    notification.Text,
                    username: "Broadcast");
            }
            catch (Exception e)
            {
                log.Error("Failed to send broadcast message to lobby discord webhook", e);
            }
        }

        private async void SendChatMessageAudit(ChatNotification notification, bool isMuted)
        {
            if (adminChatLogChannel == null || !conf.AdminEnableChatAudit)
            {
                return;
            }

            try
            {
                List<long> recipients = DiscordLobbyUtils.GetMessageRecipients(
                    notification,
                    out string fallback,
                    out string context);
                await adminChatLogChannel.SendMessageAsync(
                    username: notification.SenderHandle,
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = notification.Text,
                            Description = description(
                                !recipients.IsNullOrEmpty()
                                    ? $"to {DiscordLobbyUtils.FormatMessageRecipients(notification.SenderAccountId, recipients)}"
                                    : fallback),
                            Color = DiscordLobbyUtils.GetColor(notification.ConsoleMessageType),
                            Footer = footer(isMuted ? $"MUTED ({context})" : context)
                        }.Build()
                    ],
                    threadIdOverride: conf.AdminChatAuditThreadId);
            }
            catch (Exception e)
            {
                log.Error("Failed to send audit chat message to discord webhook", e);
            }
        }

        private async void SendAdminActionAudit(long accountId, AdminComponent.AdminActionRecord record)
        {
            if (adminActionLogChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                await adminActionLogChannel.SendMessageAsync(
                    username: record.AdminUsername,
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = $"{record.ActionType} {account.Handle ?? $"#{accountId}"} for {record.Duration}",
                            Description = description(record.Description),
                            Color = DiscordUtils.GetLogColor(Level.Warn),
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin action audit message to discord webhook", e);
            }
        }

        private async void SendAdminMessageAudit(long accountId, long adminAccountId, string msg)
        {
            if (adminActionLogChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                await adminActionLogChannel.SendMessageAsync(
                    username: LobbyServerUtils.GetHandle(adminAccountId),
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = $"Admin message for {account.Handle ?? $"#{accountId}"}",
                            Description = description(msg),
                            Color = DiscordUtils.GetLogColor(Level.Warn),
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin message audit message to discord webhook", e);
            }
        }

        private async void SendAdminPauseQueueAudit(long adminAccountId, AdminController.PauseQueueModel action)
        {
            if (adminActionLogChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(adminAccountId);
                await adminActionLogChannel.SendMessageAsync(
                    username: account?.Handle,
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = action.Paused ? "Pause queue" : "Unpause queue",
                            Color = DiscordUtils.GetLogColor(Level.Warn),
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin pause queue audit message to discord webhook", e);
            }
        }

        private async void SendBroadcastMessageAudit(ChatNotification notification)
        {
            if (adminActionLogChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                string to = notification.RecipientHandle != null
                    ? $"to {notification.RecipientHandle}"
                    : "to all";
                await adminActionLogChannel.SendMessageAsync(
                    username: notification.SenderHandle ?? "Atlas Reactor",
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = $"Broadcast {to}",
                            Description = description(notification.Text),
                            Color = Color.Orange,
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error("Failed to send broadcast message audit to discord webhook", e);
            }
        }

        private async void SendAdminScheduleShutdownAudit(
            long adminAccountId,
            AdminController.PendingShutdownModel action)
        {
            if (adminSystemReportChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                await adminSystemReportChannel.SendMessageAsync(
                    username: LobbyServerUtils.GetHandle(adminAccountId),
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Title = $"Shutdown: {action.Type}",
                            Color = DiscordUtils.GetLogColor(Level.Warn),
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin schedule shutdown audit message to discord webhook", e);
            }
        }

        public async void SendAdminGameReport(
            LobbyGameInfo gameInfo,
            string serverName,
            string serverVersion,
            LobbyGameSummary gameSummary)
        {
            if (adminSystemReportChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                if (gameSummary.GameResult == GameResult.TeamAWon
                    || gameSummary.GameResult == GameResult.TeamBWon
                    || gameInfo.GameConfig == null
                    || gameInfo.GameConfig.GameType != GameType.PvP)
                {
                    return;
                }

                PlayerGameSummary playerGameSummary = gameSummary.PlayerGameSummaryList
                    .FirstOrDefault(x => x.MatchResults != null
                                         && x.MatchResults.FriendlyStatlines != null
                                         && x.MatchResults.EnemyStatlines != null);
                string msg = null;
                if (playerGameSummary != null)
                {
                    MatchResultsStats matchResultsStats = playerGameSummary.MatchResults;
                    msg = string.Join(
                        "\n",
                        matchResultsStats.FriendlyStatlines.Select(Format)
                            .Concat(matchResultsStats.EnemyStatlines.Select(Format)));
                }

                await adminSystemReportChannel.SendMessageAsync(
                    msg,
                    false,
                    embeds:
                    [
                        MakeGameReportEmbed(gameInfo, serverName, serverVersion, gameSummary)
                    ],
                    "Atlas Reactor");
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin game report to discord webhook", e);
            }

            return;

            string Format(MatchResultsStatline x)
            {
                return $"{x.AccountID} \t{x.DisplayName} \tReplacedByBot={x.HumanReplacedByBot}";
            }
        }

        public void SendAdminLogMessageAsync(string message) => _ = SendAdminLogMessage(message);

        public async Task SendAdminLogMessage(string message)
        {
            if (adminSystemReportChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }

            try
            {
                await adminSystemReportChannel.SendMessageAsync(message, username: "Atlas Reactor");
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin log message to discord webhook", e);
            }
        }

        public async Task SendLogEvent(Level severity, string msg)
        {
            if (adminErrorLogChannel == null || !conf.AdminEnableLog)
            {
                return;
            }

            try
            {
                await adminErrorLogChannel.SendMessageAsync(
                    username: "Atlas Reactor",
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Description = description(msg),
                            Color = DiscordUtils.GetLogColor(severity)
                        }.Build()
                    ],
                    threadIdOverride: conf.AdminLogThreadId);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send log to Discord: {e}");
            }
        }

        private void SendCrashReportAsync(long accountId, Stream archive) => _ = SendCrashReport(accountId, archive);

        public async Task SendCrashReport(long accountId, Stream archive)
        {
            if (adminClientReportChannel == null || !conf.AdminEnableUserReports)
            {
                return;
            }

            try
            {
                string handle = LobbyServerUtils.GetHandle(accountId);
                string fileName = $"Dump_{DateTime.UtcNow:yyyy_MM_dd__HH_mm_ss}_{handle}.zip";
                LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);
                FileAttachment attachment = new FileAttachment(archive, fileName);

                await adminClientReportChannel.SendFileAsync(
                    attachment,
                    $"Report from {handle}\nSent from version {sessionInfo?.BuildVersion}\n",
                    username: "Atlas Reactor");
            }
            catch (Exception e)
            {
                log.Error($"Failed to send crash report to Discord: {e}");
            }
        }

        private void SendStatusReportAsync(long accountId, ClientStatusReport report) =>
            _ = SendStatusReport(accountId, report);

        public async Task SendStatusReport(long accountId, ClientStatusReport report)
        {
            if (adminClientReportChannel == null || !conf.AdminEnableUserReports)
            {
                return;
            }

            if (ShouldIgnoreError(report.StatusDetails, out string match))
            {
                log.Info($"Client Status Report matched \"{match}\", not sending to Discord");
                return;
            }

            try
            {
                string handle = LobbyServerUtils.GetHandle(accountId);
                LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(accountId);
                await adminClientReportChannel.SendMessageAsync(
                    username: "Atlas Reactor",
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Description = description(
                                $"{report.Status} report from {handle}\n"
                                + $"Device identifier: {report.DeviceIdentifier}\n"
                                + $"File date time: {report.FileDateTime}\n"
                                + $"Sent from version {sessionInfo?.BuildVersion}\n"
                                + $"Status details: {report.StatusDetails}\n"
                                + $"User message: {report.UserMessage}\n"
                            ),
                            Color = DiscordUtils.GetLogColor(
                                report.Status is ClientStatusReport.ClientStatusReportType.Crash
                                    or ClientStatusReport.ClientStatusReportType.CrashUserMessage
                                    ? Level.Fatal
                                    : Level.Warn)
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error($"Failed to send status report to Discord: {e}");
            }
        }

        private void SendErrorReportAsync(
            long accountId,
            uint stackTraceHash,
            uint count,
            string clientVersion,
            ClientErrorDao.Entry error)
            => _ = SendErrorReport(accountId, stackTraceHash, count, clientVersion, error);

        public async Task SendErrorReport(
            long accountId,
            uint stackTraceHash,
            uint count,
            string clientVersion,
            ClientErrorDao.Entry error)
        {
            if (adminClientErrorChannel == null || !conf.AdminEnableUserReports)
            {
                return;
            }

            if (error is null)
            {
                log.Info("Client Error Report with no details, not sending to Discord");
                return;
            }

            if (ShouldIgnoreError(error.LogString, out string match))
            {
                log.Info($"Client Error Report matched \"{match}\", not sending to Discord");
                return;
            }

            try
            {
                string handle = LobbyServerUtils.GetHandle(accountId);
                await adminClientErrorChannel.SendMessageAsync(
                    username: "Atlas Reactor",
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Description = description(
                                $"{handle} has encountered error `{stackTraceHash}`{
                                    (count > 1 ? $" {count} times" : "")} on version `{clientVersion}`"),
                            Color = DiscordUtils.GetLogColor(Level.Warn),
                            Footer = footer($"{error.LogString}\n{error.StackTrace}")
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error($"Failed to send error report to Discord: {e}");
            }
        }

        private void SendNewErrorReportAsync(long accountId, ClientErrorReport error) =>
            _ = SendNewErrorReport(accountId, error);

        public async Task SendNewErrorReport(long accountId, ClientErrorReport error)
        {
            if (adminClientErrorChannel == null || !conf.AdminEnableUserReports)
            {
                return;
            }

            if (ShouldIgnoreError(error.LogString, out string match))
            {
                log.Info($"Client New Error Report matched \"{match}\", not sending to Discord");
                return;
            }

            try
            {
                string handle = LobbyServerUtils.GetHandle(accountId);
                await adminClientErrorChannel.SendMessageAsync(
                    username: "Atlas Reactor",
                    text: $"New error encountered by {handle}",
                    embeds:
                    [
                        new EmbedBuilder
                        {
                            Description = description(
                                $"ID `{error.StackTraceHash}`\n{error.LogString}\n{error.StackTrace}"),
                            Color = DiscordUtils.GetLogColor(Level.Warn)
                        }.Build()
                    ]);
            }
            catch (Exception e)
            {
                log.Error($"Failed to send new error report to Discord: {e}");
            }
        }

        private static Embed MakeGameReportEmbed(
            LobbyGameInfo gameInfo,
            string serverName,
            string serverVersion,
            LobbyGameSummary gameSummary)
        {
            string map = Maps.GetMapName[gameInfo.GameConfig.Map];
            string gameType = gameInfo.GameConfig.GameType.ToString();

            EmbedBuilder eb = new EmbedBuilder
            {
                Title = $"Game Result for {gameType} {map ?? gameInfo.GameConfig.Map}",
                Description = description(
                    $"{RenderGameResult(gameSummary.GameResult)} " +
                    $"{gameSummary.TeamAPoints}-{gameSummary.TeamBPoints} ({gameSummary.NumOfTurns} turns)"
                ),
                Color = gameSummary.GameResult.ToString() == "TeamAWon" ? Color.Green : Color.Red
            };

            eb.AddField("Team A", LINE, true);
            eb.AddField("│", "│", true);
            eb.AddField("Team B", LINE, true);
            eb.AddField(
                "**[ Takedowns : Deaths : Deathblows ] [ Damage : Healing : Damage Received ]**",
                LINE_LONG,
                false);

            var (teamA, teamB) = GetTeamsFromGameSummary(gameSummary);
            int n = Math.Max(teamA.Count, teamB.Count);
            for (int i = 0; i < n; i++)
            {
                GameReportAddPlayer(eb, teamA.ElementAtOrDefault(i));
                eb.AddField("│", "│", true);
                GameReportAddPlayer(eb, teamB.ElementAtOrDefault(i));
            }

            eb.Footer = footer(
                $"{serverName} - {serverVersion} - {GameUtils.GameIdString(gameInfo)} - {gameInfo.GameServerProcessCode}");
            return eb.Build();
        }

        private static string RenderGameResult(GameResult gameResult)
        {
            return gameResult switch
            {
                GameResult.TeamAWon => "Team A Won",
                GameResult.TeamBWon => "Team B Won",
                GameResult.TieGame => "Draw",
                _ => gameResult.ToString()
            };
        }

        public async void SendPlayerFeedback(long accountId, ClientFeedbackReport message)
        {
            if (adminUserReportChannel == null || !conf.AdminEnableUserReports)
            {
                return;
            }

            try
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                EmbedBuilder eb = new EmbedBuilder
                {
                    Title = $"User Report From: {account.Handle}",
                    Description = description(message.Message),
                    Color = 16711680
                };
                eb.AddField("Reason", message.Reason, true);
                if (message.ReportedPlayerHandle != null)
                {
                    eb.AddField(
                        "Reported Account",
                        $"{message.ReportedPlayerHandle} #{message.ReportedPlayerAccountId}",
                        true);
                }

                Game game = SessionManager.GetClientConnection(accountId)?.CurrentGame;
                if (game != null)
                {
                    eb.AddField(
                        "Game",
                        $"{game.Server?.Name} {GameUtils.GameIdString(game.GameInfo)} Turn {game.GameMetrics.CurrentTurn}",
                        true);
                }

                await adminUserReportChannel.SendMessageAsync(
                    null,
                    false,
                    embeds: [eb.Build()],
                    "Atlas Reactor",
                    threadIdOverride: conf.AdminUserReportThreadId);
            }
            catch (Exception e)
            {
                log.Error("Failed to send user report to discord webhook", e);
            }
        }

        private static void GameReportAddPlayer(EmbedBuilder eb, PlayerGameSummary player)
        {
            if (player == null)
            {
                eb.AddField("-", "-", true);
                return;
            }

            string handle = "UNKNOWN";
            if (player.AccountId == 0)
            {
                handle = player.CharacterName;
            }
            else
            {
                PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
                if (account is not null)
                {
                    handle = account.Handle;
                }
            }

            eb.AddField(
                $"{handle} ({player.CharacterName})",
                $"**[ {player.NumAssists} : {player.NumDeaths} : {player.NumKills} ] [ {player.TotalPlayerDamage} : " +
                $"{player.GetTotalHealingFromAbility() + player.TotalPlayerAbsorb} : {player.TotalPlayerDamageReceived} ]**",
                true);
        }

        private static (List<PlayerGameSummary> teamA, List<PlayerGameSummary> teamB) GetTeamsFromGameSummary(
            LobbyGameSummary gameSummary)
        {
            var players = gameSummary.PlayerGameSummaryList.Where(p => !p.IsSpectator()).ToList();
            return (players.Where(p => p.IsInTeamA()).ToList(),
                players.Where(p => !p.IsInTeamA()).ToList());
        }

        private bool ShouldIgnoreError(string error, out string match)
        {
            match = conf.ClientStatusReportBlacklist?.FirstOrDefault(error.Contains);
            return match is not null;
        }

        private const int FOOTER_MAX_LENGTH = 2048;
        private const int DESCRIPTION_MAX_LENGTH = 4096;

        private static EmbedFooterBuilder footer(String text)
        {
            return new EmbedFooterBuilder { Text = limitLength(text, FOOTER_MAX_LENGTH) };
        }

        private static string description(string text)
        {
            return limitLength(text, DESCRIPTION_MAX_LENGTH);
        }

        private static string limitLength(string text, int length)
            => text?.Length > length ? text[..(length - 3)] + "..." : text;
    }
}