using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Utils;
using log4net;

namespace CentralServer.LobbyServer.Tutorial;

public static class TutorialManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(TutorialManager));

    // The server is reserved before the CreateGameResponse is sent because the client ignores
    // an unassignment while waiting for a tutorial: failures must be reported in the response.
    public static TutorialGame StartTutorial(long accountId, out LocalizationPayload error)
    {
        BridgeServerProtocol server = ServerManager.GetServer(true);
        if (server is null)
        {
            log.Warn($"No available server for tutorial game of {LobbyServerUtils.GetHandle(accountId)}");
            // Shown on the loading screen while the client keeps retrying
            error = LocalizationPayload.Create(
                "WaitingForStatus",
                "Global",
                LocalizationArg_LocalizationPayload.Create(LocalizationPayload.Create("ServerResources", "Global")));
            return null;
        }

        TutorialGame game = new TutorialGame(accountId, server);
        if (!GameManager.RegisterGame(game.ProcessCode, game))
        {
            log.Error($"Failed to register tutorial game for {LobbyServerUtils.GetHandle(accountId)}");
            server.Shutdown();
            error = LocalizationPayload.Create("UnknownErrorTryAgain@Frontend");
            return null;
        }
        game.Assign();
        _ = game.StartTutorialAsync();
        error = null;
        return game;
    }
}