using System;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.Utils;
using log4net;

namespace CentralServer.LobbyServer.Group;

public class GroupsTask : PeriodicRunner
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GroupsTask));
    
    public GroupsTask(CancellationToken token) : base(token, TimeSpan.FromSeconds(5))
    {
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        GroupManager.PingGroupRequests();
        return Task.CompletedTask;
    }
}