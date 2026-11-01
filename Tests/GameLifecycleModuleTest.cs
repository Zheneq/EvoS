using CentralServer.BridgeServer;
using CentralServer.LobbyServer.CustomGames;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="GameLifecycleModule"/> state behaviour and handlers.
/// Joined to [Collection("ClientNotifierSeam")] because GroupManager state is
/// process-global; serializing avoids cross-talk.
/// </summary>
[Collection("ClientNotifierSeam")]
public class GameLifecycleModuleTest : EvosTest
{
    public GameLifecycleModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    // Use unique per-test AccountIds (> 7_000_000 to avoid collision with other test ranges)
    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 7_000_000L;

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

    private static (GameLifecycleModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModuleWithRegistry(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn, GameManager.Instance);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static (GameLifecycleModule module, RecordingClientConnection conn)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new GameLifecycleModule(conn, GameManager.Instance);
        return (module, conn);
    }

    // LocalizationPayload does not override Equals, so we compare fields directly.
    private static void AssertLocalization(LocalizationPayload payload, string expectedTerm, string expectedContext)
    {
        Assert.NotNull(payload);
        Assert.Equal(expectedTerm, payload.Term);
        Assert.Equal(expectedContext, payload.Context);
    }

    // Case 1: Fresh module: CurrentGame null, IsInGame() false, PlayerInfo null.
    [Fact]
    public void FreshModule_CurrentGameNull_IsInGameFalse_PlayerInfoNull()
    {
        long accountId = UniqueId();
        var (module, _) = MakeModule(accountId);

        Assert.Null(module.CurrentGame);
        Assert.False(module.IsInGame());
        Assert.Null(module.PlayerInfo);
    }

    // Case 2: LeaveGame(null) returns false (null-server log path), no sends.
    [Fact]
    public void LeaveGame_NullGame_ReturnsFalse_NoSends()
    {
        long accountId = UniqueId();
        var (module, conn) = MakeModule(accountId);

        bool result = module.LeaveGame(null);

        Assert.False(result);
        Assert.Empty(conn.Sent);
    }

    // Case 3: LeaveGame(someGame) when not in a game — can't construct a Game easily
    // (abstract, heavy deps); dropping this case per plan's drop-and-note clause.

    // Case 4: BalancedTeamRequest with no game: one BalancedTeamResponse, Success = false.
    [Fact]
    public void BalancedTeamRequest_NoGame_SendsFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new BalancedTeamRequest
        {
            RequestId = 4,
            Slots = new List<BalanceTeamSlot>()
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<BalancedTeamResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 5: PreviousGameInfoRequest with no game: one response, PreviousGameInfo == null.
    [Fact]
    public void PreviousGameInfoRequest_NoGame_NullPreviousGameInfo()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new PreviousGameInfoRequest { RequestId = 5 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<PreviousGameInfoResponse>(conn.Sent[0]);
        Assert.Null(response.PreviousGameInfo);
    }

    // Case 6: LeaveGameRequest with no current game: LeaveGameResponse (Success = true) +
    // GameStatusNotification (Stopped) + one SendGameUnassignmentNotification call — in that order.
    [Fact]
    public void LeaveGameRequest_NoCurrentGame_SendsThreeResponsesInOrder()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new LeaveGameRequest { RequestId = 6 });

