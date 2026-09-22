using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Network.Static;
using log4net;
using NetSerializer;
using ILog = log4net.ILog;

namespace EvoS.Framework.Network
{
    public sealed class EvosSerializer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EvosSerializer));
        
        private readonly Serializer _serializer = new Serializer(new Dictionary<Type, uint>());

        private readonly Dictionary<uint, Type> _typesById = new Dictionary<uint, Type>();
        private readonly Dictionary<Type, uint> _idsByType = new Dictionary<Type, uint>
        {
            {typeof(object), 1},
            {typeof(int), 11},
            {typeof(int[]), 12},
            {typeof(uint), 13},
            {typeof(float[]), 16},
            {typeof(float), 17},
            {typeof(DateTime?), 18},
            {typeof(DateTime), 19},
            {typeof(bool), 20},
            {typeof(string), 26},
            {typeof(byte[][]), 27},
            {typeof(float?), 33},
            {typeof(int?), 34},
            {typeof(byte[]), 28},
            {typeof(ushort), 53},
            {typeof(long), 82},
            {typeof(byte), 92},
            {typeof(short), 93},
            {typeof(TimeSpan), 158},
            {typeof(double), 159},
            {typeof(TimeSpan?), 162},
            {typeof(string[]), 260},
            {typeof(Guid), 278},
            {typeof(ulong), 319},
            {typeof(DateTime[]), 571},
            {typeof(Dictionary<int, FactionTierAndVersion>), 582},
            {typeof(KeyValuePair<int, FactionTierAndVersion>[]), 583},
            {typeof(KeyValuePair<int, FactionTierAndVersion>), 584},
            {typeof(Dictionary<uint, uint>), 693},
            {typeof(KeyValuePair<uint, uint>[]), 694},
            {typeof(KeyValuePair<uint, uint>), 695},
        };

        private EvosSerializer()
        {
            foreach (var (key, value) in _idsByType)
            {
                _typesById.Add(value, key);
            }
        }

        public EvosSerializer FindTypes()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                .Concat(Assembly.GetEntryAssembly().GetTypes()))
            {
                var attribute = type.GetCustomAttribute<EvosMessageAttribute>();
                if (attribute == null || attribute.TypeID < 0)
                    continue;

                AddType(attribute, type);
            }

            return this;
        }

        public void CheckMissingTypes()
        {
            var typesProcessed = new HashSet<Type>();
            var types = new List<Type>(_idsByType.Keys);

            while (types.Count > 0)
            {
                var type = types[0];
                types.RemoveAt(0);

                // Avoid getting stuck in a cycle
                if (typesProcessed.Contains(type))
                {
                    continue;
                }

                typesProcessed.Add(type);

                if (type.IsAbstract)
                {
                    continue;
                }

                // Check the type itself as we may add arguments from generic types
                if (!_idsByType.ContainsKey(type))
                {
                    throw new EvosSerializerTypeException($"{type} has no type mapping!");
                }

                if (type.IsGenericType)
                {
                    // TODO: This might not be what we want for all generic types
                    var typeIgnore = type.GetCustomAttribute<EvosMessageAttribute>()?.IgnoreGenericArgs;
                    var baseTypeIgnore = type.GetGenericTypeDefinition().GetCustomAttribute<EvosMessageAttribute>()?.IgnoreGenericArgs;
                    if (typeIgnore != true && baseTypeIgnore != true)
                    {
                        types.AddRange(type.GetGenericArguments());
                    }

                    // NetSerializer handles Dictionaries and Lists specially
                    if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                        type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) ||
                        type.GetGenericTypeDefinition() == typeof(List<>) ||
                        type.GetGenericTypeDefinition() == typeof(Queue<>))
                    {
                        continue;
                    }
                }

                // Ensure we have the element type for all arrays
                if (type.HasElementType)
                {
                    types.Add(type.GetElementType());
                }

                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public |
                                                     BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<NonSerializedAttribute>() != null || _idsByType.ContainsKey(field.FieldType))
                    {
                        continue;
                    }

                    throw new EvosSerializerTypeException(
                        $"{type.FullName}.{field.Name} of type {field.FieldType} has no type mapping!");
                }
            }
        }

        /// <summary>
        /// Add <c>ItemType[]</c> entries for <c>List&lt;ItemType&gt;</c> entries where they are missing.
        /// </summary>
        public void FinalizeLists()
        {
            var types = new List<Type>(_idsByType.Keys);

            while (types.Count > 0)
            {
                var type = types[0];
                var typeId = _idsByType[type];
                types.RemoveAt(0);

                if (!type.IsGenericType || !(
                        type.GetGenericTypeDefinition() == typeof(List<>) ||
                        type.GetGenericTypeDefinition() == typeof(Queue<>) 
                        ))
                {
                    continue;
                }

                var itemType = type.GetGenericArguments()[0];
                var arrayType = itemType.MakeArrayType();

                if (!_idsByType.ContainsKey(arrayType))
                {
                    InternalAddType(arrayType, typeId + 1);
//                    InternalAddType(itemType, typeId + 2);
//                    types.Add(itemType);
                }
            }
        }

        public Serializer Build()
        {
            this.FinalizeLists();
            this.CheckMissingTypes();

            this._serializer.AddTypes(_idsByType);

//            this.DumpTypeMap();
            log.Info($"Loaded {_idsByType.Count} {(_idsByType.Count == 1 ? "ID" : "IDs")}");
            return this._serializer;
        }

        public void AddType(EvosMessageAttribute attribute, Type fallbackType)
        {
            var messageType = attribute.Type != null ? attribute.Type : fallbackType;
            var typeId = (uint) attribute.TypeID;

            InternalAddType(messageType, typeId);

            // Specially handle dictionaries 
            if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = messageType.GetGenericArguments()[0];
                var valueType = messageType.GetGenericArguments()[1];
                var keyValuePairType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
                InternalAddType(keyValuePairType.MakeArrayType(), typeId + 1);
                InternalAddType(keyValuePairType, typeId + 2);
                return;
            }

            // Find fields and properties with EvosMessage attributes
            foreach (var member in messageType.GetMembers(BindingFlags.NonPublic | BindingFlags.Public |
                                                          BindingFlags.Instance))
            {
                var memberAttribute = member.GetCustomAttribute<EvosMessageAttribute>();
                if (memberAttribute == null)
                    continue;
                Type memberType = null;
                if (member.MemberType == MemberTypes.Field && ((FieldInfo) member).GetCustomAttribute<NonSerializedAttribute>() == null)
                {
                    memberType = ((FieldInfo) member).FieldType;
                }
                else if (member.MemberType == MemberTypes.Property)
                {
                    memberType = ((PropertyInfo) member).PropertyType;
                }

                if (memberType != null)
                {
                    AddType(memberAttribute, memberType);
                }
            }
        }

        private void InternalAddType(Type messageType, uint typeId)
        {
            if (!_idsByType.TryAdd(messageType, typeId) && _idsByType[messageType] != typeId)
            {
                throw new EvosSerializerTypeException(
                    $"Cannot map {messageType} to {typeId}, as it is already mapped to {_idsByType[messageType]}");
            }
            if (!_typesById.TryAdd(typeId, messageType) && _typesById[typeId] != messageType)
            {
                throw new EvosSerializerTypeException(
                    $"Cannot map {typeId} to {messageType}, as it is already mapped to {_typesById[typeId]}");
            }
        }

        public void DumpTypeMap()
        {
            foreach (var (type, typeId) in this._serializer.GetTypeMap().OrderBy(x => x.Value))
            {
                Console.WriteLine($"{typeId} - {type}");
            }
        }

        public static Serializer Instance { get; } = new EvosSerializer()
            .FindTypes()
            .Build();
    }
}
