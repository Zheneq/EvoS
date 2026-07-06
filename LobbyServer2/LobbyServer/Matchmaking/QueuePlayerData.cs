namespace CentralServer.LobbyServer.Matchmaking;

public record QueuePlayerData(
    long AccountId,
    string EloKey,
    int NumControlledCharacters);