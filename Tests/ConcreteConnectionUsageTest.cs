using System.Reflection;
using CentralServer.LobbyServer;
using CentralServer.LobbyServer.Session;

namespace Tests;

/// <summary>
/// Outside namespace <c>CentralServer.LobbyServer.Session</c>, code works with <see cref="IClientConnection"/>,
/// never with the concrete <see cref="LobbyServerProtocol"/>, so that everything can be tested with
/// <c>RecordingClientConnection</c>. Reflection sees fields, properties, parameters and return types
/// (including generic arguments, arrays and out parameters), but not locals.
/// </summary>
public class ConcreteConnectionUsageTest
{
    private const string SessionNamespace = "CentralServer.LobbyServer.Session";

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void NothingOutsideSessionDependsOnLobbyServerProtocol()
    {
        string[] violations = typeof(LobbyServerProtocol).Assembly.GetTypes()
            .Where(type => !IsExempt(type))
            .SelectMany(type => FindMentions(type).Select(mention => $"{type.FullName}.{mention}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void FindsEveryKindOfMention()
    {
        string[] expected =
        [
            ".ctor(constructorParameter) (parameter)",
            "Field (field)",
            "Parameter(connection) (parameter)",
            "Property (property)",
            "Return (return type)",
        ];

        Assert.Equal(expected, FindMentions(typeof(Fixture)).Order(StringComparer.Ordinal));
    }

    private static bool IsExempt(Type type)
    {
        // Compiler-generated closures and state machines are nested in the type whose code they belong to
        Type outermost = type;
        while (outermost.DeclaringType != null)
        {
            outermost = outermost.DeclaringType;
        }

        return outermost == typeof(LobbyServerProtocol)
               || outermost.Namespace == SessionNamespace
               || outermost.Namespace?.StartsWith(SessionNamespace + ".") == true;
    }

    private static IEnumerable<string> FindMentions(Type type)
    {
        foreach (FieldInfo field in type.GetFields(Declared))
        {
            if (Mentions(field.FieldType))
            {
                yield return $"{field.Name} (field)";
            }
        }

        HashSet<MethodInfo> accessors = [];
        foreach (PropertyInfo property in type.GetProperties(Declared))
        {
            accessors.UnionWith(property.GetAccessors(true));
            if (Mentions(property.PropertyType))
            {
                yield return $"{property.Name} (property)";
            }
        }

        IEnumerable<MethodBase> methods = type.GetMethods(Declared)
            .Where(method => !accessors.Contains(method))
            .Concat<MethodBase>(type.GetConstructors(Declared));
        foreach (MethodBase method in methods)
        {
            if (method is MethodInfo methodInfo && Mentions(methodInfo.ReturnType))
            {
                yield return $"{method.Name} (return type)";
            }
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (Mentions(parameter.ParameterType))
                {
                    yield return $"{method.Name}({parameter.Name}) (parameter)";
                }
            }
        }
    }

    private static bool Mentions(Type type)
    {
        return type == typeof(LobbyServerProtocol)
               || type.HasElementType && Mentions(type.GetElementType()!)
               || type.IsGenericType && type.GetGenericArguments().Any(Mentions);
    }

    /// <summary>One mention of each kind, plus members that must not count.</summary>
    private abstract class Fixture
    {
        public List<LobbyServerProtocol>? Field = null;
        public IClientConnection? Allowed = null;

        protected Fixture(Func<LobbyServerProtocol>? constructorParameter)
        {
        }

        public abstract LobbyServerProtocol?[] Property { get; }
        public abstract LobbyServerProtocol? Return();
        public abstract void Parameter(out LobbyServerProtocol? connection);
        public abstract IClientConnection AllowedMethod(IClientConnection connection);
    }
}
