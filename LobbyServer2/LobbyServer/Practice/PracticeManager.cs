using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using log4net;

namespace CentralServer.LobbyServer.Practice;

public static class PracticeManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PracticeManager));

    public static PracticeGame StartPractice(long accountId, out LocalizationPayload error, out bool allowRetry)
    {
        if (!LobbyConfiguration.GetGameTypePracticeAvailable())
        {
            log.Info($"{LobbyServerUtils.GetHandle(accountId)} attempted to start a practice game while it is disabled");
            error = LocalizationPayload.Create("DisabledByAdmin@Matchmaking");
            allowRetry = false;
            return null;
        }

        // Shown in a popup, after which the client lets the player ready up again
        allowRetry = true;
        return SoloGame.Start(
            accountId,
            server => new PracticeGame(accountId, server),
            LocalizationPayload.Create("FailedStartGameServer@Frontend"),
            out error);
    }
}
