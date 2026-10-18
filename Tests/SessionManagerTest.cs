using System.Net;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

/// <summary>
/// Tests for <see cref="SessionManager.HasActiveSession"/>, which a fresh login polls while waiting out the session
/// of a crashed client. Uses its own <see cref="SessionManager"/> instance, leaving the global one empty.
/// </summary>
public class SessionManagerTest(ITestOutputHelper output) : EvosTest(output)
{
    private readonly SessionManager _sessions = new();

    private static long MakeAccount(long accountId)
    {
        DB.Get().AccountDao.CreateAccount(TestAccountHelper.MakeAccount(accountId, $"session{accountId}", 1500, 0, "elo"));
        return accountId;
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
        ((ISessionRegistry)_sessions).CreateSession(accountId, null, IPAddress.Loopback);

        Assert.True(_sessions.HasActiveSessionCore(accountId));
    }
}
