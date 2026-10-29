using CentralServer.LobbyServer.Utils;

namespace CentralServer.LobbyServer.CustomGames;

public static class CustomGameMessages
{
    public static LocalizationPayload GameNoLongerAvailable { get; }
        = LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou", "Invite"); // The game is no longer available to be joined.

    public static LocalizationPayload NotInAGame { get; }
        = LocalizationPayload.Create("NotInAGame", "Invite"); // You are not in a game.

    public static LocalizationPayload GameAlreadyLaunched { get; }
        = LocalizationPayload.Create("GameAlreadyLaunched", "Invite"); // Your game has already launched.

    // You've already sent an invite to that player recently, please wait a minute.
    public static LocalizationPayload AlreadyInvitedPlayerToGame { get; }
        = LocalizationPayload.Create("AleadyInvitedPlayerToGame", "Invite"); // sic

    public static LocalizationPayload GameInvite { get; }
        = LocalizationPayload.Create("GameInvite", "Global"); // Game Invite

    public static LocalizationPayload OtherPlayerInGame(long accountId)
    {
        return LocalizationPayload.Create(
            "OtherPlayerInGame",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId))); // {0} is already in a game.
    }

    public static LocalizationPayload OtherPlayerAlreadyQueueing(long accountId)
    {
        return LocalizationPayload.Create(
            "OtherPlayerAlreadyQueueing",
            "Invite",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId))); // {0} is already queuing for a game.
    }
}
