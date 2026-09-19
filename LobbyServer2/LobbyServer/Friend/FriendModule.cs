using CentralServer.LobbyServer.Session;
using EvoS.Framework;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Friend;

public class FriendModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FriendModule));
    private readonly IClientConnection _conn;

    public FriendModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<FriendUpdateRequest>(HandleFriendUpdate);
    }

    private void HandleFriendUpdate(FriendUpdateRequest request)
    {
        long friendAccountId = LobbyServerUtils.ResolveAccountId(request.FriendAccountId, request.FriendHandle);
        if (friendAccountId == 0)
        {
            string failure = FriendManager.GetFailTerm(request.FriendOperation);
            string context = failure != null ? "FriendList" : "Global";
            failure ??= "FailedMessage";
            _conn.Send(FriendUpdateResponse.of(
                request,
                LocalizationPayload.Create(failure, context,
                    LocalizationArg_LocalizationPayload.Create(
                        LocalizationPayload.Create("PlayerNotFound", "Invite",
                            LocalizationArg_Handle.Create(request.FriendHandle))))
            ));
            log.Info($"Attempted to {request.FriendOperation} {request.FriendHandle}#{request.FriendAccountId}, but such a user was not found");
            return;
        }

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        if (friendAccountId == _conn.AccountId)
        {
            log.Info($"{account.Handle} attempted to {request.FriendOperation} themselves");
            _conn.Send(
                FriendUpdateResponse.of(
                    request,
                    LocalizationPayload.Create(
                        "CannotFriendYourself",
                        "FriendUpdateResponse")));
            return;
        }

        PersistedAccountData friendAccount = DB.Get().AccountDao.GetAccount(friendAccountId);

        if (account == null || friendAccount == null)
        {
            _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
            log.Info($"Failed to find account {_conn.AccountId} and/or {friendAccountId}");
            return;
        }

        SocialComponent socialComponent = account.SocialComponent;
        switch (request.FriendOperation)
        {
            case FriendOperation.Block:
            {
                bool updated = socialComponent.Block(friendAccountId);
                log.Info($"{account.Handle} blocked {friendAccount.Handle}{(updated ? "" : ", but they were already blocked")}");
                if (updated)
                {
                    DB.Get().AccountDao.UpdateSocialComponent(account);
                    _conn.Send(FriendUpdateResponse.of(request));
                    _conn.Send(FriendManager.GetFriendStatusNotification(_conn.AccountId));
                }
                else
                {
                    _conn.Send(FriendUpdateResponse.of(
                        request,
                        LocalizationPayload.Create("FailedFriendBlock", "FriendList",
                            LocalizationArg_LocalizationPayload.Create(
                                LocalizationPayload.Create("PlayerAlreadyBlocked", "FriendUpdateResponse",
                                    LocalizationArg_Handle.Create(request.FriendHandle))))
                    ));
                }
                return;
            }
            case FriendOperation.Unblock:
            {
                Unblock(request, account, friendAccount);
                return;
            }
            case FriendOperation.Add:
            {
                if (socialComponent.FriendInfo.ContainsKey(friendAccountId))
                {
                    log.Info($"{account.Handle} attempted to add {friendAccount.Handle} to friend list but they are already friends");
                    _conn.Send(
                        FriendUpdateResponse.of(
                            request,
                            LocalizationPayload.Create(
                                "PlayerAlreadyYourFriend",
                                "FriendUpdateResponse",
                                LocalizationArg_Handle.Create(friendAccount.Handle))));
                    return;
                }

                if (friendAccount.SocialComponent.GetOutgoingFriendRequests().Contains(_conn.AccountId))
                {
                    log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list while having an incoming friend request from them");
                    _conn.Send(
                        FriendManager.AddFriend(_conn.AccountId, friendAccountId)
                            ? FriendUpdateResponse.of(request)
                            : FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                if (account.SocialComponent.GetOutgoingFriendRequests().Contains(friendAccountId))
                {
                    log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list while already having an outgoing friend request to them. Ignoring");
                    _conn.Send(FriendUpdateResponse.of(request));
                    return;
                }

                log.Info($"{account.Handle} requested to add {friendAccount.Handle} to friend list");
                if (!FriendManager.AddFriendRequest(_conn.AccountId, friendAccountId))
                {
                    _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                _conn.Send(FriendUpdateResponse.of(request));
                return;
            }
            case FriendOperation.Remove:
            {
                if (socialComponent.IsBlocked(friendAccountId)) // UI bug
                {
                    if (!Unblock(request, account, friendAccount))
                    {
                        _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    return;
                }

                if (socialComponent.FriendInfo.ContainsKey(friendAccountId))
                {
                    log.Info($"{account.Handle} removed {friendAccount.Handle} from friend list");
                    if (!FriendManager.RemoveFriend(_conn.AccountId, friendAccountId))
                    {
                        _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    _conn.Send(FriendUpdateResponse.of(request));
                    return;
                }

                if (socialComponent.GetOutgoingFriendRequests().Contains(friendAccountId))
                {
                    if (!FriendManager.RemoveFriendRequest(_conn.AccountId, friendAccountId))
                    {
                        log.Info($"{account.Handle} attempted to cancel their friend request to {friendAccount.Handle} but the request was not found");
                        _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    log.Info($"{account.Handle} cancelled their friend request to {friendAccount.Handle}");
                    _conn.Send(FriendUpdateResponse.of(request));
                    return;
                }

                if (socialComponent.GetIncomingFriendRequests().Contains(friendAccountId))
                {
                    if (!FriendManager.RemoveFriendRequest(friendAccountId, _conn.AccountId))
                    {
                        log.Info($"{account.Handle} attempted to cancel friend request from {friendAccount.Handle} but the request was not found");
                        _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                        return;
                    }

                    log.Info($"{account.Handle} cancelled friend request from {friendAccount.Handle}");
                    _conn.Send(FriendUpdateResponse.of(request));
                    return;
                }

                log.Info($"{account.Handle} attempted to remove {friendAccount.Handle} from friend list but they aren't friends");
                if (LobbyConfiguration.AreAllOnlineFriends() && LobbyServerUtils.IsVanilla(_conn.AccountId))
                {
                    _conn.Send(new ChatNotification
                    {
                        ConsoleMessageType = ConsoleMessageType.SystemMessage,
                        Text = "We are all friends here. You cannot deny that."
                    });
                    return;
                }

                _conn.Send(
                    FriendUpdateResponse.of(
                        request,
                        LocalizationPayload.Create(
                            "NotFriendsWithPlayer",
                            "FriendUpdateResponse",
                            LocalizationArg_Handle.Create(friendAccount.Handle))));
                return;
            }
            case FriendOperation.Accept:
            {
                if (!FriendManager.RemoveFriendRequest(friendAccountId, _conn.AccountId))
                {
                    log.Info($"{account.Handle} attempted to accept friend request from {friendAccount.Handle} but the request was not found");
                    _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                if (!FriendManager.AddFriend(friendAccountId, _conn.AccountId))
                {
                    log.Info($"{account.Handle} failed to accept friend request from {friendAccount.Handle}");
                    _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                log.Info($"{account.Handle} accepted friend request from {friendAccount.Handle}");
                _conn.Send(FriendUpdateResponse.of(request));
                return;
            }
            case FriendOperation.Reject:
            {
                if (!FriendManager.RemoveFriendRequest(friendAccountId, _conn.AccountId))
                {
                    log.Info($"{account.Handle} attempted to reject friend request from {friendAccount.Handle} but the request was not found");
                    _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                log.Info($"{account.Handle} rejected friend request from {friendAccount.Handle}");
                _conn.Send(FriendUpdateResponse.of(request));
                return;
            }
            case FriendOperation.Note:
            {
                if (!FriendManager.AreFriends(_conn.AccountId, friendAccountId))
                {
                    log.Error($"Failed to save {account.Handle}'s note for {friendAccount.Handle}: not a friend");
                    _conn.Send(
                        FriendUpdateResponse.of(
                            request,
                            LocalizationPayload.Create(
                                "NotFriendsWithPlayer",
                                "FriendUpdateResponse",
                                LocalizationArg_Handle.Create(friendAccount.Handle))));
                    return;
                }

                if (!FriendManager.SetFriendNote(_conn.AccountId, friendAccountId, request.StringData))
                {
                    log.Info($"Failed to save {account.Handle}'s note for {friendAccount.Handle}");
                    _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                    return;
                }

                log.Info($"{account.Handle} note for {friendAccount.Handle}: {
                    FriendManager.GetFriendNote(_conn.AccountId, friendAccountId)}");
                _conn.Send(FriendUpdateResponse.of(request));
                return;
            }
            default:
            {
                log.Warn($"{account.Handle} attempted to {request.FriendOperation} {friendAccount.Handle}, " +
                         $"but this operation is not supported yet");
                _conn.Send(FriendUpdateResponse.of(request, LocalizationPayload.Create("ServerError@Global")));
                return;
            }
        }
    }

    private bool Unblock(FriendUpdateRequest request, PersistedAccountData account, PersistedAccountData friendAccount)
    {
        bool updated = account.SocialComponent.Unblock(friendAccount.AccountId);
        log.Info($"{account.Handle} unblocked {friendAccount.Handle}{(updated ? "" : ", but they weren't blocked")}");
        if (updated)
        {
            DB.Get().AccountDao.UpdateSocialComponent(account);
        }

        _conn.Send(FriendUpdateResponse.of(request));
        _conn.Send(FriendManager.GetFriendStatusNotification(_conn.AccountId));
        return updated;
    }
}
