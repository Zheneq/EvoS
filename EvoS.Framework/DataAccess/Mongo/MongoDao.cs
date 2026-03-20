using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EvoS.Framework.Misc;
using log4net;
using MongoDB.Driver;

namespace EvoS.Framework.DataAccess.Mongo
{
    public class MongoDao<TKey, TEntry> where TEntry: class
    {
        private static readonly ILog log = LogManager.GetLogger("MongoDao");
        
        private const MongoCollectionSettings settings = null;

        protected readonly IMongoCollection<TEntry> c;
        
        protected MongoDao(string collectionName, params CreateIndexModel<TEntry>[] indices)
        {
            IMongoDatabase mongo = MongoDB.GetDatabase();
            List<string> collections = mongo.ListCollectionNames().ToList();
            bool init = !collections.Contains(collectionName);
            if (init)
            {
                log.Info($"Collection {collectionName} not found in [{string.Join(", ", collections)}], creating...");
                try
                {
                    mongo.CreateCollection(collectionName); // TODO MONGO settings
                }
                catch (Exception e)
                {
                    log.Error("Failed to create a collection", e);
                    throw;
                }
            }
            c = mongo.GetCollection<TEntry>(collectionName, settings);
            if (init && !indices.IsNullOrEmpty())
            {
                try
                {
                    c.Indexes.CreateMany(indices);
                }
                catch (Exception e)
                {
                    log.Error("Failed to create an index", e);
                    throw;
                }
            }
        }

        protected TEntry findById(TKey id)
        {
            return c.Find(Key(id)).FirstOrDefault();
        }

        protected void insert(TKey id, TEntry entry)
        {
            c.ReplaceOne(
                filter: Key(id),
                options: new ReplaceOptions { IsUpsert = true },
                replacement: entry);
        }

        protected UpdateResult UpdateField<TField>(TKey id, TEntry entry, FieldDefinition<TField> field)
        {
            return c.UpdateOne(
                Key(id), 
                u.Set(field._expr, field._compiled.Invoke(entry)));
        }

        protected UpdateResult UpdateField<TField>(TKey id, TField entry, FieldDefinition<TField> field)
        {
            return c.UpdateOne(
                Key(id), 
                u.Set(field._expr, entry));
        }
        
        protected static FilterDefinitionBuilder<TEntry> f => Builders<TEntry>.Filter;
        protected static UpdateDefinitionBuilder<TEntry> u => Builders<TEntry>.Update;
        protected static SortDefinitionBuilder<TEntry> s => Builders<TEntry>.Sort;
        protected static ProjectionDefinitionBuilder<TEntry> p => Builders<TEntry>.Projection;

        protected FilterDefinition<TEntry> Key(TKey id)
        {
            return f.Eq("_id", id);
        }

        protected class FieldDefinition<TField>
        {
            public readonly Expression<Func<TEntry, TField>> _expr;
            public readonly Func<TEntry, TField> _compiled;
            
            public FieldDefinition(Expression<Func<TEntry, TField>> field)
            {
                _expr = field;
                _compiled = _expr.Compile();
            }
        }
    }
}