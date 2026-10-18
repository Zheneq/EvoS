using System.Reflection;
using CentralServer.LobbyServer;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;

namespace Tests;

public class ProtocolPatcherTest
{
    // Messages that carry requirements but are only sent by clients
    private static readonly Type[] ClientToServer =
    [
        typeof(CreateGameRequest),
        typeof(GameInfoUpdateRequest),
        typeof(RejoinGameRequest),
    ];

    private static readonly Assembly[] Assemblies = [typeof(WebSocketMessage).Assembly, typeof(ProtocolPatcher).Assembly];

    [Fact]
    public void AllMessagesCarryingRequirementsArePatched()
    {
        // If this test fails, a message that can carry a RequirementCollection is sent to clients
        // without passing through ProtocolPatcher. Add it to ProtocolPatcher.Mapping.
        List<Type> carriers = Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(WebSocketMessage)) && !t.IsAbstract)
            .Where(t => Reaches(t, typeof(RequirementCollection), []))
            .Except(ClientToServer)
            .OrderBy(t => t.FullName)
            .ToList();

        List<Type> patched = ProtocolPatcher.Mapping.Keys.OrderBy(t => t.FullName).ToList();

        Assert.Equal(carriers, patched);
    }

    [Fact]
    public void VanillaRequirementsAreNotCopied()
    {
        GameSubType subType = new GameSubType { Requirements = RequirementCollection.Create() };
        subType.Requirements.Add(new QueueRequirement_AccessLevel { AccessLevel = ClientAccessLevel.VIP });

        GameInfoNotification gameInfo = new GameInfoNotification
        {
            GameInfo = new LobbyGameInfo { GameConfig = new LobbyGameConfig { SubTypes = [subType] } }
        };
        Assert.Same(gameInfo, Patch(gameInfo));

        LobbyServerReadyNotification ready = new LobbyServerReadyNotification
        {
            ServerQueueConfiguration = new ServerQueueConfigurationUpdateNotification
            {
                GameTypeAvailabilies = new Dictionary<GameType, GameTypeAvailability>
                {
                    {
                        GameType.PvP, new GameTypeAvailability
                        {
                            QueueableGroupSizes = new Dictionary<int, RequirementCollection> { { 1, null! } },
                            SubTypes = [subType]
                        }
                    }
                }
            }
        };
        Assert.Same(ready, Patch(ready));
    }

    private static WebSocketMessage Patch(WebSocketMessage message)
    {
        return ProtocolPatcher.Mapping[message.GetType()](message, ProtocolVersion.VANILLA);
    }

    private static bool Reaches(Type type, Type target, HashSet<Type> visited)
    {
        if (type == target)
        {
            return true;
        }
        if (!visited.Add(type))
        {
            return false;
        }

        List<Type> children = [];
        if (type.GetElementType() is { } elementType)
        {
            children.Add(elementType);
        }
        if (type.IsGenericType)
        {
            children.AddRange(type.GetGenericArguments());
        }
        if (Assemblies.Contains(type.Assembly))
        {
            children.AddRange(type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttribute<NonSerializedAttribute>() == null)
                .Select(f => f.FieldType));
            if (type.BaseType is not null)
            {
                children.Add(type.BaseType);
            }
        }

        return children.Any(t => Reaches(t, target, visited));
    }
}
