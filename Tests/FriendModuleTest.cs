using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Friend;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="FriendModule"/> handlers.
/// Joined to [Collection("ClientNotifierSeam")] because <see cref="FriendManager"/> triggers
/// friend-list updates via the global <see cref="IClientNotifier"/>; serializing them avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class FriendModuleTest : EvosTest
{
    public FriendModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds (> 11_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 11_000_000L;

    /// <summary>
    /// Minimal IHandlerRegistry that lets tests dispatch messages directly into a module.
    /// </summary>
    private sealed class CapturingRegistry : IHandlerRegistry
    {
        private readonly Dictionary<Type, Action<WebSocketMessage>> _handlers = new();

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            _handlers[typeof(T)] = msg => handler((T)msg);
        }

        public void Dispatch<T>(T message) where T : WebSocketMessage
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                handler(message);
            else
                throw new InvalidOperationException($"No handler registered for {typeof(T).Name}");
        }
    }

    private static (FriendModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new FriendModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static PersistedAccountData MakeAccount(long accountId)
    {
        var account = new PersistedAccountData
        {
            AccountId = accountId,
            Handle = $"TestFriend#{accountId}",
            UserName = $"TestFriend{accountId}",
            AccountComponent = new AccountComponent(),
            SocialComponent = new SocialComponent(),
        };
        DB.Get().AccountDao.CreateAccount(account);
        return account;
    }

    // Helper: assert LocalizationPayload fields.
    private static void AssertLocalization(LocalizationPayload payload, string expectedTerm, string expectedContext)
    {
        Assert.NotNull(payload);
        Assert.Equal(expectedTerm, payload.Term);
        Assert.Equal(expectedContext, payload.Context);
    }

    // Case 1: FriendUpdateRequest with unknown FriendHandle and FriendAccountId = 0 →
    // one FriendUpdateResponse with localized PlayerNotFound error (Add path).
    [Fact]
    public void FriendUpdate_UnknownHandle_PlayerNotFoundError()
    {
        using var scope = new ClientNotifierScope();

        long accountId = UniqueId();
        MakeAccount(accountId);
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new FriendUpdateRequest
        {
            FriendHandle = "NoSuchPlayer#9999",
            FriendAccountId = 0,
            FriendOperation = FriendOperation.Add,
            RequestId = 1
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<FriendUpdateResponse>(conn.Sent[0]);
        // The outer localization wraps a PlayerNotFound inner payload
        Assert.NotNull(response.LocalizedFailure);
        // For Add op GetFailTerm returns "FailedFriendAdd", context "FriendList"
        AssertLocalization(response.LocalizedFailure, "FailedFriendAdd", "FriendList");
    }

    // Case 2: FriendOperation.Add from AccountA targeting AccountB (no prior relationship) →
    // FriendUpdateResponse success (no LocalizedFailure). The RefreshFriendList notifications
    // go to both parties via ClientNotifier — we do not assert on the recording connection's
    // FriendListRefreshes count since AddFriendRequest goes through the notifier, not the
    // module's direct connection.
    [Fact]
    public void FriendUpdate_Add_NoPriorRelationship_SuccessResponse()
    {
        using var scope = new ClientNotifierScope();

        long accountIdA = UniqueId();
        long accountIdB = UniqueId();
        MakeAccount(accountIdA);
        MakeAccount(accountIdB);
        GroupManager.CreateGroup(accountIdA);
        GroupManager.CreateGroup(accountIdB);
        var (_, conn, registry) = MakeModule(accountIdA);

        registry.Dispatch(new FriendUpdateRequest
        {
            FriendHandle = $"TestFriend#{accountIdB}",
            FriendAccountId = accountIdB,
            FriendOperation = FriendOperation.Add,
            RequestId = 2
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<FriendUpdateResponse>(conn.Sent[0]);
        Assert.Null(response.LocalizedFailure);
    }

    // Case 3: FriendOperation.Remove when AccountA and AccountB are not friends →
    // FriendUpdateResponse with NotFriendsWithPlayer localized failure.
    [Fact]
    public void FriendUpdate_Remove_NotFriends_NotFriendsWithPlayerError()
    {
        using var scope = new ClientNotifierScope();

        long accountIdA = UniqueId();
        long accountIdB = UniqueId();
        MakeAccount(accountIdA);
        MakeAccount(accountIdB);
        GroupManager.CreateGroup(accountIdA);
        GroupManager.CreateGroup(accountIdB);
        var (_, conn, registry) = MakeModule(accountIdA);

        registry.Dispatch(new FriendUpdateRequest
        {
            FriendHandle = $"TestFriend#{accountIdB}",
            FriendAccountId = accountIdB,
            FriendOperation = FriendOperation.Remove,
            RequestId = 3
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<FriendUpdateResponse>(conn.Sent[0]);
        AssertLocalization(response.LocalizedFailure, "NotFriendsWithPlayer", "FriendUpdateResponse");
    }

    // Case 4: FriendOperation.Block from AccountA targeting AccountB (not previously blocked) →
    // FriendUpdateResponse success + FriendStatusNotification (RefreshFriendList), in that order.
    [Fact]
    public void FriendUpdate_Block_NewBlock_SuccessAndFriendStatusNotification()
    {
        using var scope = new ClientNotifierScope();

        long accountIdA = UniqueId();
        long accountIdB = UniqueId();
        MakeAccount(accountIdA);
        MakeAccount(accountIdB);
        GroupManager.CreateGroup(accountIdA);
        GroupManager.CreateGroup(accountIdB);
        var (_, conn, registry) = MakeModule(accountIdA);

        registry.Dispatch(new FriendUpdateRequest
        {
            FriendHandle = $"TestFriend#{accountIdB}",
            FriendAccountId = accountIdB,
            FriendOperation = FriendOperation.Block,
            RequestId = 4
        });

        Assert.Equal(2, conn.Sent.Count);
        var response = Assert.IsType<FriendUpdateResponse>(conn.Sent[0]);
        Assert.Null(response.LocalizedFailure);
        Assert.IsType<FriendStatusNotification>(conn.Sent[1]);
    }

    // Case 5: Self-operation — FriendOperation.Add targeting own AccountId →
    // FriendUpdateResponse with CannotFriendYourself failure.
    [Fact]
    public void FriendUpdate_AddSelf_CannotFriendYourselfError()
    {
        using var scope = new ClientNotifierScope();

        long accountId = UniqueId();
        MakeAccount(accountId);
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new FriendUpdateRequest
        {
            FriendHandle = $"TestFriend#{accountId}",
            FriendAccountId = accountId,
            FriendOperation = FriendOperation.Add,
            RequestId = 5
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<FriendUpdateResponse>(conn.Sent[0]);
        AssertLocalization(response.LocalizedFailure, "CannotFriendYourself", "FriendUpdateResponse");
    }
}
