using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Matchmaking;

/**
 *  Concept

  An asymmetric subtype is a programmatically derived variant of an existing base subtype
  (e.g., the standard 4v4 PvP Deathmatch). In it, one or more players each control N characters
  instead of one, while all other players control one character each. The team still has the same
  total character count (e.g. 4v4) — the asymmetry is only in who controls what.

  ---
  Registration (AsymmetricSubType.cs + startup code)

  An asymmetric subtype is described by an AsymmetricSubTypeDescriptor:
  - ControlledCharacters — how many characters the asymmetric player controls
  - BaseSubTypeName — the existing GameSubType to derive from
  - AllowedAccountIds — which accounts can play the asymmetric role
  - EloCalculator — optional override for post-match ELO math (null = same math as normal)

  AsymmetricSubTypeDescriptor.CreateAdvertisedSubType() builds the GameSubType the server registers:
  it copies the base subtype, sets TeamABots = 0 / TeamBBots = 0, and adds ControlAllBots + NotAllowedForGroups
  to the mod list. This is what the client sees in the queue.

  At startup, you call MatchmakingQueue.RegisterAsymmetricSubType(descriptor), which appends the advertised
  GameSubType to the queue's subtype list (giving it a new bit in the SubTypeMask), creates a matchmaker for it,
  and stores the descriptor internally.

  Multiple asymmetric subtypes derived from the same base share ONE matchmaker pool. Only the first registered
  descriptor for a given base (IsPrimaryForBase = true) runs the active matchmaker; subsequent ones (secondaries)
  are present in the subtype list for client-side bit selection but contribute an empty pool to matchmaking.
  The primary's pool combines all asymmetric variants of the base plus normal-queue fillers, so players with
  different ControlledCharacters values can match together.

  ---
  Queue Eligibility (MatchmakingQueue.FilterSubTypeMaskForGroup)

  When a player joins the queue, their selected subtype bitmask is filtered. For each asymmetric subtype bit,
  descriptor.IsAvailableFor(leader) is checked. If the player is not in AllowedAccountIds, that bit is cleared —
  they cannot queue as the asymmetric role. Groups always have the bit cleared via NotAllowedForGroups
  (only solo players can be asymmetric).

  ---
  Matchmaking — Slot Counting (Matchmaker.cs, MatchmakerBase.cs)

  MatchmakingGroup has an EffectiveSlots field (default = Members.Count). The recursive match-finder in
  MatchmakerBase.MatchScratch.Team uses EffectiveSlots — not Players — when checking and incrementing team
  capacity. So an asymmetric player with ControlledCharacters=3 occupies 3 of the team's 4 slots while being 1 human.

  ---
  Matchmaking — Combined Pool (MatchmakingQueue.GetQueuedGroupsBySubtype)

  For the primary asymmetric subtype of a given base, the pool fed to the matchmaker combines all variants:

  ┌──────────────────────────────────────────────┬────────────────────────────────┐
  │                    Source                    │        EffectiveSlots          │
  ├──────────────────────────────────────────────┼────────────────────────────────┤
  │ Groups with any asymmetric bit for this base │ descriptor.ControlledCharacters│
  │ AND eligible (IsAvailableFor)                │ for that descriptor            │
  ├──────────────────────────────────────────────┼────────────────────────────────┤
  │ Groups with any asymmetric bit for this base │ 1 (filler)                     │
  │ but NOT eligible                             │                                │
  ├──────────────────────────────────────────────┼────────────────────────────────┤
  │ Groups with the base subtype bit set         │ 1 (filler)                     │
  │ (normal queue)                               │                                │
  └──────────────────────────────────────────────┴────────────────────────────────┘

  Secondary asymmetric subtypes (same base, registered later) return an empty pool and run no matches.

  ---
  Match Formation → Game Creation (MatchmakingQueue.StartMatch, MatchmakingManager, PvpGame)

  Once the matchmaker finds a complete match, StartMatch builds two dicts from groups with EffectiveSlots > 1:
  - asymmetricSlots: Dictionary<long, int>    (accountId → ControlledCharacters)
  - asymmetricEloKeys: Dictionary<long, string> (accountId → their specific descriptor's LocalizedName)

  Both are threaded through MatchmakingManager.StartGameAsync → PvpGame.StartGameAsync and stored on Game.

  In PvpGame, the shared GameSubType is cloned for the specific match, and TeamABots/TeamBBots are set to the
  actual proxy count per team. This gives the game server the correct slot count without mutating the shared object.

  ---
  Team Filling — Proxy Assignment (Game.cs)

  FillTeam has an optional asymmetricSlots parameter. When non-null:

  1. Each human player is added normally.
  2. Immediately after, if that player appears in asymmetricSlots with N > 1, AddAsymmetricProxy is called N−1
     times to add proxy slots controlled by them.
  3. The static bot loop is skipped (proxies are added inline instead).

  AddAsymmetricProxy creates a LobbyServerPlayerInfo with:
  - IsNPCBot = false (it's human-controlled)
  - AccountId / Handle = the controlling player's
  - ControllingPlayerId = the controlling player's PlayerId
  - Character picked from account.LastRemoteCharacters[proxyNr] (same as ControlAllBots coop mode)
  - The proxy's PlayerId is added to controllingPlayer.ProxyPlayerIds

  ---
  ELO (MatchmakingQueue.OnGameEnded, Elo.cs)

  When a game ends, asymmetricEloKeys (stored on the Game object) provides per-player ELO key overrides:
  - Asymmetric players update their ELO under their specific descriptor's LocalizedName key
    (e.g. "PvP_Asymmetric_N3") so ratings are tracked separately per role.
  - Normal filler players update under the base "PvP" key as usual.

  The ELO change magnitude is still computed from base PvP ELOs for both teams (most data, most stable).
  Only the storage key differs. descriptor.EloCalculator can override the full calculation if non-null.

  Elo.OnGameEnded deduplicates team lists by AccountId before computing ratings (proxy slots share the
  controlling player's account ID and would otherwise skew averages). The ControlAllBots early-exit only fires
  when no calculator is wired — preserving backward compatibility with existing coop fourlancer games.
 */
