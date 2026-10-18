using CentralServer.BridgeServer;

namespace CentralServer.LobbyServer.Tutorial;

public static class TutorialManager
{
    // The server is reserved before the CreateGameResponse is sent because the client ignores
    // an unassignment while waiting for a tutorial: failures must be reported in the response.
    public static TutorialGame StartTutorial(long accountId, out LocalizationPayload error)
    {
        return SoloGame.Start(
            accountId,
            server => new TutorialGame(accountId, server),
            // Shown on the loading screen while the client keeps retrying
            LocalizationPayload.Create(
                "WaitingForStatus",
                "Global",
                LocalizationArg_LocalizationPayload.Create(LocalizationPayload.Create("ServerResources", "Global"))),
            out error);
    }
}
