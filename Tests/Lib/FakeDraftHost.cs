using CentralServer.BridgeServer;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;

namespace Tests.Lib;

/// <summary>
/// Hand-rolled fake for <see cref="IDraftHost"/>: a roster to fill in, settable game state,
/// and a record of what the draft sent and whether it cancelled the match.
/// </summary>
public class FakeDraftHost : IDraftHost
{
    public List<LobbyServerPlayerInfo> Players { get; } = [];
    public bool IsControlAllBots { get; set; }
    public bool HasBans { get; set; } = true;
    public DraftTimeouts Timeouts { get; set; } = new(
        Ban1: TimeSpan.FromSeconds(60),
        Ban2: TimeSpan.FromSeconds(30),
        Pick: TimeSpan.FromSeconds(30),
        Trade: TimeSpan.FromSeconds(15));

    // What CheckParticipantsConnected finds. Like the game, it cancels the match when someone has disconnected.
    public bool Connected { get; set; } = true;

    // Runs where the draft checks connectivity: before each sub-phase, outside the draft's lock
    public Action? BeforeConnectivityCheck { get; set; }

    private readonly CancellationTokenSource _cancellation = new();
    public CancellationToken CancellationToken => _cancellation.Token;
    public bool IsCancelled => _cancellation.IsCancellationRequested;
    public readonly List<(string? DodgerHandle, long? DodgerAccountId)> Cancellations = [];
    public readonly List<(FreelancerResolutionPhaseSubType Phase, RankedResolutionPhaseData Data)> Snapshots = [];
    public int GameInfoNotifications;

    public IReadOnlyList<LobbyServerPlayerInfo> Roster => Players;

    public LobbyServerPlayerInfo? GetPlayerById(int playerId) => Players.Find(p => p.PlayerId == playerId);

    public LobbyServerPlayerInfo? GetMainSlot(long accountId) =>
        Players.Find(p => p.AccountId == accountId && !p.IsRemoteControlled);

    public bool CheckParticipantsConnected()
    {
        BeforeConnectivityCheck?.Invoke();
        if (!Connected)
        {
            CancelMatch();
        }

        return Connected;
    }

    public void CancelMatch(string? dodgerHandle = null, long? dodgerAccountId = null)
    {
        if (IsCancelled)
        {
            return;
        }

        Cancellations.Add((dodgerHandle, dodgerAccountId));
        // Like the game
        _ = _cancellation.CancelAsync();
    }

    public void SendDraftSnapshot(FreelancerResolutionPhaseSubType phase, RankedResolutionPhaseData data)
    {
        // The draft keeps changing its data, so keep a copy as it was sent
        RankedResolutionPhaseData copy = data.Clone();
        copy.UnselectedPlayerStates = [..data.UnselectedPlayerStates];
        copy.PlayersOnDeck = [..data.PlayersOnDeck];
        copy.TradeActions = [..data.TradeActions];
        Snapshots.Add((phase, copy));
    }

    public void SendGameInfoNotifications() => GameInfoNotifications++;

    public LobbyCharacterInfo GetCharacterInfo(LobbyServerPlayerInfo slot, CharacterType characterType) =>
        new() { CharacterType = characterType };
}
