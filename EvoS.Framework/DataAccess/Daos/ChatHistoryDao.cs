using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.NetworkMessages;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface ChatHistoryDao
    {
        List<Entry> GetRelevantMessages(
            long accountId,
            bool includeBlocked,
            bool includeGeneral,
            bool isAfter,
            DateTime afterTime,
            int limit);
        void Save(Entry entry);
        
        public class Entry
        {
            [BsonId] public ObjectId _id = ObjectId.GenerateNewId();
            public long sender;
            public DateTime time;
            public string message;
            public string game;
            public List<long> recipients;
            public List<long> blockedRecipients;
            public bool isMuted;
            public Team senderTeam;
            public CharacterType characterType;
            public ConsoleMessageType consoleMessageType;

            public Entry(
                ChatNotification Notify,
                DateTime Time,
                string Game,
                IEnumerable<long> Recipients,
                IEnumerable<long> BlockedRecipients,
                bool IsMuted)
            {
                sender = Notify.SenderAccountId;
                time = Time;
                message = Notify.Text;
                game = Game;
                recipients = Recipients.ToList();
                blockedRecipients = BlockedRecipients.ToList();
                isMuted = IsMuted;
                senderTeam = Notify.SenderTeam;
                characterType = Notify.CharacterType;
                consoleMessageType = Notify.ConsoleMessageType;
            }
        }
    }
}