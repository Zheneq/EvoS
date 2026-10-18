using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer;

/// <summary>
/// Replaces queue requirements the client cannot deserialize (see <see cref="QueueRequirement_ProtocolDependent"/>)
/// in outgoing messages. Shared objects are never modified: every patch returns the original object when
/// nothing has to change and clones it otherwise.
/// </summary>
public static class ProtocolPatcher
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ProtocolPatcher));

    public static readonly Dictionary<Type, Func<WebSocketMessage, string, WebSocketMessage>> Mapping = new[]
    {
        Entry<LobbyServerReadyNotification>(Patch),
        Entry<ServerQueueConfigurationUpdateNotification>(Patch),
        Entry<GameInfoNotification>(Patch),
        Entry<GameAssignmentNotification>(Patch),
        Entry<GameInfoUpdateResponse>(Patch),
        Entry<PreviousGameInfoResponse>(Patch),
        Entry<LobbyCustomGamesNotification>(Patch),
        Entry<MatchmakingQueueAssignmentNotification>(Patch),
        Entry<MatchmakingQueueStatusNotification>(Patch),
    }.ToDictionary();

    // Message patches are only called from here with the message being sent, so they don't check it for null.
    // Everything nested inside a message can be null.
    private static KeyValuePair<Type, Func<WebSocketMessage, string, WebSocketMessage>> Entry<T>(
        Func<T, string, T> patch)
        where T : WebSocketMessage
    {
        return new(typeof(T), (message, protocolVersion) =>
        {
            T patched = patch((T)message, protocolVersion);
            if (patched != message)
            {
                log.Debug($"Replaced queue requirements in {typeof(T).Name} for protocol version {protocolVersion}");
            }
            return patched;
        });
    }

    private static LobbyServerReadyNotification Patch(LobbyServerReadyNotification message, string protocolVersion)
    {
        ServerQueueConfigurationUpdateNotification config = Patch(message.ServerQueueConfiguration, protocolVersion);
        if (config == message.ServerQueueConfiguration)
        {
            return message;
        }
        LobbyServerReadyNotification patched = message.Clone();
        patched.ServerQueueConfiguration = config;
        return patched;
    }

    private static ServerQueueConfigurationUpdateNotification Patch(
        ServerQueueConfigurationUpdateNotification message,
        string protocolVersion)
    {
        if (message is null)
        {
            return null;
        }
        Dictionary<GameType, GameTypeAvailability> gameTypes =
            PatchValues(message.GameTypeAvailabilies, gta => Patch(gta, protocolVersion));
        Dictionary<CharacterType, RequirementCollection> freeRotationAdditions =
            PatchValues(message.FreeRotationAdditions, req => Patch(req, protocolVersion));
        if (gameTypes == message.GameTypeAvailabilies && freeRotationAdditions == message.FreeRotationAdditions)
        {
            return message;
        }
        ServerQueueConfigurationUpdateNotification patched = message.Clone();
        patched.GameTypeAvailabilies = gameTypes;
        patched.FreeRotationAdditions = freeRotationAdditions;
        return patched;
    }

    private static GameInfoNotification Patch(GameInfoNotification message, string protocolVersion)
    {
        LobbyGameInfo gameInfo = Patch(message.GameInfo, protocolVersion);
        if (gameInfo == message.GameInfo)
        {
            return message;
        }
        GameInfoNotification patched = message.Clone();
        patched.GameInfo = gameInfo;
        return patched;
    }

    private static GameAssignmentNotification Patch(GameAssignmentNotification message, string protocolVersion)
    {
        LobbyGameInfo gameInfo = Patch(message.GameInfo, protocolVersion);
        if (gameInfo == message.GameInfo)
        {
            return message;
        }
        GameAssignmentNotification patched = message.Clone();
        patched.GameInfo = gameInfo;
        return patched;
    }

    private static GameInfoUpdateResponse Patch(GameInfoUpdateResponse message, string protocolVersion)
    {
        LobbyGameInfo gameInfo = Patch(message.GameInfo, protocolVersion);
        if (gameInfo == message.GameInfo)
        {
            return message;
        }
        GameInfoUpdateResponse patched = message.Clone();
        patched.GameInfo = gameInfo;
        return patched;
    }

    private static PreviousGameInfoResponse Patch(PreviousGameInfoResponse message, string protocolVersion)
    {
        LobbyGameInfo gameInfo = Patch(message.PreviousGameInfo, protocolVersion);
        if (gameInfo == message.PreviousGameInfo)
        {
            return message;
        }
        PreviousGameInfoResponse patched = message.Clone();
        patched.PreviousGameInfo = gameInfo;
        return patched;
    }

    private static LobbyCustomGamesNotification Patch(LobbyCustomGamesNotification message, string protocolVersion)
    {
        List<LobbyGameInfo> gameInfos = PatchItems(message.CustomGameInfos, gi => Patch(gi, protocolVersion));
        if (gameInfos == message.CustomGameInfos)
        {
            return message;
        }
        LobbyCustomGamesNotification patched = message.Clone();
        patched.CustomGameInfos = gameInfos;
        return patched;
    }

    private static MatchmakingQueueAssignmentNotification Patch(
        MatchmakingQueueAssignmentNotification message,
        string protocolVersion)
    {
        LobbyMatchmakingQueueInfo queueInfo = Patch(message.MatchmakingQueueInfo, protocolVersion);
        if (queueInfo == message.MatchmakingQueueInfo)
        {
            return message;
        }
        MatchmakingQueueAssignmentNotification patched = message.Clone();
        patched.MatchmakingQueueInfo = queueInfo;
        return patched;
    }

    private static MatchmakingQueueStatusNotification Patch(
        MatchmakingQueueStatusNotification message,
        string protocolVersion)
    {
        LobbyMatchmakingQueueInfo queueInfo = Patch(message.MatchmakingQueueInfo, protocolVersion);
        if (queueInfo == message.MatchmakingQueueInfo)
        {
            return message;
        }
        MatchmakingQueueStatusNotification patched = message.Clone();
        patched.MatchmakingQueueInfo = queueInfo;
        return patched;
    }

    private static LobbyMatchmakingQueueInfo Patch(LobbyMatchmakingQueueInfo queueInfo, string protocolVersion)
    {
        if (queueInfo is null)
        {
            return null;
        }
        LobbyGameConfig config = Patch(queueInfo.GameConfig, protocolVersion);
        if (config == queueInfo.GameConfig)
        {
            return queueInfo;
        }
        LobbyMatchmakingQueueInfo patched = queueInfo.Clone();
        patched.GameConfig = config;
        return patched;
    }

    private static LobbyGameInfo Patch(LobbyGameInfo gameInfo, string protocolVersion)
    {
        if (gameInfo is null)
        {
            return null;
        }
        LobbyGameConfig config = Patch(gameInfo.GameConfig, protocolVersion);
        if (config == gameInfo.GameConfig)
        {
            return gameInfo;
        }
        LobbyGameInfo patched = gameInfo.Clone();
        patched.GameConfig = config;
        return patched;
    }

    private static LobbyGameConfig Patch(LobbyGameConfig config, string protocolVersion)
    {
        if (config is null)
        {
            return null;
        }
        List<GameSubType> subTypes = PatchItems(config.SubTypes, st => Patch(st, protocolVersion));
        if (subTypes == config.SubTypes)
        {
            return config;
        }
        LobbyGameConfig patched = config.Clone();
        patched.SubTypes = subTypes;
        return patched;
    }

    private static GameTypeAvailability Patch(GameTypeAvailability gameType, string protocolVersion)
    {
        if (gameType is null)
        {
            return null;
        }
        RequirementCollection requirements = Patch(gameType.Requirements, protocolVersion);
        Dictionary<int, RequirementCollection> groupSizes =
            PatchValues(gameType.QueueableGroupSizes, req => Patch(req, protocolVersion));
        List<GameSubType> subTypes = PatchItems(gameType.SubTypes, st => Patch(st, protocolVersion));
        if (requirements == gameType.Requirements
            && groupSizes == gameType.QueueableGroupSizes
            && subTypes == gameType.SubTypes)
        {
            return gameType;
        }
        GameTypeAvailability patched = gameType.Clone();
        patched.Requirements = requirements;
        patched.QueueableGroupSizes = groupSizes;
        patched.SubTypes = subTypes;
        return patched;
    }

    private static GameSubType Patch(GameSubType subType, string protocolVersion)
    {
        if (subType is null)
        {
            return null;
        }
        RequirementCollection requirements = Patch(subType.Requirements, protocolVersion);
        if (requirements == subType.Requirements)
        {
            return subType;
        }
        GameSubType patched = subType.Clone();
        patched.Requirements = requirements;
        return patched;
    }

    private static RequirementCollection Patch(RequirementCollection requirements, string protocolVersion)
    {
        return requirements?.ForProtocolVersion(protocolVersion);
    }

    // Keeps item order: subtype indices are the bits of the subtype mask
    private static List<T> PatchItems<T>(List<T> items, Func<T, T> patch) where T : class
    {
        if (items is null)
        {
            return null;
        }
        List<T> patched = null;
        for (int i = 0; i < items.Count; i++)
        {
            T item = patch(items[i]);
            if (item != items[i])
            {
                patched ??= [..items];
                patched[i] = item;
            }
        }
        return patched ?? items;
    }

    private static Dictionary<TKey, TValue> PatchValues<TKey, TValue>(
        Dictionary<TKey, TValue> dict,
        Func<TValue, TValue> patch)
        where TValue : class
    {
        if (dict is null)
        {
            return null;
        }
        Dictionary<TKey, TValue> patched = null;
        foreach ((TKey key, TValue value) in dict)
        {
            TValue newValue = patch(value);
            if (newValue != value)
            {
                patched ??= new Dictionary<TKey, TValue>(dict);
                patched[key] = newValue;
            }
        }
        return patched ?? dict;
    }
}
