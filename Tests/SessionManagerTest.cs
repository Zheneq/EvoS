using System.Net;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="SessionManager"/>: <see cref="SessionManager.HasActiveSession"/>, which a fresh login polls
/// while waiting out the session of a crashed client (on its own instance, leaving the global one empty), and
/// starting sessions, which goes through the global instance because <see cref="SessionManager.OnPlayerConnect"/>
/// is static.
/// Joined to [Collection("ClientNotifierSeam")] because starting a session creates a group in the
/// process-global <see cref="GroupManager"/>.
/// </summary>
[Collection("ClientNotifierSeam")]
public class SessionManagerTest(ITestOutputHelper output) : EvosTest(output)
{
    private readonly SessionManager _sessions = new();
    private readonly TestLobby _lobby = new();

    private static long MakeAccount(long accountId)
    {
        DB.Get().AccountDao.CreateAccount(TestAccountHelper.MakeAccount(accountId, $"session{accountId}", 1500, 0, "elo"));
        return accountId;
    }

    // Logs in the way the directory server (CreateSession) and LoginModule (OnPlayerConnect) do
    private RecordingClientConnection Login(long accountId)
    {
        LobbySessionInfo session = _lobby.Sessions.CreateSession(accountId, null, IPAddress.Loopback, hasLiveGame: false);
        RecordingClientConnection conn = _lobby.Connect(accountId);
        SessionManager.OnPlayerConnect(conn, new RegisterGameClientRequest
        {
            SessionInfo = new LobbySessionInfo { AccountId = accountId, SessionToken = session.SessionToken }
        });
        return conn;
    }

    [Fact]
    public void HasActiveSession_NoSession_False()
    {
        Assert.False(_sessions.HasActiveSessionCore(9_100_001));
    }

    [Fact]
    public void HasActiveSession_LoginInProgress_True()
    {
        long accountId = MakeAccount(9_100_002);
        ((ISessionRegistry)_sessions).CreateSession(accountId, null, IPAddress.Loopback, hasLiveGame: false);

        Assert.True(_sessions.HasActiveSessionCore(accountId));
    }

    [Fact]
    public void Login_StartsSessionInSoloGroup()
    {
        long accountId = MakeAccount(9_100_003);

        RecordingClientConnection conn = Login(accountId);

        Assert.Same(conn, _lobby.Sessions.GetClientConnection(accountId));
        GroupInfo group = _lobby.Groups.GetPlayerGroup(accountId);
        Assert.NotNull(group);
        Assert.True(group.IsSolo());
        Assert.True(group.IsLeader(accountId));
        Assert.Equal(1, group.SubTypeMask);
    }

    [Fact]
    public void Login_LogsNoErrors()
    {
        long accountId = MakeAccount(9_100_009);
        MemoryAppender appender = new();
        Logger root = ((Hierarchy)LogManager.GetRepository()).Root;
        root.AddAppender(appender);
        try
        {
            Login(accountId);
        }
        finally
        {
            root.RemoveAppender(appender);
        }

        // Group creation used to look for the leader's connection before the session was registered
        Assert.DoesNotContain(
            appender.GetEvents(),
            e => e.Level >= Level.Error && e.RenderedMessage.Contains(accountId.ToString()));
    }

    [Fact]
    public void Login_RaisesSessionStartingBeforeSessionIsRegistered()
    {
        long accountId = MakeAccount(9_100_004);
        bool raised = false;
        IClientConnection? connectionWhenRaised = null;

        void OnSessionStarting(long startingAccountId)
        {
            if (startingAccountId != accountId) return; // other tests may log in concurrently
            raised = true;
            connectionWhenRaised = _lobby.Sessions.GetClientConnection(startingAccountId);
        }

        _lobby.Sessions.OnSessionStarting += OnSessionStarting;
        try
        {
            Login(accountId);
        }
        finally
        {
            _lobby.Sessions.OnSessionStarting -= OnSessionStarting;
        }

        Assert.True(raised);
        Assert.Null(connectionWhenRaised);
    }

    [Fact]
    public void Relogin_LeavesPreviousGroupForSoloGroup()
    {
        long leader = MakeAccount(9_100_005);
        long member = MakeAccount(9_100_006);
        Login(leader);
        Login(member);
        long groupId = _lobby.Groups.GetGroupID(leader);
        _lobby.Groups.JoinGroup(groupId, member);
        Assert.Equal(new[] { leader, member }, _lobby.Groups.GetGroupMembers(groupId));

        Login(member);

        Assert.Equal(new[] { leader }, _lobby.Groups.GetGroupMembers(groupId));
        GroupInfo memberGroup = _lobby.Groups.GetPlayerGroup(member);
        Assert.NotEqual(groupId, memberGroup.GroupId);
        Assert.True(memberGroup.IsSolo());
    }

    [Fact]
    public void CreateSession_WithLiveGame_KeepsSessionToken()
    {
        long accountId = MakeAccount(9_100_007);
        Login(accountId);
        long sessionToken = _lobby.Sessions.GetSessionInfo(accountId)!.SessionToken;

        LobbySessionInfo next = _lobby.Sessions.CreateSession(accountId, null, IPAddress.Loopback, hasLiveGame: true);

        Assert.Equal(sessionToken, next.SessionToken);
    }

    [Fact]
    public void CreateSession_WithoutLiveGame_IssuesNewSessionToken()
    {
        long accountId = MakeAccount(9_100_008);
        Login(accountId);
        long sessionToken = _lobby.Sessions.GetSessionInfo(accountId)!.SessionToken;

        LobbySessionInfo next = _lobby.Sessions.CreateSession(accountId, null, IPAddress.Loopback, hasLiveGame: false);

        Assert.NotEqual(sessionToken, next.SessionToken);
    }
}
