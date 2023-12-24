using System;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.Utils;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakingTask : PeriodicRunner
{
    private static readonly object queueUpdateRunning = new object();
    
    public MatchmakingTask(CancellationToken token) : base(token, TimeSpan.FromSeconds(20))
    {
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        MatchmakingManager.Update();
        return Task.CompletedTask;
    }
}