using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakerFifo : MatchmakerBase
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MatchmakerFifo));

    private const float Score = 1000f;
    
    public MatchmakerFifo(
        AccountDao accountDao,
        GameType gameType,
        GameSubType subType,
        string eloKey)
        : base(accountDao, gameType, subType, eloKey)
    {
    }
    
    public MatchmakerFifo(
        GameType gameType,
        GameSubType subType)
        : base(DB.Get().AccountDao, gameType, subType, string.Empty)
    {
    }
        
    public override List<ScoredMatch> GetMatchesRanked(List<MatchmakingGroup> queuedGroups, DateTime now)
    {
        if (queuedGroups.Count == 0)
        {
            return new();
        }

        Match bestMatch = FindMatches(queuedGroups.OrderBy(g => g.QueueTime).ToList()).FirstOrDefault();
        if (bestMatch is null)
        {
            return new();
        }
        
        log.Info($"Best match: {bestMatch}");
        return new() { new ScoredMatch(bestMatch, Score, "first one") };
    }
}