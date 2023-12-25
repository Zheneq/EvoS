using System;
using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public static class Elo
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Elo));
    private static readonly object EloLock = new();
    
    public static void OnGameEnded(
        LobbyGameInfo gameInfo,
        LobbyGameSummary gameSummary,
        string eloKey, // TODO move eloKey into conf
        MatchmakingConfiguration conf,
        DateTime now,
        IAccountProvider accountProvider,
        IMatchHistoryProvider matchHistoryProvider)
    {
        if (gameSummary is null
            || gameSummary.GameResult != GameResult.TeamAWon && gameSummary.GameResult != GameResult.TeamBWon)
        {
            return;
        }
        
        List<PersistedAccountData> teamA = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamA())
            .Select(pgs => accountProvider(pgs.AccountId))
            .ToList();
        List<PersistedAccountData> teamB = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamB())
            .Select(pgs => accountProvider(pgs.AccountId))
            .ToList();
        
        log.Info($"Game {gameInfo.Name} ended, " +
                 $"{string.Join(", ", teamA.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamAWon ? "won" : "lost")}, " +
                 $"{string.Join(", ", teamB.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamBWon ? "won" : "lost")}");
        
        lock (EloLock)
        {
            foreach (PersistedAccountData acc in teamA.Concat(teamB))
            {
                UpdateConfidence(acc, gameInfo.GameConfig.GameType, eloKey, conf, now, matchHistoryProvider);
            }
            float eloChange = GetEloChange(teamA, teamB, eloKey, conf, gameSummary.GameResult == GameResult.TeamAWon ? 1 : 0);
            AwardEloTeam(teamA, eloKey, conf, eloChange);
            AwardEloTeam(teamB, eloKey, conf, -eloChange);
        }
    }

    private static void UpdateConfidence(
        PersistedAccountData player,
        GameType gameType,
        string eloKey,
        MatchmakingConfiguration conf,
        DateTime now,
        IMatchHistoryProvider matchHistoryProvider)
    {
        List<PersistedCharacterMatchData> matches = matchHistoryProvider(player.AccountId);
        PersistedCharacterMatchData lastMatch = matches
            .FirstOrDefault(m => m.MatchComponent.GameType == gameType);

        int confidenceLevelDelta = -100;
        if (lastMatch is not null)
        {
            confidenceLevelDelta = 0;
            TimeSpan timeSinceLastMatch = now - lastMatch.MatchComponent.MatchTime;
            for (int i = conf.EloConfidenceRetention.Count - 1; i >= 0; i--)
            {
                if (timeSinceLastMatch > conf.EloConfidenceRetention[i])
                {
                    confidenceLevelDelta = -i;
                    break;
                }
            }

            int eloConfidenceLevel = GetEloConfidenceLevel(player, eloKey);
            if (eloConfidenceLevel < conf.EloConfidenceUpgrade.Count)
            {
                int num = matches
                    .Count(m => m.MatchComponent.GameType == gameType
                                && now - lastMatch.MatchComponent.MatchTime < conf.EloConfidenceRetention[0]);
                if (num >= conf.EloConfidenceUpgrade[eloConfidenceLevel])
                {
                    confidenceLevelDelta = 1;
                }
            }
        }

        int currentConfLevel = GetEloConfidenceLevel(player, eloKey);
        log.Info($"Updating {player.Handle}'s {eloKey} elo confidence level " +
                 $"{currentConfLevel} -> {Math.Max(0, currentConfLevel + confidenceLevelDelta)}");
        
        player.ExperienceComponent.EloValues.ApplyDelta(eloKey, 0, confidenceLevelDelta);
    }

    private static float GetTeamElo(List<PersistedAccountData> team, string eloKey)
    {
        return team.Select(p => GetElo(p, eloKey)).Sum() / team.Count;
    }

    private static float GetEloChange(
        List<PersistedAccountData> teamA,
        List<PersistedAccountData> teamB,
        string eloKey,
        MatchmakingConfiguration conf,
        int result)
    {

        float k = conf.EloBasePot * (teamA.Select(p => GetEloConfidenceFactor(p, eloKey, conf)).Sum() / (2 * teamA.Count) +
                                  teamB.Select(p => GetEloConfidenceFactor(p, eloKey, conf)).Sum() / (2 * teamB.Count));
        return k * (result - GetPrediction(eloKey, teamA, teamB));
    }

    private static float GetPrediction(string eloKey, List<PersistedAccountData> teamA, List<PersistedAccountData> teamB)
    {
        return GetPrediction(GetTeamElo(teamA, eloKey), GetTeamElo(teamB, eloKey));
    }

    public static float GetPrediction(float teamAElo, float teamBElo)
    {
        return 1.0f / (1 + MathF.Pow(10, (teamBElo - teamAElo) / 400.0f));
    }
    
    private static float GetElo(PersistedAccountData acc, string eloKey)
    {
        acc.ExperienceComponent.EloValues.GetElo(eloKey, out float elo, out _);
        return elo;
    }
    
    private static float GetEloConfidenceFactor(PersistedAccountData acc, string eloKey, MatchmakingConfiguration conf)
    {
        int cf = GetEloConfidenceLevel(acc, eloKey);
        return conf.EloConfidenceFactor[Math.Clamp(cf, 0, conf.EloConfidenceFactor.Count-1)];
    }

    private static int GetEloConfidenceLevel(PersistedAccountData acc, string eloKey)
    {
        acc.ExperienceComponent.EloValues.GetElo(eloKey, out _, out int cf);
        return Math.Max(cf, 0);
    }

    private static void AwardElo(PersistedAccountData acc, string eloKey, float delta)
    {
        float currentElo = GetElo(acc, eloKey);
        log.Info($"Updating {acc.Handle}'s {eloKey} elo {currentElo} -> {currentElo + delta}");
        acc.ExperienceComponent.EloValues.ApplyDelta(eloKey, delta, 0);
    }

    private static void AwardEloTeam(List<PersistedAccountData> team, string eloKey, MatchmakingConfiguration conf, float eloDelta)
    {
        float avgConf = team.Select(p => GetEloConfidenceFactor(p, eloKey, conf)).Sum() / team.Count;
        foreach (PersistedAccountData acc in team)
        {
            AwardElo(acc, eloKey, eloDelta * GetEloConfidenceFactor(acc, eloKey, conf) / avgConf);
        }
    }
}