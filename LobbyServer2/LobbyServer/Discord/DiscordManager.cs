using System;
using System.Collections.Generic;
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
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using log4net.Core;
using static EvoS.Framework.Network.Static.GameSubType;
using Game = CentralServer.BridgeServer.Game;

namespace CentralServer.LobbyServer.Discord
{
    public class DiscordManager
    {
        private static DiscordManager _instance;
        private static readonly ILog log = LogManager.GetLogger(typeof(DiscordManager));


        private static readonly string LINE = "\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_\\_";
        private static readonly string LINE_LONG = LINE + "\\_\\_\\_\\_\\_\\_\\_" + LINE;

        private readonly DiscordConfiguration conf;

        private readonly DiscordClientWrapper gameLogChannel;
        private readonly DiscordClientWrapper adminChannel;
        private readonly DiscordClientWrapper lobbyChannel;
        private readonly DiscordClientWrapper adminSystemReportChannel;
        private readonly DiscordClientWrapper adminUserReportChannel;
        private readonly DiscordClientWrapper adminChatLogChannel;
        private readonly DiscordClientWrapper adminActionLogChannel;
        private readonly DiscordClientWrapper adminErrorLogChannel;
        private DiscordBotWrapper discordBot;

        private readonly CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

        private static readonly DiscordLobbyUtils.Status NO_STATUS = new DiscordLobbyUtils.Status { totalPlayers = -1, inGame = -1, inQueue = -1 };
        private DiscordLobbyUtils.Status lastStatus = NO_STATUS;


        public DiscordManager()
        {
            conf = LobbyConfiguration.GetDiscordConfiguration();
            if (!conf.Enabled)
            {
                log.Info("Discord is not enabled");
                return;
            }

            if (conf.GameLogChannel.IsChannel())
            {
                log.Info("Discord game log is enabled");
                gameLogChannel = new DiscordClientWrapper(conf.GameLogChannel);
            }

            if (conf.AdminChannel.IsChannel())
            {
                log.Info("Discord admin is enabled");
                adminChannel = new DiscordClientWrapper(conf.AdminChannel);
            }

            if (conf.AdminSystemReportChannel.IsChannel())
            {
                log.Info("Discord admin system report channel is enabled");
                adminSystemReportChannel = new DiscordClientWrapper(conf.AdminSystemReportChannel);
            }
            else if (adminChannel is not null)
            {
                adminSystemReportChannel = adminChannel;
            }

            if (conf.AdminUserReportChannel.IsChannel())
            {
                log.Info("Discord admin user report channel is enabled");
                adminUserReportChannel = new DiscordClientWrapper(conf.AdminUserReportChannel);
            }
            else if (adminChannel is not null)
            {
                adminUserReportChannel = adminChannel;
            }

            if (conf.AdminChatLogChannel.IsChannel())
            {
                log.Info("Discord admin chat log channel is enabled");
                adminChatLogChannel = new DiscordClientWrapper(conf.AdminChatLogChannel);
            }
            else if (adminChannel is not null)
            {
                adminChatLogChannel = adminChannel;
            }

            if (conf.AdminActionLogChannel.IsChannel())
            {
                log.Info("Discord admin action log channel is enabled");
                adminActionLogChannel = new DiscordClientWrapper(conf.AdminActionLogChannel);
            }
            else if (adminChannel is not null)
            {
                adminActionLogChannel = adminChannel;
            }

            if (conf.AdminErrorLogChannel.IsChannel())
            {
                log.Info("Discord admin error log channel is enabled");
                adminErrorLogChannel = new DiscordClientWrapper(conf.AdminErrorLogChannel);
            }
            else if (adminChannel is not null)
            {
                adminErrorLogChannel = adminChannel;
            }

            if (conf.LobbyChannel.IsChannel())
            {
                log.Info("Discord lobby is enabled");
                lobbyChannel = new DiscordClientWrapper(conf.LobbyChannel);
            }
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
                ChatManager.Get().OnGlobalChatMessage += SendGlobalChatMessageAsync;
            }

            if (adminChatLogChannel is not null)
            {
                ChatManager.Get().OnChatMessage += SendChatMessageAuditAsync;
            }

            if (adminActionLogChannel is not null)
            {
                AdminManager.Get().OnAdminAction += SendAdminActionAuditAsync;
                AdminManager.Get().OnAdminMessage += SendAdminMessageAuditAsync;
                AdminController.OnAdminPauseQueue += SendAdminPauseQueueAuditAsync;
            }

            if (adminSystemReportChannel is not null)
            {
                AdminController.OnAdminScheduleShutdown += SendAdminScheduleShutdownAuditAsync;
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
                ChatManager.Get().OnGlobalChatMessage -= SendGlobalChatMessageAsync;
            }

            if (adminChatLogChannel is not null)
            {
                ChatManager.Get().OnChatMessage -= SendChatMessageAuditAsync;
            }

            if (adminActionLogChannel is not null)
            {
                AdminManager.Get().OnAdminAction -= SendAdminActionAuditAsync;
                AdminManager.Get().OnAdminMessage -= SendAdminMessageAuditAsync;
                AdminController.OnAdminPauseQueue -= SendAdminPauseQueueAuditAsync;
            }

