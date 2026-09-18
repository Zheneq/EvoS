using System;
using System.Collections.Concurrent;
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
    partial class ChatManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ChatManager));

        private static ChatManager _instance;

        public event Action<ChatNotification> OnGlobalChatMessage = delegate { };
        public event Action<ChatNotification, bool> OnChatMessage = delegate { };
        public event Action<ChatNotification> OnBroadcastMessage = delegate { };

        public static readonly string BotHandlePrefix = TmpSprite.Tag(TmpSpriteId.Iso, 24);
        private static readonly char BotHandleRenderedPrefix = TmpSprite.Rendered(TmpSpriteId.Iso);
        private static readonly string MentorHandlePrefix = TmpSprite.Tag(TmpSpriteId.Mentor, 24);

        private const string DevPrefixRegex = @"\(.*?\)";

        [GeneratedRegex("^(?:" + TmpSprite.TagRegex + "|" + TmpSprite.RenderedRegex + "|" + DevPrefixRegex + @")\s*")]
        private static partial Regex HandlePrefixRegex();

        private record WhisperHandlerEntry(long SenderAccountId, string RecipientHandle, Action<ChatNotification> Callback);
        private readonly ConcurrentDictionary<(long, string), WhisperHandlerEntry> _whisperHandlers = new();

        public void RegisterWhisperHandler(long senderAccountId, string recipientHandle, Action<ChatNotification> callback = null)
        {
            _whisperHandlers[(senderAccountId, recipientHandle)] = new WhisperHandlerEntry(senderAccountId, recipientHandle, callback);
        }

        public void UnregisterWhisperHandler(long senderAccountId, string recipientHandle)
        {
            _whisperHandlers.TryRemove((senderAccountId, recipientHandle), out _);
        }

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

                            // Remove Mentor icon
                            message.SenderHandle = HandlePrefixRegex().Replace(message.SenderHandle, "");

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
                        FixWhisperChatNotification(notification);

                        // Clean the recipient handle by removing (mentor icon) and (Dev) tag
                        string actualRecipientHandle = HandlePrefixRegex().Replace(notification.RecipientHandle, "");
                        message.RecipientHandle = actualRecipientHandle;
                        message.Text = notification.Text;
                        
                        if (notification.RecipientHandle.StartsWith(BotHandleRenderedPrefix)
                            || notification.RecipientHandle.StartsWith(BotHandlePrefix))
                        {
                            message.RecipientHandle = BotHandlePrefix + actualRecipientHandle;
                            conn.Send(message);
                            if (_whisperHandlers.TryGetValue((conn.AccountId, actualRecipientHandle), out WhisperHandlerEntry handlerEntry))
                            {
                                log.Info($"Invoking chat bot {actualRecipientHandle} callback with {message}");
                                handlerEntry.Callback?.Invoke(message);
                            }
                            recipients.Add(0);
                        }
                        else
                        {
                            long? accountId = SessionManager.GetOnlinePlayerByHandleOrUsername(actualRecipientHandle);

                            if (accountId.HasValue && accountId.Value != conn.AccountId)
                            {
                                SendMessageToPlayer(accountId.Value, message, out bool isBlocked);
                                conn.Send(message);
                                (isBlocked ? blockedRecipients : recipients).Add(accountId.Value);
                            }
                            else 
                            {
                                log.Warn($"{conn.AccountId} {account.Handle} failed to whisper to {actualRecipientHandle}");
                                conn.SendSystemMessage(
                                    LocalizationPayload.Create(
                                        "FailedMessage",
                                        "Global",
                                        LocalizationArg_LocalizationPayload.Create(
                                            GroupMessages.PlayerNotFound(actualRecipientHandle))));
                            }
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

            // Remove prefixes
            message.SenderHandle = HandlePrefixRegex().Replace(message.SenderHandle, "");

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
                ClientNotifier.Get().Send(player, message);
            }
        }

        public void Broadcast(string msg)
        {
            ChatNotification message = new ChatNotification
            {
                SenderAccountId = 0,
                ConsoleMessageType = ConsoleMessageType.BroadcastMessage,
                Text = msg,
            };

            SessionManager.Broadcast(message);

            DB.Get().ChatHistoryDao.Save(new ChatHistoryDao.Entry(
                message,
                DateTime.UtcNow,
                null,
                new HashSet<long>(),
                new HashSet<long>(),
                false));

            OnBroadcastMessage(message);
        }

        public void BroadcastToPlayer(long accountId, string msg)
        {
            LobbySessionInfo session = SessionManager.GetSessionInfo(accountId);
            if (session == null)
            {
                log.Error($"Cannot broadcast to player: accountId={accountId} is not online");
                return;
            }

            ChatNotification message = new ChatNotification
            {
                SenderAccountId = 0,
                RecipientHandle = session.Handle,
                ConsoleMessageType = ConsoleMessageType.BroadcastMessage,
                Text = msg,
            };

            ClientNotifier.Get().Send(accountId, message);

            DB.Get().ChatHistoryDao.Save(new ChatHistoryDao.Entry(
                message,
                DateTime.UtcNow,
                null,
                new HashSet<long> { accountId },
                new HashSet<long>(),
                false));

            OnBroadcastMessage(message);
        }

        public void SendSystemWhisper(string senderHandle, long recipientAccountId, string text)
        {
            LobbySessionInfo session = SessionManager.GetSessionInfo(recipientAccountId);
            if (session == null)
            {
                log.Error($"Cannot send whisper: recipient={recipientAccountId} is not online");
                return;
            }

            ChatNotification message = new ChatNotification
            {
                SenderAccountId = 0,
                SenderHandle = BotHandlePrefix + senderHandle,
                RecipientHandle = session.Handle,
                ConsoleMessageType = ConsoleMessageType.WhisperChat,
                Text = text,
                EmojisAllowed = InventoryManager.GetUnlockedEmojiIDs(0),
                DisplayDevTag = false,
            };

            ClientNotifier.Get().Send(recipientAccountId, message);

            DB.Get().ChatHistoryDao.Save(new ChatHistoryDao.Entry(
                message,
                DateTime.UtcNow,
                null,
                new HashSet<long> { recipientAccountId },
                new HashSet<long>(),
                false));

            OnChatMessage(message, false);
        }

        private static readonly Regex SpriteTagRegex = new ("^(" + TmpSprite.TagRegex.Replace("<", @"<\s") + @"[^\s]+)\s");

        [GeneratedRegex(@"<\s")]
        private static partial Regex HandleFixRegex();

        private const string BrokenHandle = "<";
        
        /**
         * Depending on what player does, reply handle with icon can either start with a rendered sprite
         * or just be "<" with the rest of unrendered sprite prepended to the actual message
         * (with a bunch of spaces sprinkled in)
         */
        private static void FixWhisperChatNotification(ChatNotification notification)
        {
            if (notification.RecipientHandle != BrokenHandle)
            {
                return;
            }

            Match match = SpriteTagRegex.Match(BrokenHandle + ' ' + notification.Text);
            if (match.Success)
            {
                notification.RecipientHandle = HandleFixRegex().Replace(match.Groups[0].Value, "<").Trim();
                notification.Text = notification.Text[(match.Groups[0].Value.Length - 2)..];
            }
        }
    }
}
