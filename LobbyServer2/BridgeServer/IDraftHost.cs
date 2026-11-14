using System;
using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;

namespace CentralServer.BridgeServer;

// What a draft needs from its game. Game implements the members it does not already have explicitly.
public interface IDraftHost
{
    IReadOnlyList<LobbyServerPlayerInfo> Roster { get; }
    LobbyServerPlayerInfo GetPlayerById(int playerId);
    // The slot of the account itself, not one of the slots it controls
    LobbyServerPlayerInfo GetMainSlot(long accountId);
    bool IsCancelled { get; }
    bool IsControlAllBots { get; }
    bool HasBans { get; }
    DraftTimeouts Timeouts { get; }
    // Cancels the match if someone has disconnected
    bool CheckParticipantsConnected();
    void CancelMatch(string dodgerHandle = null, long? dodgerAccountId = null);
    // Sends the draft state to every player in the game
    void SendDraftSnapshot(FreelancerResolutionPhaseSubType phase, RankedResolutionPhaseData data);
    void SendGameInfoNotifications();
    // Character info for a slot that ends the draft with the given character
    LobbyCharacterInfo GetCharacterInfo(LobbyServerPlayerInfo slot, CharacterType characterType);
}

public readonly record struct DraftTimeouts(TimeSpan Ban1, TimeSpan Ban2, TimeSpan Pick, TimeSpan Trade);
