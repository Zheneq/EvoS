using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace CentralServer.LobbyServer.Matchmaking;

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
    public int N; // characters controlled per asymmetric player
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
