using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.DataAccess.Daos
{
    public interface MatchHistoryDao
    {
        public const int LIMIT = 100;
        
        public List<PersistedCharacterMatchData> Find(long accountId);
        public void Save(ICollection<MatchEntry> matchEntries);

        public class MatchEntry
        {
            public long AccountId;
            public PersistedCharacterMatchData Data;

            public static List<MatchEntry> Cons(LobbyGameInfo gameInfo, LobbyGameSummary gameSummary)
            {
                List<long> accountIds = gameSummary.PlayerGameSummaryList.Select(pgs => pgs.AccountId).Distinct().ToList();
                List<MatchEntry> result = new List<MatchEntry>();
                foreach (long accountId in accountIds)
                {
                    PlayerGameSummary playerGameSummary = gameSummary.PlayerGameSummaryList.First(pgs => pgs.AccountId == accountId);
                    result.Add(new MatchEntry
                    {
                        AccountId = accountId,
                        Data = new PersistedCharacterMatchData
                        {
                            CreateDate = new DateTime(gameInfo.CreateTimestamp),
                            GameServerProcessCode = gameInfo.GameServerProcessCode,
                            MatchComponent = new MatchComponent(gameInfo, gameSummary, accountId),
                            MatchDetailsComponent = new MatchDetailsComponent(playerGameSummary),
                            MatchFreelancerStats = new MatchFreelancerStats(playerGameSummary),
                            SchemaVersion = new SchemaVersion<MatchSchemaChange>(),
                            UpdateDate = DateTime.UtcNow
                        }
                    });
                }

                return result;
            }
        }
    }
}