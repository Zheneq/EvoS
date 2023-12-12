using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface MiscDao
    {
        Entry GetEntry(string Key);
        void SaveEntry(Entry entry);

        [BsonKnownTypes(typeof(TextEntry), typeof(TrustWarEntry))]
        class Entry
        {
            public required string _id;

            public string Key => _id;
        }
        
        class TextEntry : Entry
        {
            public required string Message;
        }
        
        class TrustWarEntry : Entry
        {
            public required long[] Points;
        }
    }
}