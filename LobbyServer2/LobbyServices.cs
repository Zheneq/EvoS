using System;
using System.Runtime.CompilerServices;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using Microsoft.Extensions.DependencyInjection;

namespace CentralServer;

public static class LobbyServices
{
    public static IServiceCollection AddLobbyServices(this IServiceCollection services)
    {
        services.AddSingleton<ISessionRegistry, SessionManager>();
        services.AddSingleton<IGroupRegistry, GroupManager>();
        services.AddSingleton<IServerPool, ServerManager>();
        services.AddSingleton<IGameRegistry, GameManager>();
        services.AddSingleton<IMatchmakingManager, MatchmakingManager>();
        services.AddTransient<LobbyServerProtocol>();
        return services;
    }

    // Points the transitional static Instances at the container's singletons.
    // TODO remove these statics, rely on DI
    public static void BindStaticInstances(IServiceProvider services)
    {
        // Run every manager's static initializer before replacing any Instance. Each initializer builds
        // the pre-initialized instance from the other managers' current Instances (GroupManager subscribes
        // to SessionManager, GameManager to ServerManager); those must still be the pre-initialized ones,
        // or a throwaway instance would subscribe to the container's services. Without this, the runtime
        // may run an initializer as late as the assignment below.
        foreach (Type manager in new[]
                 {
                     typeof(SessionManager), typeof(GroupManager), typeof(ServerManager),
                     typeof(GameManager), typeof(MatchmakingManager)
                 })
        {
            RuntimeHelpers.RunClassConstructor(manager.TypeHandle);
        }

        SessionManager.Instance = (SessionManager)services.GetRequiredService<ISessionRegistry>();
        GroupManager.Instance = (GroupManager)services.GetRequiredService<IGroupRegistry>();
        ServerManager.Instance = (ServerManager)services.GetRequiredService<IServerPool>();
        GameManager.Instance = (GameManager)services.GetRequiredService<IGameRegistry>();
        MatchmakingManager.Instance = (MatchmakingManager)services.GetRequiredService<IMatchmakingManager>();
    }
}
