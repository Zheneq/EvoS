using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Session;
using CentralServer.Utils;
using log4net;

namespace CentralServer.LobbyServer.Friend;

public class FriendsTask : PeriodicRunner
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FriendsTask));
    
    public FriendsTask(CancellationToken token) : base(token, TimeSpan.FromSeconds(5))
    {
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        HashSet<long> pendingUpdate = FriendManager.GetAndResetPendingUpdate();
        HashSet<long> onlinePlayers = SessionManager.GetOnlinePlayers();
        HashSet<long> receivers = pendingUpdate
            .SelectMany(FriendManager.GetFriends)
            .Distinct()
            .Where(onlinePlayers.Contains)
            .ToHashSet();
        log.Debug($"Got {pendingUpdate.Count} updates total for {receivers.Count} players");
        foreach (long accId in receivers)
        {
            LobbyServerProtocol conn = SessionManager.GetClientConnection(accId);
            if (conn is not null)
            {
                await conn.RefreshFriendList();
            }
        }
    }
}