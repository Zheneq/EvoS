using System;
using System.Collections.Generic;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using Tests.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="InviteRules"/>, the checks shared by group and custom game invites.
/// </summary>
public class InviteRulesTest : EvosTest
{
    private const string Target = "a test";

    public InviteRulesTest(ITestOutputHelper output) : base(output)
    {
    }

    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 9_000_000L;

    private static void AssertLocalization(LocalizationPayload payload, string expectedTerm, string expectedContext)
    {
        Assert.NotNull(payload);
        Assert.Equal(expectedTerm, payload.Term);
        Assert.Equal(expectedContext, payload.Context);
    }

    // Registers a login so that ResolveAccountId can find the account by its handle
    private static string MakeLogin(long accountId)
    {
        string username = $"invitee{accountId}";
        DB.Get().LoginDao.Save(new LoginDao.LoginEntry
        {
            AccountId = accountId,
            Username = username,
            Salt = "",
            Hash = "",
        });
        return $"Invitee{accountId}#{accountId}";
    }

    private static PersistedAccountData MakeAccount(long accountId, params long[] blockedAccounts)
    {
        return new PersistedAccountData
        {
            AccountId = accountId,
            UserName = $"invitee{accountId}",
            Handle = $"Invitee{accountId}#{accountId}",
            SocialComponent = new SocialComponent
            {
                BlockedAccounts = new HashSet<long>(blockedAccounts)
            },
        };
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Player#123", "Player#123")]
    [InlineData("Player#123(Dev)", "Player#123")]
    [InlineData("\uE000Player#123", "Player#123")] // the mentor icon
    public void CleanHandle_RemovesTags(string handle, string expected)
    {
        Assert.Equal(expected, InviteRules.CleanHandle(handle));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NoSuchPlayer#9999")]
    public void ResolveInvitee_UnknownHandle_PlayerNotFound(string handle)
    {
        var inviter = new RecordingClientConnection { AccountId = UniqueId() };

        LocalizationPayload failure = InviteRules.ResolveInvitee(inviter, handle, Target, out long inviteeAccountId);

        AssertLocalization(failure, "PlayerNotFound", "Invite");
        Assert.Equal(0, inviteeAccountId);
    }

    [Fact]
    public void ResolveInvitee_OwnHandle_CantInviteYourself()
    {
        long accountId = UniqueId();
        string handle = MakeLogin(accountId);
        var inviter = new RecordingClientConnection { AccountId = accountId };

        LocalizationPayload failure = InviteRules.ResolveInvitee(inviter, handle, Target, out _);

        AssertLocalization(failure, "CantInviteYourself", "Invite");
    }

    [Fact]
    public void ResolveInvitee_OtherPlayer_Resolved()
    {
        long inviteeAccountId = UniqueId();
        string handle = MakeLogin(inviteeAccountId);
        var inviter = new RecordingClientConnection { AccountId = UniqueId() };

        LocalizationPayload failure = InviteRules.ResolveInvitee(inviter, handle, Target, out long resolved);

        Assert.Null(failure);
        Assert.Equal(inviteeAccountId, resolved);
    }

    [Fact]
    public void CheckInvitee_InviterBlocksInvitee_YouAreBlocking()
    {
        long inviteeAccountId = UniqueId();
        PersistedAccountData inviter = MakeAccount(UniqueId(), inviteeAccountId);
        PersistedAccountData invitee = MakeAccount(inviteeAccountId);

        LocalizationPayload failure = InviteRules.CheckInvitee(
            inviter, invitee, invitee.Handle, Target, out bool shadowBanned, out LobbyServerProtocol connection);

        AssertLocalization(failure, "YouAreBlocking", "Invite");
        Assert.False(shadowBanned);
        Assert.Null(connection);
    }

    [Fact]
    public void CheckInvitee_InviteeBlocksInviter_ShadowBanned()
    {
        long inviterAccountId = UniqueId();
        PersistedAccountData inviter = MakeAccount(inviterAccountId);
        PersistedAccountData invitee = MakeAccount(UniqueId(), inviterAccountId);

        LocalizationPayload failure = InviteRules.CheckInvitee(
            inviter, invitee, invitee.Handle, Target, out bool shadowBanned, out LobbyServerProtocol connection);

        Assert.Null(failure);
        Assert.True(shadowBanned);
        Assert.Null(connection);
    }

    [Fact]
    public void CheckInvitee_InviteeOffline_PlayerNotFound()
    {
        PersistedAccountData inviter = MakeAccount(UniqueId());
        PersistedAccountData invitee = MakeAccount(UniqueId());

        LocalizationPayload failure = InviteRules.CheckInvitee(
            inviter, invitee, invitee.Handle, Target, out bool shadowBanned, out LobbyServerProtocol connection);

        AssertLocalization(failure, "PlayerNotFound", "Invite");
        Assert.False(shadowBanned);
        Assert.Null(connection);
    }
}
