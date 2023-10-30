using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using Discord;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Discord
{
    public static class DiscordLobbyUtils
    {
        public static List<long> GetMessageRecipients(ChatNotification notification, out string fallback, out string context)
        {
            fallback = null;
            context = null;
            switch (notification.ConsoleMessageType)
            {
                case ConsoleMessageType.GlobalChat:
                    return null;
                case ConsoleMessageType.WhisperChat:
                    fallback = notification.RecipientHandle;
                    long? accountId = SessionManager.GetOnlinePlayerByHandle(notification.RecipientHandle);
                    return accountId.HasValue ? new List<long> { accountId.Value } : null;
                case ConsoleMessageType.GameChat:
                case ConsoleMessageType.TeamChat:
                {
                    LobbyServerProtocol conn = SessionManager.GetClientConnection(notification.SenderAccountId);
                    LobbyServerPlayerInfo playerInfo = null;
                    string serverInfo = "";
                    if (conn.CurrentGame != null)
                    {
                        serverInfo = $"{conn.CurrentGame.Server?.Name ?? "<no server>"} " +
                                     $"{LobbyServerUtils.GameIdString(conn.CurrentGame.GameInfo)}\n" +
                                     $"{conn.CurrentGame.GameInfo.GameConfig.Map} " +
                                     $"{conn.CurrentGame.GameInfo.GameConfig.GameType} ";
                        playerInfo = conn.CurrentGame.GetPlayerInfo(notification.SenderAccountId);
                    }

                    string team = "";
                    if (notification.ConsoleMessageType != ConsoleMessageType.GameChat && playerInfo != null)
                    {
                        team = $"{playerInfo.TeamId} ";
                    }
                    
                    context = $"{serverInfo}{team}";

                    if (conn.CurrentGame != null)
                    {
                        if (notification.ConsoleMessageType == ConsoleMessageType.GameChat)
                        {
                            return conn.CurrentGame
                                .GetPlayers()
                                .Where(id => id != notification.SenderAccountId)
                                .ToList();
                        }
                        if (playerInfo != null)
                        {
                            return conn.CurrentGame
                                .GetPlayers(playerInfo.TeamId)
                                .Where(id => id != notification.SenderAccountId)
                                .ToList();
                        }
                    }
                    return null;
                }
                case ConsoleMessageType.GroupChat:
                {
                    fallback = "<group>";
                    LobbyPlayerGroupInfo group = GroupManager.GetGroupInfo(notification.SenderAccountId);
                    return group != null
                        ? group.Members
                                .Select(member => member.AccountID)
                                .Where(id => id != notification.SenderAccountId)
                                .ToList()
                        : new List<long>();
                }
                default:
                    fallback = $"<{notification.ConsoleMessageType}>";
                    return new List<long>();
            }
        }

        public static string FormatMessageRecipients(long sender, List<long> recipients)
        {
            List<string> recipientsStrs = new List<string>();
            foreach (long recipient in recipients)
            {
                PersistedAccountData acc = DB.Get().AccountDao.GetAccount(recipient);
                if (acc == null) continue;
                bool blocked = acc.SocialComponent.IsBlocked(sender);
                recipientsStrs.Add(blocked ? $"~~{acc.Handle}~~" : acc.Handle);
            }

            return string.Join(", ", recipientsStrs.ToArray());
        }

        public static Color GetColor(ConsoleMessageType messageType)
        {
            switch (messageType)
            {
                case ConsoleMessageType.GlobalChat:
                    return Color.Default;
                case ConsoleMessageType.WhisperChat:
                    return Color.Purple;
                case ConsoleMessageType.GameChat:
                    return Color.Gold;
                case ConsoleMessageType.TeamChat:
                    return Color.Blue;
                case ConsoleMessageType.GroupChat:
                    return Color.Green;
                default:
                    return Color.DarkRed;
            }
        }
        
        public static string BuildPlayerCountSummary(Status status)
        {
            return $"{status.totalPlayers} player{(status.totalPlayers != 1 ? "s" : "")} online, " +
                   $"{status.inQueue} in queue, {status.inGame} in game";
        }

        public static Status GetStatus()
        {
            int inGame = 0;
            int inQueue = 0;
            int online = 0;
            foreach (long accountId in SessionManager.GetOnlinePlayers())
            {
                LobbyServerProtocol conn = SessionManager.GetClientConnection(accountId);
                if (conn == null) continue;
                online++;
                if (conn.IsInQueue()) inQueue++;
                else if (conn.IsInGame()) inGame++;
            }

            Status status = new Status { totalPlayers = online, inGame = inGame, inQueue = inQueue };
            return status;
        }

        public struct Status
        {
            public int totalPlayers;
            public int inQueue;
            public int inGame;
        }
    }
}