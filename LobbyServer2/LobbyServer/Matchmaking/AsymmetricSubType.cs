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

  For an asymmetric subtype index i, the pool fed to its matchmaker is a union of two group sets:

  ┌────────────────────────────────┬──────────────────────┐
  │             Source             │    EffectiveSlots    │
  ├────────────────────────────────┼──────────────────────┤
  │ Groups with bit i set AND      │ ControlledCharacters │
  │ eligible (IsAvailableFor)      │                      │
  ├────────────────────────────────┼──────────────────────┤
  │ Groups with bit i set but NOT  │ 1 (filler)           │
  │ eligible                       │                      │
  ├────────────────────────────────┼──────────────────────┤
  │ Groups with the base sub-type  │ 1 (filler)           │
  │ bit set (normal queue)         │                      │
  └────────────────────────────────┴──────────────────────┘

  This lets normal PvP players fill the remaining slots in an asymmetric match without needing to know anything special.

  ---
  Match Formation → Game Creation (MatchmakingQueue.StartMatch, MatchmakingManager, PvpGame)

  Once the matchmaker finds a complete match, StartMatch inspects each TeamA group's EffectiveSlots.
  Any group with EffectiveSlots > 1 maps its members to asymmetricSlots:
  Dictionary<long, int> (accountId → ControlledCharacters).
  This dict is threaded through MatchmakingManager.StartGameAsync → PvpGame.StartGameAsync.

  TODO does game server care?
  In PvpGame, the shared GameSubType is cloned for the specific match, and TeamABots is set to the actual total
  proxy count (sum of N-1 per asymmetric player). This gives the game server the correct slot count without
  mutating the shared subtype object.

  ---
  Team Filling — Proxy Assignment (Game.cs)

  FillTeam has an optional asymmetricSlots parameter. When non-null and filling TeamA:

  1. Each human player is added normally.
  2. Immediately after, if that player appears in asymmetricSlots with N > 1, AddAsymmetricProxy is called N−1 times
    to add proxy slots controlled by them.
  3. The static bot loop is skipped (proxies are added inline instead).

  AddAsymmetricProxy creates a LobbyServerPlayerInfo with:
  - IsNPCBot = false (it's human-controlled)
  - AccountId / Handle = the controlling player's
  - ControllingPlayerId = the controlling player's PlayerId
  - Character picked from account.LastRemoteCharacters[proxyNr] (same as ControlAllBots coop mode)
  - The proxy's PlayerId is added to controllingPlayer.ProxyPlayerIds

  TeamB always uses the normal FillTeam path — no proxies.

  ---
  ELO (MatchmakingQueue.OnGameEnded, Elo.cs)

  When a game ends, if the sub-type is asymmetric:
  - The ELO key is the sub-type's LocalizedName (e.g. "PvP_Asymmetric_N3") instead of "PvP", 
    so asymmetric ratings are tracked separately.
  - descriptor.EloCalculator is passed to Elo.OnGameEnded. If non-null, it replaces the standard ELO math.
    If null, the existing formula runs unchanged.

  Elo.OnGameEnded deduplicates team lists by AccountId before computing ratings (proxy slots share
  the controlling player's account ID and would otherwise skew averages). The ControlAllBots early-exit only fires
   when no calculator is wired — this preserves backward compatibility with existing coop fourlancer games while
  letting asymmetric PvP games update ELO normally.
 */
public interface IAsymmetricEloCalculator
{
    float CalculateEloChange(
        List<PersistedAccountData> teamA,
        List<PersistedAccountData> teamB,
        Dictionary<long, int> asymmetricSlots,
        string eloKey,
        MatchmakingConfiguration conf,
        int result);
}

public class AsymmetricSubTypeDescriptor
{
    public string LocalizedName;
    public string BaseSubTypeName;
    public int BaseSubTypeIndex = -1; // set by MatchmakingQueue.RegisterAsymmetricSubType
    public int ControlledCharacters;
    public HashSet<long> AllowedAccountIds = new();
    public IAsymmetricEloCalculator EloCalculator; // null = same math as standard

    public bool IsAvailableFor(long accountId)
    {
        // Time-window filtering is a stub — fill in later
        return AllowedAccountIds.Contains(accountId);
    }

    public GameSubType CreateAdvertisedSubType(GameSubType baseSubType)
    {
        GameSubType advertised = baseSubType.Clone();
        advertised.LocalizedName = LocalizedName;
        advertised.TeamABots = 0;
        advertised.TeamBBots = 0;

        // Fresh Mods list so we don't mutate the original
        advertised.Mods = new List<GameSubType.SubTypeMods>(baseSubType.Mods ?? new List<GameSubType.SubTypeMods>());
        if (!advertised.Mods.Contains(GameSubType.SubTypeMods.ControlAllBots))
        {
            advertised.Mods.Add(GameSubType.SubTypeMods.ControlAllBots);
        }
        if (!advertised.Mods.Contains(GameSubType.SubTypeMods.NotAllowedForGroups))
        {
            advertised.Mods.Add(GameSubType.SubTypeMods.NotAllowedForGroups);
        }

        return advertised;
    }
}

public static class AsymmetricSubTypeManager
{
    public static readonly Dictionary<string, AsymmetricSubTypeDescriptor> Descriptors = new();

    public static void Register(AsymmetricSubTypeDescriptor descriptor)
    {
        Descriptors[descriptor.LocalizedName] = descriptor;
    }

    public static AsymmetricSubTypeDescriptor GetDescriptor(string localizedName)
    {
        Descriptors.TryGetValue(localizedName, out var d);
        return d;
    }

    public static bool IsAsymmetricRole(long accountId, string localizedName)
    {
        var d = GetDescriptor(localizedName);
        return d != null && d.IsAvailableFor(accountId);
    }
}
