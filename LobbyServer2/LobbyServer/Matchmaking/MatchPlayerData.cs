using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Matchmaking;

public record MatchPlayerData(
    long AccountId,
    string Handle,
    string EloKey,
    EloValues EloValues, // TODO do we need to carry all of them if we are only looking for EloKey one?
    int SubTypeIndex,
    List<CharacterType> SelectedCharacters,
    HashSet<long> BlockedAccounts)
{
    public int NumControlledCharacters => SelectedCharacters.Count;
    public float GetElo()
    {
        EloValues.GetElo(EloKey, out float elo, out _);
        return elo;
    }
    public int GetEloConfidenceLevel()
    {
        EloValues.GetElo(EloKey, out _, out int cf);
        return Math.Max(cf, 0);
    }
}