using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public class MatchmakerRanked : MatchmakerBase
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Matchmaker));

    
    private readonly Func<MatchmakingConfiguration> _conf;
    protected MatchmakingConfiguration Conf => _conf();
    
    private readonly Random rand = new Random();
    
    public MatchmakerRanked(
        AccountDao accountDao,
        GameType gameType,
        GameSubType subType,
        string eloKey,
        Func<MatchmakingConfiguration> conf)
        : base(accountDao, gameType, subType, eloKey)
    {
        _conf = conf;
    }
    
    public MatchmakerRanked(
        GameType gameType,
        GameSubType subType,
        string eloKey,
        Func<MatchmakingConfiguration> conf)
        :this(DB.Get().AccountDao, gameType, subType, eloKey, conf)
    {
    }

    protected override bool IgnoreFiltering(List<MatchmakingGroup> queuedGroups, DateTime now)
    {
        double waitTime = Math.Sqrt(queuedGroups.Select(g => Math.Pow((now - g.QueueTime).TotalSeconds, 2)).Average());
        return waitTime > Conf.FallbackTime.TotalSeconds;
    }

    protected override bool FilterMatch(Match match, DateTime now)
    {
        double waitingTime = GetReferenceTime(match, now);
        int maxEloDiff = Conf.MaxTeamEloDifferenceStart +
                         Convert.ToInt32((Conf.MaxTeamEloDifference - Conf.MaxTeamEloDifferenceStart)
                                         * Math.Clamp(waitingTime / Conf.MaxTeamEloDifferenceWaitTime.TotalSeconds, 0, 1));

        float eloDiff = Math.Abs(match.TeamA.Elo - match.TeamB.Elo);
        bool result = eloDiff <= maxEloDiff;
        return result;
    }

    private static double GetReferenceTime(Match match, DateTime now)
    {
        int cutoff = int.Max(1, Convert.ToInt32(MathF.Floor(match.Groups.Count() / 2.0f))); // don't want to keep the first ones to queue waiting for too long
        double waitingTime = match.Groups
            .Select(g => (now - g.QueueTime).TotalSeconds)
            .Order()
            .TakeLast(cutoff)
            .Average();
        return waitingTime;
    }

    public override List<ScoredMatch> GetMatchesRanked(List<MatchmakingGroup> queuedGroups, DateTime now)
    {
        return base.GetMatchesRanked(queuedGroups.Take(12).ToList(), now);
    }

    protected override ScoredMatch RankMatch(Match match, DateTime now)
    {
        float teamEloDifferenceFactor = 1 - Cap(Math.Abs(match.TeamA.Elo - match.TeamB.Elo) / Conf.MaxTeamEloDifference);
        float teammateEloDifferenceAFactor = 1 - Cap((match.TeamA.MaxElo - match.TeamA.MinElo) / Conf.TeammateEloDifferenceWeightCap);
        float teammateEloDifferenceBFactor = 1 - Cap((match.TeamB.MaxElo - match.TeamB.MinElo) / Conf.TeammateEloDifferenceWeightCap);
        float teammateEloDifferenceFactor = (teammateEloDifferenceAFactor + teammateEloDifferenceBFactor) * 0.5f;
        double waitTime = Math.Sqrt(match.Groups.Select(g => Math.Pow((now - g.QueueTime).TotalSeconds, 2)).Average());
        float waitTimeFactor = Cap((float)(waitTime / Conf.WaitingTimeWeightCap.TotalSeconds));
        // TODO team composition does not matter for ranked
        float teamCompositionFactor = (GetTeamCompositionFactor(match.TeamA) + GetTeamCompositionFactor(match.TeamB)) * 0.5f;
        float teamBlockFactor = (GetBlocksFactor(match.TeamA) + GetBlocksFactor(match.TeamB)) * 0.5f;
        float teamConfidenceBalanceFactor = GetTeamConfidenceBalanceFactor(match);
        float tieBreakerFactor = GetTieBreakerFactor(match);
        
        // TODO balance max - min elo in the team
        // TODO recently canceled matches factor
        // TODO match-to-match variance factor
        // TODO accumulated wait time (time spent in queue for the last hour?)
        // TODO win history factor (too many losses - try not to put into a disadvantaged team)?
        // TODO non-linearity?
        
        // TODO if you are waiting for 20 minutes, you must be in the next game
        // TODO overrides like "we must have this player in the next match" & "we must start next match by specific time"

        float teamEloDifferenceFactorWeighted = teamEloDifferenceFactor * Conf.TeamEloDifferenceWeight;
        float teammateEloDifferenceFactorWeighted = teammateEloDifferenceFactor * Conf.TeammateEloDifferenceWeight;
        float waitTimeFactorWeighted = waitTimeFactor * Conf.WaitingTimeWeight;
        float teamCompositionFactorWeighted = teamCompositionFactor * Conf.TeamCompositionWeight;
        float teamBlockFactorWeighted = teamBlockFactor * Conf.TeamBlockWeight;
        float teamConfidenceBalanceFactorWeighted = teamConfidenceBalanceFactor * Conf.TeamConfidenceBalanceWeight;
        float tieBreakerFactorWeighted = tieBreakerFactor * Conf.TieBreakerWeight;
        float score =
            teamEloDifferenceFactorWeighted
            + teammateEloDifferenceFactorWeighted
            + waitTimeFactorWeighted
            + teamCompositionFactorWeighted
            + teamBlockFactorWeighted
            + teamConfidenceBalanceFactorWeighted
            + tieBreakerFactorWeighted;
        
        string description = $"Score {score:0.00} " +
                  $"(tElo:{teamEloDifferenceFactorWeighted:0.00} [{teamEloDifferenceFactor:0.00}], " +
                  $"tmElo:{teammateEloDifferenceFactorWeighted:0.00} [{teammateEloDifferenceFactor:0.00}], " +
                  $"q:{waitTimeFactorWeighted:0.00} [{waitTimeFactor:0.00}], " +
                  $"tComp:{teamCompositionFactorWeighted:0.00} [{teamCompositionFactor:0.00}], " +
                  $"blocks:{teamBlockFactorWeighted:0.00} [{teamBlockFactor:0.00}], " +
                  $"tConf:{teamConfidenceBalanceFactorWeighted:0.00} [{teamConfidenceBalanceFactor:0.00}], " +
                  $"tieBr:{tieBreakerFactorWeighted:0.00} [{tieBreakerFactor:0.00}]" +
                  $") {match}";

        return new ScoredMatch(match, score, description);
    }

    private static float Cap(float factor)
    {
        return Math.Clamp(factor, 0, 1);
    }

    private static float GetTeamCompositionFactor(Match.Team team)
    {
        if (team.Groups.Count == 1)
        {
            return 1;
        }
        
        float score = 0;
        Dictionary<CharacterRole,int> roles = team.Accounts.Values
            .Select(acc => acc.AccountComponent.LastCharacter)
            .Select(ch => CharacterConfigs.Characters[ch].CharacterRole)
            .GroupBy(role => role)
            .ToDictionary(el => el.Key, el => el.Count());
        
        if (roles.ContainsKey(CharacterRole.Tank))
        {
            score += 0.3f;
        }
        if (roles.ContainsKey(CharacterRole.Support))
        {
            score += 0.3f;
        }
        if (roles.TryGetValue(CharacterRole.Assassin, out int flNum))
        {
            score += 0.2f * Math.Min(flNum, 2);
        }
        if (roles.TryGetValue(CharacterRole.None, out int fillNum))
        {
            score += 0.27f * Math.Min(fillNum, 4);
        }

        return Math.Min(score, 1);
    }

    private static float GetBlocksFactor(Match.Team team)
    {
        if (team.Groups.Count == 1)
        {
            return 1;
        }
        
        int totalBlocks = team.Accounts.Values
            .Select(acc => team.AccountIds.Count(accId => acc.SocialComponent.BlockedAccounts.Contains(accId)))
            .Sum();

        return 1 - Math.Min(totalBlocks * 0.125f, 1);
    }

    private float GetTeamConfidenceBalanceFactor(Match match)
    {
        int diff = Math.Abs(match.TeamA.Accounts.Values.Select(GetEloConfidenceLevel).Sum()
                            - match.TeamB.Accounts.Values.Select(GetEloConfidenceLevel).Sum());
        return 1 - Cap(diff * 0.33f);
    }

    private int GetEloConfidenceLevel(PersistedAccountData acc)
    {
        acc.ExperienceComponent.EloValues.GetElo(_eloKey, out _, out int eloConfLevel);
        return eloConfLevel;
    }

    private float GetTieBreakerFactor(Match match)
    {
        return rand.NextSingle();
    }
}