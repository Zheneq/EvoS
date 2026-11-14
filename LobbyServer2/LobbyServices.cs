using System;
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
        // Resolve everything before replacing any Instance. Constructing a manager runs its type's
        // static initializer, which builds the pre-initialized instance from the other managers'
        // current Instances; those must still be the pre-initialized ones, or that throwaway
        // instance would subscribe to the container's services' events.
        var sessions = (SessionManager)services.GetRequiredService<ISessionRegistry>();
        var groups = (GroupManager)services.GetRequiredService<IGroupRegistry>();
        var servers = (ServerManager)services.GetRequiredService<IServerPool>();
        var games = (GameManager)services.GetRequiredService<IGameRegistry>();
        var matchmaking = (MatchmakingManager)services.GetRequiredService<IMatchmakingManager>();

        SessionManager.Instance = sessions;
        GroupManager.Instance = groups;
        ServerManager.Instance = servers;
        GameManager.Instance = games;
        MatchmakingManager.Instance = matchmaking;
    }
}