        Assert.Equal(2, conn.Sent.Count);
        var leaveResponse = Assert.IsType<LeaveGameResponse>(conn.Sent[0]);
        Assert.True(leaveResponse.Success);
        var statusNotification = Assert.IsType<GameStatusNotification>(conn.Sent[1]);
        Assert.Equal(GameStatus.Stopped, statusNotification.GameStatus);
        Assert.Equal(1, conn.GameUnassignmentCalls);
    }

    // Case 7: JoinGameRequest for an unknown process code: failed JoinGameResponse with the
    // failure payload; ResetReadyStateCalls == 1.
    [Fact]
    public void JoinGameRequest_UnknownProcessCode_FailedResponseAndResetReadyState()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new JoinGameRequest
        {
            RequestId = 7,
            GameServerProcessCode = "no-such-game-xyz",
            AsSpectator = false
        });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<JoinGameResponse>(conn.Sent[0]);
        Assert.False(response.Success);
        Assert.NotNull(response.LocalizedFailure);
        Assert.Equal(1, conn.ResetReadyStateCalls);
    }

    // Case 8a: GameInvitationRequest while not in a custom game: one failed response, NotInAGame.
    [Fact]
    public void GameInvitationRequest_NotInCustomGame_NotInAGame()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        GameInvitationResponse response = InviteToGame(conn, registry, "someone");

        Assert.False(response.Success);
        Assert.Equal("someone", response.InviteeHandle);
        AssertLocalization(response.LocalizedFailure, "NotInAGame", "Invite");
    }

    // Case 8b: RankedLeaderboardOverviewRequest sends exactly one failed response.
    [Fact]
    public void RankedLeaderboardOverviewRequest_SendsOneFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new RankedLeaderboardOverviewRequest { RequestId = 9 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<RankedLeaderboardOverviewResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 8c: CalculateFreelancerStatsRequest sends exactly one failed response.
    [Fact]
    public void CalculateFreelancerStatsRequest_SendsOneFailedResponse()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new CalculateFreelancerStatsRequest { RequestId = 10 });

        Assert.Single(conn.Sent);
        var response = Assert.IsType<CalculateFreelancerStatsResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // Case 8d: GameInviteConfirmationResponse to an unknown invite: "no longer available" if accepted.
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void GameInviteConfirmationResponse_UnknownInvite(bool accepted, int expectedSystemMessages)
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        registry.Dispatch(new GameInviteConfirmationResponse
        {
            ResponseId = -9999,
            Accepted = accepted
        });

        Assert.Empty(conn.Sent);
        Assert.Equal(expectedSystemMessages, conn.SystemMessages.Count);
        if (accepted)
        {
            AssertLocalization(conn.SystemMessages[0], "GameCreatorNoLongerHasAGameForYou", "Invite");
        }
    }

    // Case 8e: PlayerPanelUpdatedNotification no-throw.
    [Fact]
    public void PlayerPanelUpdatedNotification_NoThrow()
    {
        long accountId = UniqueId();
        var (_, _, registry) = MakeModuleWithRegistry(accountId);

        var ex = Record.Exception(() => registry.Dispatch(new PlayerPanelUpdatedNotification()));

        Assert.Null(ex);
    }

    // Practice reads the selected character from the account
    private static long MakeAccount(params long[] blockedAccounts)
    {
        long accountId = UniqueId();
        DB.Get().AccountDao.CreateAccount(new PersistedAccountData
        {
            AccountId = accountId,
            Handle = $"Test#{accountId}",
            AccountComponent = new AccountComponent
            {
                LastCharacter = CharacterType.Archer,
            },
            CharacterData = new Dictionary<CharacterType, PersistedCharacterData>
            {
                { CharacterType.Archer, new PersistedCharacterData(CharacterType.Archer) }
            },
            SocialComponent = new SocialComponent
            {
                BlockedAccounts = new HashSet<long>(blockedAccounts)
            },
        });
        return accountId;
    }

    // Registers a login so that the account can be found by the returned handle
    private static string MakeLogin(long accountId)
    {
        DB.Get().LoginDao.Save(new LoginDao.LoginEntry
        {
            AccountId = accountId,
            Username = $"player{accountId}",
            Salt = "",
            Hash = "",
        });
        return $"Player{accountId}#{accountId}";
    }

    // Creates a custom game owned by the account. The owner takes a player slot.
    private static Game CreateCustomGame(long owner, int teamPlayers = 4, int spectators = 0)
    {
        GroupManager.CreateGroup(owner);
        Game game = CustomGameManager.CreateGame(
            owner,
            new LobbyGameConfig
            {
                TeamAPlayers = teamPlayers,
                TeamBPlayers = teamPlayers,
                Spectators = spectators,
                SubTypes = new List<GameSubType> { new GameSubType() }
            },
            out LocalizationPayload error);
        Assert.True(game is not null, error?.ToString());
        return game;
    }

    // Creates a custom game owned by the account and puts the module in it
    private static Game JoinNewCustomGame(GameLifecycleModule module, long owner)
    {
        Game game = CreateCustomGame(owner);
        module.JoinGame(game);
        return game;
    }

    // Responds the way the client does
    private static void RespondToInvite(CapturingRegistry registry, CustomGameInvite invite, bool accepted)
    {
        registry.Dispatch(new GameInviteConfirmationResponse
        {
            Accepted = accepted,
            GameCreatorAccountId = invite.InviterAccountId,
            InitialRequestId = invite.InitialRequestId,
            ResponseId = invite.InviteId
        });
    }

    private static Team TeamOf(Game game, long accountId)
    {
        return game.TeamInfo.TeamPlayerInfo.Single(p => p.AccountId == accountId).TeamId;
    }

    private static GameInvitationResponse InviteToGame(
        RecordingClientConnection conn,
        CapturingRegistry registry,
        string inviteeHandle)
    {
        registry.Dispatch(new GameInvitationRequest
        {
            RequestId = 12,
            InviteeHandle = inviteeHandle
        });

        return Assert.IsType<GameInvitationResponse>(Assert.Single(conn.Sent));
    }

    private static CreateGameResponse CreateGame(RecordingClientConnection conn, CapturingRegistry registry, GameType gameType)
    {
        registry.Dispatch(new CreateGameRequest
        {
            RequestId = 11,
            GameConfig = new LobbyGameConfig { GameType = gameType }
        });

        return Assert.IsType<CreateGameResponse>(Assert.Single(conn.Sent));
    }

    // Case 9: solo games refuse players in a group.
    [Theory]
    [InlineData(GameType.Tutorial)]
    [InlineData(GameType.Practice)]
    public void CreateGameRequest_SoloGameInGroup_FailedResponse(GameType gameType)
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        GroupManager.CreateGroup(accountId);
        GroupManager.JoinGroup(GroupManager.GetGroupID(accountId), UniqueId());
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        CreateGameResponse response = CreateGame(conn, registry, gameType);

        Assert.False(response.Success);
        Assert.Equal("BadGroupSizeForQueue", response.LocalizedFailure.Term);
        Assert.Equal("Matchmaking", response.LocalizedFailure.Context);
        LocalizationPayload.Serializer.Deserialize(
            new MemoryStream(Assert.Single(response.LocalizedFailure.ArgumentsAsBinaryData)),
            out object groupSize);
        Assert.Equal(2, Assert.IsType<LocalizationArg_Int32>(groupSize).m_value);
        Assert.True(response.AllowRetry);
    }

    // Case 10: a solo group passes the group check. No game servers are registered in tests,
    // so the request gets as far as picking a server.
    [Theory]
    [InlineData(GameType.Tutorial, "WaitingForStatus")]
    [InlineData(GameType.Practice, "FailedStartGameServer")]
    public void CreateGameRequest_SoloGameSoloGroup_PassesGroupCheck(GameType gameType, string expectedTerm)
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        GroupManager.CreateGroup(accountId);
        var (_, conn, registry) = MakeModuleWithRegistry(accountId);

        CreateGameResponse response = CreateGame(conn, registry, gameType);

        Assert.False(response.Success);
        Assert.Equal(expectedTerm, response.LocalizedFailure.Term);
    }

    // Case 11: GameInvitationRequest from a custom game checks the invitee.
    [Fact]
    public void GameInvitationRequest_UnknownHandle_PlayerNotFound()
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        JoinNewCustomGame(module, accountId);
        try
        {
            GameInvitationResponse response = InviteToGame(conn, registry, "NoSuchPlayer#9999");

            Assert.False(response.Success);
            AssertLocalization(response.LocalizedFailure, "PlayerNotFound", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    [Fact]
    public void GameInvitationRequest_OwnHandle_CantInviteYourself()
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        string handle = MakeLogin(accountId);
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        JoinNewCustomGame(module, accountId);
        try
        {
            GameInvitationResponse response = InviteToGame(conn, registry, handle);

            Assert.False(response.Success);
            AssertLocalization(response.LocalizedFailure, "CantInviteYourself", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    [Fact]
    public void GameInvitationRequest_OfflineInvitee_PlayerNotFound()
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        string inviteeHandle = MakeLogin(MakeAccount());
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        JoinNewCustomGame(module, accountId);
        try
        {
            GameInvitationResponse response = InviteToGame(conn, registry, inviteeHandle + "(Dev)");

            Assert.False(response.Success);
            Assert.Equal(inviteeHandle, response.InviteeHandle);
            AssertLocalization(response.LocalizedFailure, "PlayerNotFound", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    [Fact]
    public void GameInvitationRequest_InviterBlocksInvitee_YouAreBlocking()
    {
        using var scope = new ClientNotifierScope();
        long inviteeAccountId = MakeAccount();
        long accountId = MakeAccount(inviteeAccountId);
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        JoinNewCustomGame(module, accountId);
        try
        {
            GameInvitationResponse response = InviteToGame(conn, registry, MakeLogin(inviteeAccountId));

            Assert.False(response.Success);
            AssertLocalization(response.LocalizedFailure, "YouAreBlocking", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    // A blocked inviter is told that the invite was sent
    [Fact]
    public void GameInvitationRequest_InviteeBlocksInviter_ReportsSuccessWithoutInvite()
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        long inviteeAccountId = MakeAccount(accountId);
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = JoinNewCustomGame(module, accountId);
        try
        {
            GameInvitationResponse response = InviteToGame(conn, registry, MakeLogin(inviteeAccountId));

            Assert.True(response.Success);
            Assert.Null(response.LocalizedFailure);
            // no pending invite, otherwise this would fail
            Assert.NotNull(CustomGameManager.Invites.Create(accountId, inviteeAccountId, game.ProcessCode, 0));
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    // Case 12: joining a game drops the player's pending invites
    [Fact]
    public void JoinGame_DropsPendingInvites()
    {
        using var scope = new ClientNotifierScope();
        long accountId = MakeAccount();
        var (module, _) = MakeModule(accountId);
        CustomGameInvite invite = CustomGameManager.Invites.Create(UniqueId(), accountId, "CustomGame-other", 1);
        Assert.NotNull(invite);

        JoinNewCustomGame(module, accountId);
        try
        {
            Assert.Null(CustomGameManager.Invites.Take(invite.InviteId, accountId));
        }
        finally
        {
            CustomGameManager.DeleteGame(accountId);
        }
    }

    // Case 13: GameInviteConfirmationResponse to a pending invite
    [Fact]
    public void GameInviteConfirmationResponse_Accepted_JoinsGame()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner);
        try
        {
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            RespondToInvite(registry, invite, true);

            Assert.Same(game, module.CurrentGame);
            Assert.Equal(Team.TeamA, TeamOf(game, accountId));
            Assert.Equal(1, conn.ResetReadyStateCalls);
            Assert.Empty(conn.SystemMessages);
            Assert.Empty(scope.Notifier.SystemMessages);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void GameInviteConfirmationResponse_AcceptedWithTeamsFull_JoinsAsSpectator()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, _, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner, teamPlayers: 1, spectators: 1);
        try
        {
            Assert.NotNull(CustomGameManager.JoinGame(MakeAccount(), game.ProcessCode, false, out _));
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            RespondToInvite(registry, invite, true);

            Assert.Same(game, module.CurrentGame);
            Assert.Equal(Team.Spectator, TeamOf(game, accountId));
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void GameInviteConfirmationResponse_AcceptedWithGameFull_TellsBoth()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner, teamPlayers: 1);
        try
        {
            Assert.NotNull(CustomGameManager.JoinGame(MakeAccount(), game.ProcessCode, false, out _));
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            RespondToInvite(registry, invite, true);

            Assert.Null(module.CurrentGame);
            AssertLocalization(Assert.Single(conn.SystemMessages), "GameCreatorNoLongerHasAGameForYou", "Invite");
            var (inviter, message) = Assert.Single(scope.Notifier.SystemMessages);
            Assert.Equal(owner, inviter);
            AssertLocalization(message, "FailedToJoinGame", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void GameInviteConfirmationResponse_Rejected_TellsInviter()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner);
        try
        {
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            RespondToInvite(registry, invite, false);

            Assert.Null(module.CurrentGame);
            Assert.Empty(conn.SystemMessages);
            var (inviter, message) = Assert.Single(scope.Notifier.SystemMessages);
            Assert.Equal(owner, inviter);
            AssertLocalization(message, "OtherPlayerRejectedInvite", "Invite");
            Assert.Null(CustomGameManager.Invites.Take(invite.InviteId, accountId));
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void GameInviteConfirmationResponse_WrongGameCreator_DoesNotJoin()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner);
        try
        {
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            registry.Dispatch(new GameInviteConfirmationResponse
            {
                Accepted = true,
                GameCreatorAccountId = owner + 1,
                InitialRequestId = invite.InitialRequestId,
                ResponseId = invite.InviteId
            });

            Assert.Null(module.CurrentGame);
            AssertLocalization(Assert.Single(conn.SystemMessages), "GameCreatorNoLongerHasAGameForYou", "Invite");
            Assert.Empty(scope.Notifier.SystemMessages);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    [Fact]
    public void GameInviteConfirmationResponse_AcceptedInQueue_DoesNotJoin()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        conn.InQueue = true;
        Game game = CreateCustomGame(owner);
        try
        {
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);

            RespondToInvite(registry, invite, true);

            Assert.Null(module.CurrentGame);
            Assert.DoesNotContain(game.TeamInfo.TeamPlayerInfo, p => p.AccountId == accountId);
            AssertLocalization(Assert.Single(conn.SystemMessages), "OtherPlayerAlreadyQueueing", "Invite");
            var (inviter, message) = Assert.Single(scope.Notifier.SystemMessages);
            Assert.Equal(owner, inviter);
            AssertLocalization(message, "FailedToJoinGame", "Invite");
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
        }
    }

    // A stale popup clicked after joining a game
    [Fact]
    public void GameInviteConfirmationResponse_AcceptedAfterJoiningGame_NoMessages()
    {
        using var scope = new ClientNotifierScope();
        long owner = MakeAccount();
        long accountId = MakeAccount();
        var (module, conn, registry) = MakeModuleWithRegistry(accountId);
        Game game = CreateCustomGame(owner);
        try
        {
            CustomGameInvite invite = CustomGameManager.Invites.Create(owner, accountId, game.ProcessCode, 5);
            JoinNewCustomGame(module, accountId);

            RespondToInvite(registry, invite, true);

            Assert.NotSame(game, module.CurrentGame);
            Assert.Empty(conn.SystemMessages);
            Assert.Empty(scope.Notifier.SystemMessages);
        }
        finally
        {
            CustomGameManager.DeleteGame(owner);
            CustomGameManager.DeleteGame(accountId);
        }
    }
}