public class AsymmetricSubTypeConfig
{
    public string LocalizedName { get; set; }
    public string BaseSubTypeName { get; set; }
    public int NumControlledCharacters { get; set; }
    public TimeSpan TurnTime { get; set; }
}

public class AsymmetricSubTypeDescriptor
{
    public string LocalizedName;
    public string BaseSubTypeName;
    public int BaseSubTypeIndex;
    public int SubTypeIndex; 
    public bool SkipMatchmaking;
    public int NumControlledCharacters;
    public TimeSpan TurnTime;
    
    public GameSubType CreateAdvertisedSubType(GameSubType baseSubType)
    {
        GameSubType advertised = baseSubType.Clone();
        advertised.LocalizedName = LocalizedName;
        advertised.TeamABots = 0;
        advertised.TeamBBots = 0;

        advertised.Mods = new List<GameSubType.SubTypeMods>(baseSubType.Mods ?? []);
        if (!advertised.Mods.Contains(GameSubType.SubTypeMods.ControlAllBots))
        {
            advertised.Mods.Add(GameSubType.SubTypeMods.ControlAllBots);
        }
        if (!advertised.Mods.Contains(GameSubType.SubTypeMods.NotAllowedForGroups))
        {
            advertised.Mods.Add(GameSubType.SubTypeMods.NotAllowedForGroups);
        }

        advertised.GameOverrides = new GameValueOverrides { TurnTimeSpan = TurnTime };

        advertised.Requirements = RequirementCollection.Create();
        if (baseSubType.Requirements != null)
        {
            advertised.Requirements.AddRange(baseSubType.Requirements);
        }
        advertised.Requirements.Add(new QueueRequirement_AccessLevel { AccessLevel = ClientAccessLevel.VIP });

        return advertised;
    }
}
