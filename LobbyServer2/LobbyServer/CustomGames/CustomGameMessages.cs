namespace CentralServer.LobbyServer.CustomGames;

public static class CustomGameMessages
{
    public static LocalizationPayload GameNoLongerAvailable { get; }
        = LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou", "Invite"); // The game is no longer available to be joined.
}