            if (adminSystemReportChannel is not null)
            {
                AdminController.OnAdminScheduleShutdown -= SendAdminScheduleShutdownAuditAsync;
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

        public async void SendGameReport(LobbyGameInfo gameInfo, string serverName, string serverVersion, LobbyGameSummary gameSummary)
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
                    embeds: new[] {
                        MakeGameReportEmbed(gameInfo, serverName, serverVersion, gameSummary)
                    },
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
                        embeds: new[]
                        {
                            new EmbedBuilder
                            {
                                Title = DiscordLobbyUtils.BuildPlayerCountSummary(status),
                                Color = Color.Green
                            }.Build()
                        },
                        username: "Atlas Reactor")
                    .ContinueWith(x => lastStatus = status);
            }
            catch (Exception e)
            {
                log.Error("Failed to send status to discord webhook", e);
            }
        }

        private void SendGlobalChatMessageAsync(ChatNotification notification)
        {
            _ = SendGlobalChatMessage(notification);
        }

        private async Task SendGlobalChatMessage(ChatNotification notification)
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

        private void SendChatMessageAuditAsync(ChatNotification notification, bool isMuted)
        {
            _ = SendChatMessageAudit(notification, isMuted);
        }

        private async Task SendChatMessageAudit(ChatNotification notification, bool isMuted)
        {
            if (adminChatLogChannel == null || !conf.AdminEnableChatAudit)
            {
                return;
            }
            try
            {
                List<long> recipients = DiscordLobbyUtils.GetMessageRecipients(notification, out string fallback, out string context);
                await adminChatLogChannel.SendMessageAsync(
                    username: notification.SenderHandle,
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = notification.Text,
                        Description = !recipients.IsNullOrEmpty()
                            ? $"to {DiscordLobbyUtils.FormatMessageRecipients(notification.SenderAccountId, recipients)}"
                            : fallback,
                        Color = DiscordLobbyUtils.GetColor(notification.ConsoleMessageType),
                        Footer = new EmbedFooterBuilder { Text = isMuted ? $"MUTED ({context})" : context }
                    }.Build() },
                    threadIdOverride: conf.AdminChatAuditThreadId);
            }
            catch (Exception e)
            {
                log.Error("Failed to send audit chat message to discord webhook", e);
            }
        }

        private void SendAdminActionAuditAsync(long accountId, AdminComponent.AdminActionRecord record)
        {
            _ = SendAdminActionAudit(accountId, record);
        }

        private async Task SendAdminActionAudit(long accountId, AdminComponent.AdminActionRecord record)
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
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = $"{record.ActionType} {account.Handle ?? $"#{accountId}"} for {record.Duration}",
                        Description = record.Description,
                        Color = DiscordUtils.GetLogColor(Level.Warn),
                    }.Build() });
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin action audit message to discord webhook", e);
            }
        }

        private void SendAdminMessageAuditAsync(long accountId, long adminAccountId, string msg)
        {
            _ = SendAdminMessageAudit(accountId, adminAccountId, msg);
        }

        private async Task SendAdminMessageAudit(long accountId, long adminAccountId, string msg)
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
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = $"Admin message for {account.Handle ?? $"#{accountId}"}",
                        Description = msg,
                        Color = DiscordUtils.GetLogColor(Level.Warn),
                    }.Build() });
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin message audit message to discord webhook", e);
            }
        }

        private void SendAdminPauseQueueAuditAsync(long adminAccountId, AdminController.PauseQueueModel action)
        {
            _ = SendAdminPauseQueueAudit(adminAccountId, action);
        }

        private async Task SendAdminPauseQueueAudit(long adminAccountId, AdminController.PauseQueueModel action)
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
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = action.Paused ? "Pause queue" : "Unpause queue",
                        Color = DiscordUtils.GetLogColor(Level.Warn),
                    }.Build() });
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin pause queue audit message to discord webhook", e);
            }
        }

        private void SendAdminScheduleShutdownAuditAsync(long adminAccountId, AdminController.PendingShutdownModel action)
        {
            _ = SendAdminScheduleShutdownAudit(adminAccountId, action);
        }

        private async Task SendAdminScheduleShutdownAudit(long adminAccountId, AdminController.PendingShutdownModel action)
        {
            if (adminSystemReportChannel == null || !conf.AdminEnableAdminAudit)
            {
                return;
            }
            try
            {
                await adminSystemReportChannel.SendMessageAsync(
                    username: LobbyServerUtils.GetHandle(adminAccountId),
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = $"Shutdown: {action.Type}",
                        Color = DiscordUtils.GetLogColor(Level.Warn),
                    }.Build() });
            }
            catch (Exception e)
            {
                log.Error("Failed to send admin schedule shutdown audit message to discord webhook", e);
            }
        }

        public async void SendAdminGameReport(LobbyGameInfo gameInfo, string serverName, string serverVersion, LobbyGameSummary gameSummary)
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
                    || gameInfo.GameConfig.GameType == GameType.Custom)
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
                    msg = string.Join("\n",
                        matchResultsStats.FriendlyStatlines.Select(Format)
                            .Concat(matchResultsStats.EnemyStatlines.Select(Format)));
                }
                await adminSystemReportChannel.SendMessageAsync(
                    msg,
                    false,
                    embeds: new[] {
                        MakeGameReportEmbed(gameInfo, serverName, serverVersion, gameSummary)
                    },
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
                    embeds: new[] { new EmbedBuilder
                    {
                        Description = msg,
                        Color = DiscordUtils.GetLogColor(severity)
                    }.Build() },
                    threadIdOverride: conf.AdminLogThreadId);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send log to Discord: {e}");
            }
        }

        private static Embed MakeGameReportEmbed(LobbyGameInfo gameInfo, string serverName, string serverVersion,
            LobbyGameSummary gameSummary)
        {
            string map = Maps.GetMapName[gameInfo.GameConfig.Map];
            string gameType = gameInfo.GameConfig.GameType.ToString();

            if (gameInfo.GameConfig.SubTypes != null)
            {
                foreach (GameSubType subType in gameInfo.GameConfig.SubTypes)
                {
                    if (subType.Mods != null && subType.Mods.Contains(SubTypeMods.RankedFreelancerSelection))
                    {
                        gameType = "Tournament";
                    }
                }
            }

            EmbedBuilder eb = new EmbedBuilder
            {
                Title = $"Game Result for {gameType} {map ?? gameInfo.GameConfig.Map}",
                Description =
                    $"{RenderGameResult(gameSummary.GameResult)} " +
                    $"{gameSummary.TeamAPoints}-{gameSummary.TeamBPoints} ({gameSummary.NumOfTurns} turns)",
                Color = gameSummary.GameResult.ToString() == "TeamAWon" ? Color.Green : Color.Red
            };

            eb.AddField("Team A", LINE, true);
            eb.AddField("│", "│", true);
            eb.AddField("Team B", LINE, true);
            eb.AddField("**[ Takedowns : Deaths : Deathblows ] [ Damage : Healing : Damage Received ]**", LINE_LONG, false);

            GetTeamsFromGameSummary(gameSummary, out List<PlayerGameSummary> teamA, out List<PlayerGameSummary> teamB);
            int n = Math.Max(teamA.Count, teamB.Count);
            for (int i = 0; i < n; i++)
            {
                GameReportAddPlayer(eb, teamA.ElementAtOrDefault(i));
                eb.AddField("│", "│", true);
                GameReportAddPlayer(eb, teamB.ElementAtOrDefault(i));
            }

            EmbedFooterBuilder footer = new EmbedFooterBuilder
            {
                Text = $"{serverName} - {serverVersion} - {LobbyServerUtils.GameIdString(gameInfo)} - {gameInfo.GameServerProcessCode}"
            };
            eb.Footer = footer;
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
                    Description = message.Message,
                    Color = 16711680
                };
                eb.AddField("Reason", message.Reason, true);
                if (message.ReportedPlayerHandle != null)
                {
                    eb.AddField("Reported Account", $"{message.ReportedPlayerHandle} #{message.ReportedPlayerAccountId}", true);
                }

                Game game = SessionManager.GetClientConnection(accountId)?.CurrentGame;
                if (game != null)
                {
                    eb.AddField("Game", $"{game.Server?.Name} {LobbyServerUtils.GameIdString(game.GameInfo)} Turn {game.GameMetrics.CurrentTurn}", true);
                }
                await adminUserReportChannel.SendMessageAsync(
                    null,
                    false,
                    embeds: new[] { eb.Build() },
                    "Atlas Reactor",
                    threadIdOverride: conf.AdminUserReportThreadId);
            }
            catch (Exception e)
            {
                log.Error("Failed to send user report to discord webhook", e);
            }
        }

        private static void GameReportAddPlayer(EmbedBuilder eb, PlayerGameSummary? player)
        {
            if (player == null)
            {
                eb.AddField("-", "-", true);
                return;
            }

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
            eb.AddField(
                $"{account.Handle} ({player.CharacterName})",
                $"**[ {player.NumAssists} : {player.NumDeaths} : {player.NumKills} ] [ {player.TotalPlayerDamage} : " +
                $"{player.GetTotalHealingFromAbility() + player.TotalPlayerAbsorb} : {player.TotalPlayerDamageReceived} ]**",
                true);
        }

        private static void GetTeamsFromGameSummary(
            LobbyGameSummary gameSummary,
            out List<PlayerGameSummary> teamA,
            out List<PlayerGameSummary> teamB)
        {
            teamA = new List<PlayerGameSummary>();
            teamB = new List<PlayerGameSummary>();

            // Sort into teams, ignore spectators if ever
            foreach (PlayerGameSummary player in gameSummary.PlayerGameSummaryList)
            {
                if (player.IsSpectator())
                {
                    continue;
                }

                if (player.IsInTeamA())
                {
                    teamA.Add(player);
                }
                else
                {
                    teamB.Add(player);
                }
            }
        }
    }
}