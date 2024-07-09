using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Chat
{
    class ChatManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ChatManager));

        private static ChatManager _instance;

        public event Action<ChatNotification> OnGlobalChatMessage = delegate { };
        public event Action<ChatNotification, bool> OnChatMessage = delegate { };

        public static ChatManager Get()
        {
            return _instance ??= new ChatManager();
        }

        private ChatManager()
        {
            SessionManager.OnPlayerConnected += Register;
            SessionManager.OnPlayerDisconnected += Unregister;
        }

        ~ChatManager()
        {
            SessionManager.OnPlayerConnected -= Register;
            SessionManager.OnPlayerDisconnected -= Unregister;
        }

        private void Register(LobbyServerProtocol conn)
        {
            conn.OnChatNotification += HandleChatNotification;
            conn.OnGroupChatRequest += HandleGroupChatRequest;
        }

        private void Unregister(LobbyServerProtocol conn)
        {
            // TODO unregister from every client on shutdown?
            conn.OnChatNotification -= HandleChatNotification;
            conn.OnGroupChatRequest -= HandleGroupChatRequest;
        }

        public void HandleChatNotification(LobbyServerProtocol conn, ChatNotification notification)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(conn.AccountId);
            bool isMuted = account.AdminComponent.Muted
                           && notification.ConsoleMessageType != ConsoleMessageType.WhisperChat
                           && notification.ConsoleMessageType != ConsoleMessageType.GroupChat;
            ChatNotification message = new ChatNotification
            {
                SenderAccountId = conn.AccountId,
                SenderHandle = account.Handle,
                ResponseId = notification.RequestId,
                CharacterType = conn.PlayerInfo?.CharacterType ?? account.AccountComponent.LastCharacter,
                ConsoleMessageType = notification.ConsoleMessageType,
                Text = notification.Text,
                EmojisAllowed = InventoryManager.GetUnlockedEmojiIDs(conn.AccountId),
                DisplayDevTag = account.AccountComponent.DisplayDevTag,
            };

            LobbyServerPlayerInfo lobbyServerPlayerInfo = null;
            if (conn.CurrentGame != null)
            {
                lobbyServerPlayerInfo = conn.CurrentGame.GetPlayerInfo(conn.AccountId);
                if (lobbyServerPlayerInfo != null)
                {
                    message.SenderTeam = lobbyServerPlayerInfo.TeamId;
                }
                else
                {
                    log.Error($"{conn.AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} " +
                              $"but they are not in the game they are supposed to be in");
                }
            }

            HashSet<long> recipients = new HashSet<long>();
            HashSet<long> blockedRecipients = new HashSet<long>();

            if (account.Mentor)
            {
                message.SenderHandle = $"<color=green>(Mentor)</color>{message.SenderHandle}";
            }

            switch (notification.ConsoleMessageType)
            {
                case ConsoleMessageType.GlobalChat:
                    {
                        if (!isMuted)
                        {
                            foreach (long player in SessionManager.GetOnlinePlayers())
                            {
                                SendMessageToPlayer(player, message, out _);
                            }
                            OnGlobalChatMessage(message);
                        }
                        else
                        {
                            SendMessageToPlayer(conn.AccountId, message, out _);
                        }
                        break;
                    }
                case ConsoleMessageType.WhisperChat:
                    {
                        // Remove (Dev and Mentor tags) incase they click a name to whisper them
                        notification.RecipientHandle = Regex.Replace(notification.RecipientHandle, @"\((Mentor|Dev)\)", "");

                        long? accountId = SessionManager.GetOnlinePlayerByHandle(notification.RecipientHandle);
                        if (accountId.HasValue && accountId.Value != conn.AccountId)
                        {
                            message.RecipientHandle = notification.RecipientHandle;
                            SendMessageToPlayer(accountId.Value, message, out bool isBlocked);
                            conn.Send(message);
                            (isBlocked ? blockedRecipients : recipients).Add(accountId.Value);
                        }
                        else
                        {
                            log.Warn($"{conn.AccountId} {account.Handle} failed to whisper to {notification.RecipientHandle}");
                        }
                        break;
                    }
                case ConsoleMessageType.GameChat:
                    {
                        if (conn.CurrentGame == null)
                        {
                            log.Warn($"{conn.AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in game");
                            break;
                        }
                        if (!isMuted)
                        {
                            foreach (long accountId in conn.CurrentGame.GetPlayersDistinct())
                            {
                                SendMessageToPlayer(accountId, message, out bool isBlocked);
                                if (accountId != conn.AccountId)
                                {
                                    (isBlocked ? blockedRecipients : recipients).Add(accountId);
                                }
                            }
                        }
                        else
                        {
                            SendMessageToPlayer(conn.AccountId, message, out _);
                            blockedRecipients.UnionWith(conn.CurrentGame.GetPlayers());
                        }
                        break;
                    }
                case ConsoleMessageType.GroupChat:
                    {
                        GroupInfo group = GroupManager.GetPlayerGroup(conn.AccountId);
                        if (group == null || group.IsSolo())
                        {
                            log.Error($"{conn.AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in a group");
                            break;
                        }
                        foreach (long member in group.Members)
                        {
                            SendMessageToPlayer(member, message, out bool isBlocked);
                            if (member != conn.AccountId)
                            {
                                (isBlocked ? blockedRecipients : recipients).Add(member);
                            }
                        }
                        break;
                    }
                case ConsoleMessageType.TeamChat:
                    {
                        if (conn.CurrentGame == null)
                        {
                            log.Warn($"{conn.AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} while not in game");
                            break;
                        }
                        if (lobbyServerPlayerInfo == null)
                        {
                            log.Error($"{conn.AccountId} {account.Handle} attempted to use {notification.ConsoleMessageType} " +
                                      $"but they are not in the game they are supposed to be in");
                            break;
                        }

                        List<long> teammateAccountIds = conn.CurrentGame.GetPlayers(lobbyServerPlayerInfo.TeamId).ToList();
                        if (!isMuted)
                        {
                            foreach (long teammateAccountId in teammateAccountIds)
                            {
                                SendMessageToPlayer(teammateAccountId, message, out bool isBlocked);
                                if (teammateAccountId != conn.AccountId)
                                {
                                    (isBlocked ? blockedRecipients : recipients).Add(teammateAccountId);
                                }
                            }
                        }
                        else
                        {
                            SendMessageToPlayer(conn.AccountId, message, out _);
                            blockedRecipients.UnionWith(teammateAccountIds);
                        }
                        break;
                    }
                default:
                    {
                        log.Error($"Console message type {notification.ConsoleMessageType} is not supported yet!");
                        log.Info(DefaultJsonSerializer.Serialize(notification));
                        break;
                    }
            }

            DB.Get().ChatHistoryDao.Save(new ChatHistoryDao.Entry(
                message,
                DateTime.UtcNow,
                conn.CurrentGame?.GameInfo?.GameServerProcessCode,
                recipients,
                blockedRecipients,
                account.AdminComponent.Muted));

            OnChatMessage(message, isMuted);
        }

        public void HandleGroupChatRequest(LobbyServerProtocol conn, GroupChatRequest request)
        {
            conn.Send(new GroupChatResponse
            {
                Text = request.Text,
                ResponseId = request.RequestId,
                Success = true
            });

            PersistedAccountData account = DB.Get().AccountDao.GetAccount(conn.AccountId);

            ChatNotification message = new ChatNotification
            {
                SenderAccountId = conn.AccountId,
                EmojisAllowed = request.RequestedEmojis,
                CharacterType = conn.PlayerInfo?.CharacterType ?? account.AccountComponent.LastCharacter,
                ConsoleMessageType = ConsoleMessageType.GroupChat,
                SenderHandle = account.Handle,
                Text = request.Text
            };

            HashSet<long> recipients = new HashSet<long>();
            HashSet<long> blockedRecipients = new HashSet<long>();

            foreach (long accountID in GroupManager.GetPlayerGroup(conn.AccountId).Members)
            {
                SendMessageToPlayer(accountID, message, out bool isBlocked);
                (isBlocked ? blockedRecipients : recipients).Add(accountID);
            }

            DB.Get().ChatHistoryDao.Save(new ChatHistoryDao.Entry(
                message,
                DateTime.UtcNow,
                conn.CurrentGame?.GameInfo?.GameServerProcessCode,
                recipients,
                blockedRecipients,
                account.AdminComponent.Muted));

            OnChatMessage(message, false);
        }

        private void SendMessageToPlayer(long player, ChatNotification message, out bool isBlocked)
        {
            SocialComponent socialComponent = DB.Get().AccountDao.GetAccount(player)?.SocialComponent;
            isBlocked = socialComponent?.IsBlocked(message.SenderAccountId) == true;
            if (!isBlocked)
            {
                SessionManager.GetClientConnection(player)?.Send(message);
            }
        }
    }
}
