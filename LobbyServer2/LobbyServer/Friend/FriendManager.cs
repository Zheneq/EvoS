using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Friend
{
    public enum PlayerOnlineStatus
    {
        Online,
        Away,
        Busy
    }
    
    class FriendManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FriendManager));
        
        private static readonly Dictionary<string, PlayerOnlineStatus> StatusMap = Enum
            .GetValues<PlayerOnlineStatus>()
            .ToDictionary((PlayerOnlineStatus e) => e.ToString());
        
        private static readonly ConcurrentDictionary<long, byte> PendingUpdate = new ConcurrentDictionary<long, byte>();

        public const string Status_InGame = "In Game";

        public static FriendStatusNotification GetFriendStatusNotification(long accountId)
        {
            FriendStatusNotification notification = new FriendStatusNotification()
            {
                FriendList = GetFriendList(accountId)
            };

            return notification;
        }

        public static FriendList GetFriendList(long accountId)
        {
            SocialComponent socialComponent = DB.Get().AccountDao.GetAccount(accountId)?.SocialComponent;
            bool isVanilla = LobbyServerUtils.IsVanilla(accountId);
            FriendList friendList = new FriendList
            {
                Friends = GetFriends(accountId)
                    .Select(id => DB.Get().AccountDao.GetAccount(id))
                    .ToDictionary(acc => acc.AccountId,
                        acc =>
                        {
                            LobbyServerProtocol conn = SessionManager.GetClientConnection(acc.AccountId);
                            SocialComponent.FriendData data = null;
                            socialComponent?.FriendInfo.TryGetValue(acc.AccountId, out data);
                            return new FriendInfo
                            {
                                FriendAccountId = acc.AccountId,
                                FriendHandle = acc.Handle,
                                FriendStatus = GetFriendStatus(socialComponent, acc, isVanilla),
                                IsOnline = conn != null,
                                StatusString = GetStatusString(conn),
                                FriendNote = data?.LastSeenNote,
                                BannerID = acc.AccountComponent.SelectedBackgroundBannerID,
                                EmblemID = acc.AccountComponent.SelectedForegroundBannerID,
                                TitleID = acc.AccountComponent.SelectedTitleID,
                                TitleLevel = acc.AccountComponent.TitleLevels.GetValueOrDefault(acc.AccountComponent.SelectedTitleID, 0),
                                RibbonID = acc.AccountComponent.SelectedRibbonID,
                            };
                        }),
                IsDelta = false
            };

            return friendList;
        }

        private static FriendStatus GetFriendStatus(SocialComponent socialComponent, PersistedAccountData otherAccount, bool isVanilla)
        {
            if (socialComponent is null)
            {
                return FriendStatus.Unknown;
            }
            
            if (socialComponent.IsBlocked(otherAccount.AccountId))
            {
                return FriendStatus.Blocked;
            }

            if (socialComponent.IncomingFriendRequests.Contains(otherAccount.AccountId))
            {
                return FriendStatus.RequestReceived;
            }

            if (socialComponent.OutgoingFriendRequests.Contains(otherAccount.AccountId))
            {
                return FriendStatus.RequestSent;
            }

            if (isVanilla || socialComponent.FriendInfo.ContainsKey(otherAccount.AccountId))
            {
                return FriendStatus.Friend;
            }

            return FriendStatus.OnlineNonFriend;
        }

        public static HashSet<long> GetFriends(long accountId)
        {
            SocialComponent socialComponent = DB.Get().AccountDao.GetAccount(accountId)?.SocialComponent;
            HashSet<long> result = socialComponent?.FriendInfo.Keys.ToHashSet() ?? new();

            if (socialComponent is not null)
            {
                result.UnionWith(socialComponent.GetIncomingFriendRequests());
                result.UnionWith(socialComponent.GetOutgoingFriendRequests());
                result.UnionWith(socialComponent.GetBlockedAccounts());
            }
            
            if (LobbyConfiguration.AreAllOnlineFriends())
            {
                result.UnionWith(SessionManager.GetOnlinePlayers().Where(id => id != accountId));
            }

            return result;
        }

        public static string GetStatusString(LobbyServerProtocol client)
        {
            if (client == null)
            {
                return "Offline";
            }
            if (client.IsInGame())
            {
                return client.IsInCharacterSelect() ? "Character Select" : Status_InGame;
            }
            if (client.IsInQueue())
            {
                return "Queued";
            }
            if (client.IsInGroup())
            {
                return "GroupChatRoom";  // No localization for "In Group" status so we have to borrow this one
            }
            return client.Status.ToString();
        }

        public static PlayerUpdateStatusResponse OnPlayerUpdateStatusRequest(LobbyServerProtocol client, PlayerUpdateStatusRequest request)
        {
            bool success = StatusMap.TryGetValue(request.StatusString, out PlayerOnlineStatus status);
            PlayerUpdateStatusResponse response = new PlayerUpdateStatusResponse()
            {
                AccountId = client.AccountId,
                StatusString = request.StatusString,
                ResponseId = request.RequestId,
                Success = success
            };
            
            if (success)
            {
                client.Status = status;
                MarkForUpdate(client);
            }

            return response;
        }

        public static string GetFailTerm(FriendOperation op)
        {
            switch (op)
            {
                case FriendOperation.Accept:
                    return "FailedFriendAccept";
                case FriendOperation.Add:
                    return "FailedFriendAdd";
                case FriendOperation.Reject:
                    return "FailedFriendReject";
                case FriendOperation.Remove:
                    return "FailedFriendRemove";
                case FriendOperation.Block:
                    return "FailedFriendBlock";
                default:
                    return null;
            }
        }

        public static void MarkForUpdate(LobbyServerProtocol client)
        {
            MarkForUpdate(client.AccountId);
        }

        public static void MarkForUpdate(long accountId)
        {
            lock (PendingUpdate)
            {
                PendingUpdate.TryAdd(accountId, 0);
            }
        }

        public static HashSet<long> GetAndResetPendingUpdate()
        {
            lock (PendingUpdate)
            {
                HashSet<long> result = PendingUpdate.Keys.ToHashSet();
                PendingUpdate.Clear();
                return result;
            }
        }

        public static bool AddFriendRequest(long requester, long requestee)
        {
            PersistedAccountData requesterAccount = DB.Get().AccountDao.GetAccount(requester);
            PersistedAccountData requesteeAccount = DB.Get().AccountDao.GetAccount(requestee);

            if (requesterAccount is null || requesteeAccount is null)
            {
                log.Error($"Add Friend Request failed: {requester} > {requestee}; {requesterAccount?.Handle} > {requesteeAccount?.Handle}");
                return false;
            }

            bool updated = requesteeAccount.SocialComponent.AddIncomingFriendRequest(requester);
            updated |= requesterAccount.SocialComponent.AddOutgoingFriendRequest(requestee);

            if (!updated)
            {
                log.Info($"Add Friend Request failed: {requesterAccount.Handle} > {requesteeAccount.Handle}; no update");
                return false;
            }
            
            Save(requesteeAccount, requesterAccount);

            log.Info($"Add Friend Request: {requesterAccount.Handle} > {requesteeAccount.Handle}");
            return true;
        }
    
        public static bool RemoveFriendRequest(long requester, long requestee)
        {
            PersistedAccountData requesterAccount = DB.Get().AccountDao.GetAccount(requester);
            PersistedAccountData requesteeAccount = DB.Get().AccountDao.GetAccount(requestee);

            if (requesterAccount is null || requesteeAccount is null)
            {
                log.Error($"Remove Friend Request failed: {requester} > {requestee}; {requesterAccount?.Handle} > {requesteeAccount?.Handle}");
                return false;
            }

            bool updated = requesteeAccount.SocialComponent.RemoveIncomingFriendRequest(requester);
            updated |= requesterAccount.SocialComponent.RemoveOutgoingFriendRequest(requestee);

            if (!updated)
            {
                log.Info($"Remove Friend Request failed: {requesterAccount.Handle} > {requesteeAccount.Handle}; no update");
                return false;
            }
            
            Save(requesteeAccount, requesterAccount);

            log.Info($"Remove Friend Request: {requesterAccount.Handle} > {requesteeAccount.Handle}");
            return true;
        }

        public static bool AddFriend(long accountIdA, long accountIdB)
        {
            PersistedAccountData accountA = DB.Get().AccountDao.GetAccount(accountIdA);
            PersistedAccountData accountB = DB.Get().AccountDao.GetAccount(accountIdB);

            if (accountA is null || accountB is null)
            {
                log.Error($"Add Friend failed: {accountIdA} + {accountIdB}; {accountA?.Handle} + {accountB?.Handle}");
                return false;
            }

            bool updated = accountA.SocialComponent.FriendInfo.TryAdd(accountIdB, SocialComponent.FriendData.of(accountB));
            updated |= accountB.SocialComponent.FriendInfo.TryAdd(accountIdA, SocialComponent.FriendData.of(accountA));

            if (!updated)
            {
                log.Info($"Add Friend failed: {accountA.Handle} + {accountB.Handle}; no update");
                return false;
            }
                    
            Save(accountA, accountB);

            log.Info($"Add Friend: {accountA.Handle} + {accountB.Handle}");
            return true;
        }
    
        public static bool RemoveFriend(long accountIdA, long accountIdB)
        {
            PersistedAccountData accountA = DB.Get().AccountDao.GetAccount(accountIdA);
            PersistedAccountData accountB = DB.Get().AccountDao.GetAccount(accountIdB);

            if (accountA is null || accountB is null)
            {
                log.Error($"Remove Friend failed: {accountIdA} - {accountIdB}; {accountA?.Handle} - {accountB?.Handle}");
                return false;
            }

            bool updated = accountA.SocialComponent.FriendInfo.Remove(accountIdB);
            updated |= accountB.SocialComponent.FriendInfo.Remove(accountIdA);

            if (!updated)
            {
                log.Info($"Remove Friend failed: {accountA.Handle} - {accountB.Handle}; no update");
                return false;
            }
                    
            Save(accountA, accountB);

            log.Info($"Remove Friend: {accountA.Handle} - {accountB.Handle}");
            return true;
        }

        private static void Save(PersistedAccountData accountA, PersistedAccountData accountB)
        {
            DB.Get().AccountDao.UpdateSocialComponent(accountA);
            DB.Get().AccountDao.UpdateSocialComponent(accountB);

            ClientNotifier.Get().RefreshFriendList(accountA.AccountId);
            ClientNotifier.Get().RefreshFriendList(accountB.AccountId);
        }

        public static bool SetFriendNote(long accountId, long friendAccountId, string note)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account is null)
            {
                log.Error($"Set Friend Note failed: {accountId} not found");
                return false;
            }
            
            if (!AreFriends(accountId, friendAccountId))
            {
                log.Error($"Set Friend Note failed: {account} is not friends with {friendAccountId}");
                return false;
            }
            
            account.SocialComponent.UpdateNote(friendAccountId, note);
            DB.Get().AccountDao.UpdateSocialComponent(account);

            return true;
        }

        public static string GetFriendNote(long accountId, long friendAccountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account is null)
            {
                log.Error($"Get Friend Note failed: {accountId} not found");
                return null;
            }
            
            return account.SocialComponent.GetFriendInfoOrNull(friendAccountId)?.LastSeenNote;
        }

        public static bool AreFriends(long accountIdA, long accountIdB)
        {
            PersistedAccountData accountA = DB.Get().AccountDao.GetAccount(accountIdA);
            PersistedAccountData accountB = DB.Get().AccountDao.GetAccount(accountIdB);

            if (accountA is null || accountB is null)
            {
                log.Error($"Are Friends failed: {accountIdA} = {accountIdB}; {accountA?.Handle} = {accountB?.Handle}");
                return false;
            }

            bool areFriendsA = accountA.SocialComponent.FriendInfo.ContainsKey(accountIdB);
            bool areFriendsB = accountB.SocialComponent.FriendInfo.ContainsKey(accountIdA);

            if (areFriendsA != areFriendsB)
            {
                log.Error($"Are Friends: {accountIdA} = {accountIdB}; friendship is not mutual, fixing");
                RemoveFriendRequest(accountIdA, accountIdB);
                RemoveFriendRequest(accountIdB, accountIdA);
                AddFriend(accountIdA, accountIdB);
            }

            return areFriendsA || areFriendsB;
        }
    }
}
