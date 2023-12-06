#nullable enable
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface TrustWarDao
    {
        public TrustWarDaoEntry? Find();
        public void Save(TrustWarDaoEntry entry);

        public class TrustWarDaoEntry
        {
            [BsonId] public ObjectId _id = ObjectId.GenerateNewId();
            public long Omni;
            public long Evos;
            public long Warbotics;

            public TrustWarDaoEntry Use()
            {
                return new TrustWarDaoEntry
                {
                    Omni = Omni,
                    Evos = Evos,
                    Warbotics = Warbotics,
                };
            }
        }
    }
}