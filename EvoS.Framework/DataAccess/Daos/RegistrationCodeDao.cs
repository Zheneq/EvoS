using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface RegistrationCodeDao
    {
        public const int LIMIT = 25;

        public RegistrationCodeEntry Find(string code);
        public List<RegistrationCodeEntry> FindIssuedBefore(int limit, DateTime dateTime);
        public List<RegistrationCodeEntry> FindAllIssued(int limit, int offset);
        public List<RegistrationCodeEntry> FindByState(RegistrationState state, int limit);
        public RegistrationCodeEntry FindLatestByDiscordUser(ulong discordUserId);
        public void Save(RegistrationCodeEntry entry);

        // Issued = 0 so legacy rows and manually issued codes (with no State field) are treated as issued.
        public enum RegistrationState
        {
            Issued = 0,
            Requested = 1,
            Declined = 2
        }

        public class RegistrationCodeEntry
        {
            [BsonId]
            public string Code;
            public RegistrationState State;
            public long IssuedBy;
            public string IssuedTo;
            public DateTime IssuedAt;
            public DateTime ExpiresAt;
            public DateTime UsedAt;
            public long UsedBy;

            public ulong DiscordUserId;
            public string DiscordUserName;
            public string DiscordDisplayName;
            public string DiscordAvatarUrl;
            public DateTime DiscordCreatedAt;
            public DateTime? DiscordJoinedAt;
            public string DeclineReason;
            public DateTime RequestedAt;

            [JsonIgnore]
            public bool IsValid => !IsUsed && !HasExpired;

            [JsonIgnore]
            public bool IsUsed => UsedBy != 0;

            [JsonIgnore]
            public bool HasExpired => ExpiresAt < DateTime.UtcNow;

            public RegistrationCodeEntry Use(long accountId)
            {
                return new RegistrationCodeEntry
                {
                    Code = Code,
                    State = State,
                    IssuedBy = IssuedBy,
                    IssuedTo = IssuedTo,
                    IssuedAt = IssuedAt,
                    ExpiresAt = ExpiresAt,
                    UsedAt = DateTime.UtcNow,
                    UsedBy = accountId,
                    DiscordUserId = DiscordUserId,
                    DiscordUserName = DiscordUserName,
                    DiscordDisplayName = DiscordDisplayName,
                    DiscordAvatarUrl = DiscordAvatarUrl,
                    DiscordCreatedAt = DiscordCreatedAt,
                    DiscordJoinedAt = DiscordJoinedAt,
                    DeclineReason = DeclineReason,
                    RequestedAt = RequestedAt
                };
            }
        }
    }
}
