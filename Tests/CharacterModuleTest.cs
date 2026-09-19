using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
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
/// Tests for <see cref="CharacterModule"/> handlers.
/// Joined to [Collection("ClientNotifierSeam")] because GroupManager state is
/// process-global; serializing avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class CharacterModuleTest : EvosTest
{
    public CharacterModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds (> 9_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 9_000_000L;

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

    private static (CharacterModule module, MatchmakingModule matchmaking, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModuleWithRegistry(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var matchmaking = new MatchmakingModule(conn);
        var gameLifecycle = new GameLifecycleModule(conn);
        var module = new CharacterModule(conn, matchmaking, gameLifecycle);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, matchmaking, conn, registry);
    }

    private static PersistedAccountData MakeAccount(long accountId, CharacterType character = CharacterType.Archer)
    {
        var account = new PersistedAccountData
        {
            AccountId = accountId,
            Handle = $"Test#{accountId}",
            AccountComponent = new AccountComponent
            {
                LastCharacter = character,
            },
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>
            {
                { character, new PersistedCharacterData(character) }
            },
        };
        DB.Get().AccountDao.CreateAccount(account);
        return account;
    }

    // Case 1: UpdateRemoteCharacterRequest with empty RemoteSlotIndexes/Characters:
    // one failed UpdateRemoteCharacterResponse, nothing else sent.
    [Fact]
    public void UpdateRemoteCharacter_EmptySlotsAndCharacters_FailedResponseOnly()
    {
        long accountId = UniqueId();
        var (_, _, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new UpdateRemoteCharacterRequest
        {
            RequestId = 1,
            RemoteSlotIndexes = Array.Empty<int>(),
            Characters = Array.Empty<CharacterType>()
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<UpdateRemoteCharacterResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 2: UpdateRemoteCharacterRequest setting a valid character into slot 0 (account
    // whose LastRemoteCharacters is empty): LastRemoteCharacters[0] updated,
    // PlayerAccountDataUpdateNotification + success response sent.
    [Fact]
    public void UpdateRemoteCharacter_ValidCharacterIntoSlot0_UpdatesSlotAndSendsTwoMessages()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        var (_, _, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new UpdateRemoteCharacterRequest
        {
            RequestId = 2,
            RemoteSlotIndexes = new[] { 0 },
            Characters = new[] { CharacterType.BattleMonk }
        });

        Assert.Equal(2, conn.Sent.Count);
        Assert.IsType<PlayerAccountDataUpdateNotification>(conn.Sent[0]);
        var response = Assert.IsType<UpdateRemoteCharacterResponse>(conn.Sent[1]);
        Assert.True(response.Success);

        // Verify the slot was actually updated
        PersistedAccountData updated = DB.Get().AccountDao.GetAccount(accountId);
        Assert.Equal(CharacterType.BattleMonk, updated.AccountComponent.LastRemoteCharacters[0]);
    }

    // Case 3: UpdateRemoteCharacterRequest with a forbidden character (PendingWillFill):
    // ChatNotification warning sent, slot unchanged, failed response.
    [Fact]
    public void UpdateRemoteCharacter_ForbiddenCharacter_ChatWarningAndFailedResponse()
    {
        long accountId = UniqueId();
        MakeAccount(accountId);
        var (_, _, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new UpdateRemoteCharacterRequest
        {
            RequestId = 3,
            RemoteSlotIndexes = new[] { 0 },
            Characters = new[] { CharacterType.PendingWillFill }
        });

        // ChatNotification for the warning + failed UpdateRemoteCharacterResponse
        Assert.Equal(2, conn.Sent.Count);
        Assert.IsType<ChatNotification>(conn.Sent[0]);
        var response = Assert.IsType<UpdateRemoteCharacterResponse>(conn.Sent[1]);
        Assert.False(response.Success);

        // Slot must be unchanged (list still empty or not set to PendingWillFill)
        PersistedAccountData updated = DB.Get().AccountDao.GetAccount(accountId);
        Assert.DoesNotContain(CharacterType.PendingWillFill, updated.AccountComponent.LastRemoteCharacters);
    }

    // Case 4: HandlePlayerInfoUpdateRequest selecting a character (update with CharacterType
    // only, no game): AccountComponent.LastCharacter persisted,
    // PlayerAccountDataUpdateNotification + PlayerInfoUpdateResponse sent, one group
    // refresh recorded on the fake connection.
    [Fact]
    public void PlayerInfoUpdateRequest_SelectCharacter_PersistsAndBroadcasts()
    {
        long accountId = UniqueId();
        MakeAccount(accountId, CharacterType.Archer);
        GroupManager.CreateGroup(accountId);
        var (_, _, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new PlayerInfoUpdateRequest
        {
            RequestId = 4,
            PlayerInfoUpdate = new LobbyPlayerInfoUpdate
            {
                CharacterType = CharacterType.Archer,
            }
        });

        // PlayerAccountDataUpdateNotification + PlayerInfoUpdateResponse
        Assert.Equal(2, conn.Sent.Count);
        Assert.IsType<PlayerAccountDataUpdateNotification>(conn.Sent[0]);
        var infoResponse = Assert.IsType<PlayerInfoUpdateResponse>(conn.Sent[1]);
        Assert.Equal(4, infoResponse.ResponseId);

        // One BroadcastRefreshGroup call
        Assert.Single(conn.GroupRefreshes);

        // LastCharacter persisted
        PersistedAccountData updated = DB.Get().AccountDao.GetAccount(accountId);
        Assert.Equal(CharacterType.Archer, updated.AccountComponent.LastCharacter);
    }

    // Case 5: HandlePlayerInfoUpdateRequest with request.GameType set for a solo-group player:
    // _matchmaking.SelectedGameType updated.
    [Fact]
    public void PlayerInfoUpdateRequest_WithGameType_UpdatesMatchmakingGameType()
    {
        long accountId = UniqueId();
        MakeAccount(accountId, CharacterType.Archer);
        GroupManager.CreateGroup(accountId);
        var (_, matchmaking, _, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new PlayerInfoUpdateRequest
        {
            RequestId = 5,
            PlayerInfoUpdate = new LobbyPlayerInfoUpdate
            {
                CharacterType = CharacterType.Archer,
            },
            GameType = GameType.PvP
        });

        Assert.Equal(GameType.PvP, matchmaking.SelectedGameType);
    }

    // Case 6: HandlePlayerInfoUpdateRequest with ContextualReadyState = Ready
    // (no game, no penalties): _matchmaking.IsReady becomes true.
    [Fact]
    public void PlayerInfoUpdateRequest_WithReadyState_SetsMatchmakingIsReady()
    {
        long accountId = UniqueId();
        MakeAccount(accountId, CharacterType.Archer);
        GroupManager.CreateGroup(accountId);
        var (_, matchmaking, _, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new PlayerInfoUpdateRequest
        {
            RequestId = 6,
            PlayerInfoUpdate = new LobbyPlayerInfoUpdate
            {
                CharacterType = CharacterType.Archer,
                ContextualReadyState = new ContextualReadyState
                {
                    ReadyState = ReadyState.Ready,
                    GameProcessCode = string.Empty
                }
            }
        });

        Assert.True(matchmaking.IsReady);
    }
}
