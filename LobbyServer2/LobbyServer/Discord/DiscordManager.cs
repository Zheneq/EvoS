using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Chat;
using Discord;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

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
        private readonly DiscordClientWrapper discordBot;

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

            if (!conf.UseDiscordBot)
            {
                log.Info("Discord bot is not enabled");
            }
            else 
            {
                if (conf.BotToken.IsNullOrEmpty() || conf.BotToken.Length < 70 || !conf.BotChannelId.HasValue || conf.BotChannelId == 0)
                {
                    log.Info("Discord bot is not configured correctly");
                } 
                else
                {
                    // Init bot but we dont use it for anything not yet anyway we just want chat from discord to atlas and commands
                    discordBot = new DiscordClientWrapper(conf);
                }
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

        public void Start()
        {
            if (lobbyChannel != null)
            {
                _ = SendServerStatusLoop(cancelTokenSource.Token);
                ChatManager.Get().OnGlobalChatMessage += SendGlobalChatMessageAsync;
            }
            if (adminChannel != null)
            {
                ChatManager.Get().OnChatMessage += SendChatMessageAuditAsync;
            }
        }

        public void Shutdown()
        {
            if (lobbyChannel != null)
            {
                ChatManager.Get().OnGlobalChatMessage -= SendGlobalChatMessageAsync;
            }
            if (adminChannel != null)
            {
                ChatManager.Get().OnChatMessage -= SendChatMessageAuditAsync;
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
                        embeds: new []
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

        private void SendChatMessageAuditAsync(ChatNotification notification)
        {
            _ = SendChatMessageAudit(notification);
        }

        private async Task SendChatMessageAudit(ChatNotification notification)
        {
            if (adminChannel == null || !conf.AdminEnableChatAudit)
            {
                return;
            }
            try
            {
                List<long> recipients = DiscordLobbyUtils.GetMessageRecipients(notification, out string fallback, out string context);
                await adminChannel.SendMessageAsync(
                    username: notification.SenderHandle,
                    embeds: new[] { new EmbedBuilder
                    {
                        Title = notification.Text,
                        Description = !recipients.IsNullOrEmpty()
                            ? $"to {DiscordLobbyUtils.FormatMessageRecipients(notification.SenderAccountId, recipients)}"
                            : fallback,
                        Color = DiscordLobbyUtils.GetColor(notification.ConsoleMessageType),
                        Footer = new EmbedFooterBuilder { Text = context }
                    }.Build() },
                    threadIdOverride: conf.AdminChatAuditThreadId);
            }
            catch (Exception e)
            {
                log.Error("Failed to send audit chat message to discord webhook", e);
            }
        }

        private static Embed MakeGameReportEmbed(LobbyGameInfo gameInfo, string serverName, string serverVersion,
            LobbyGameSummary gameSummary)
        {
            string map = Maps.GetMapName[gameInfo.GameConfig.Map];
            EmbedBuilder eb = new EmbedBuilder
            {
                Title = $"Game Result for {map ?? gameInfo.GameConfig.Map}",
                Description =
                    $"{(gameSummary.GameResult.ToString() == "TeamAWon" ? "Team A Won" : "Team B Won")} " +
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
                Text = $"{serverName} - {serverVersion} - {new DateTime(gameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}"
            };
            eb.Footer = footer;
            return eb.Build();
        }
        
        public async void SendPlayerFeedback(long accountId, ClientFeedbackReport message)
        {
            if (adminChannel == null || !conf.AdminEnableUserReports)
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
                await adminChannel.SendMessageAsync(
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