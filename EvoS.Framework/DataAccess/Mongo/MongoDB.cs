using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EvoS.Framework.Misc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class MongoDB
    {
        private static MongoDB Instance;

        private readonly IMongoDatabase database;
        
        private MongoDB()
        {
            ConventionRegistry.Register(
                "DictionaryRepresentationConvention",
                new ConventionPack {new DictionaryRepresentationConvention(DictionaryRepresentation.ArrayOfArrays)},
                _ => true);
            BsonSerializer.RegisterSerializationProvider(new StructSerializationProvider());
            
            EvosConfiguration.DBConfig dbConfig = EvosConfiguration.GetDBConfig();
            string conn = $"mongodb{(dbConfig.MongoDbSrv ? "+srv" : "")}://{dbConfig.User}:{dbConfig.Password}@{dbConfig.URI}";
            MongoClientSettings settings = MongoClientSettings.FromConnectionString(conn);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            MongoClient client = new MongoClient(settings);
            database = client.GetDatabase(dbConfig.Database);
        }

        public static IMongoDatabase GetInstance()
        {
            Instance ??= new MongoDB();
            return Instance.database;
        }

        public class DictionaryRepresentationConvention : ConventionBase, IMemberMapConvention
        {
            private readonly DictionaryRepresentation _dictionaryRepresentation;

            public DictionaryRepresentationConvention(
                DictionaryRepresentation dictionaryRepresentation = DictionaryRepresentation.ArrayOfDocuments)
            {
                // see http://mongodb.github.io/mongo-csharp-driver/2.2/reference/bson/mapping/#dictionary-serialization-options

                _dictionaryRepresentation = dictionaryRepresentation;
            }

            public void Apply(BsonMemberMap memberMap)
            {
                memberMap.SetSerializer(ConfigureSerializer(memberMap.GetSerializer(), Array.Empty<IBsonSerializer>()));
            }

            private IBsonSerializer ConfigureSerializer(IBsonSerializer serializer, IBsonSerializer[] stack)
            {
                if (serializer is IDictionaryRepresentationConfigurable dictionaryRepresentationConfigurable)
                {
                    serializer =
                        dictionaryRepresentationConfigurable.WithDictionaryRepresentation(_dictionaryRepresentation);
                }

                if (serializer is IChildSerializerConfigurable childSerializerConfigurable)
                {
                    if (!stack.Contains(childSerializerConfigurable.ChildSerializer))
                    {
                        var newStack = stack.Union(new[] { serializer }).ToArray();
                        var childConfigured =
                            ConfigureSerializer(childSerializerConfigurable.ChildSerializer, newStack);
                        return childSerializerConfigurable.WithChildSerializer(childConfigured);
                    }
                }

                return serializer;
            }
        }
    }
    
    public class StructSerializationProvider : IBsonSerializationProvider
    {
        private readonly Dictionary<Type, IBsonSerializer> Serializers;
        public StructSerializationProvider()
        {
            Serializers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsValueType &&
                            (t.Attributes & TypeAttributes.Serializable) == TypeAttributes.Serializable)
                .ToDictionary(
                    t => t,
                    t => (IBsonSerializer) Activator.CreateInstance(typeof(StructSerializer<>).MakeGenericType(t)));
        }

        public IBsonSerializer GetSerializer(Type type)
        {
            return Serializers.TryGetValue(type, out var serializer) ? serializer : null;
        }
    }
    
    public class StructSerializer<T> : SerializerBase<T>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            IBsonSerializer serializer = BsonSerializer.LookupSerializer(typeof(BsonDocument));
            string json = DefaultJsonSerializer.Serialize(value);
            try
            {
                BsonDocument bsonDocument = BsonDocument.Parse(json);
                serializer.Serialize(context, bsonDocument.AsBsonValue);
            }
            catch (FormatException e)
            {
                context.Writer.WriteString(json);
            }
        }

        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            IBsonSerializer serializer = BsonSerializer.LookupSerializer(typeof(BsonDocument));
            string json;
            if (context.Reader.CurrentBsonType == BsonType.String)
            {
                json = context.Reader.ReadString();
            }
            else
            {
                json = serializer.Deserialize(context, args).ToBsonDocument().ToJson();
            }
            
            return DefaultJsonSerializer.Deserialize<T>(json);
        }
    }
}