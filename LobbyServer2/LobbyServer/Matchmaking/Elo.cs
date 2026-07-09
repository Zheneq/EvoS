using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public static class Elo
{
    // TODO if asymm/4lancer elo is empty, initialize it for corresponding solo
    private static readonly ILog log = LogManager.GetLogger(typeof(Elo));
    private static readonly Lock EloLock = new();
    
    public static void OnGameEnded(
        LobbyGameInfo gameInfo,
        LobbyGameSummary gameSummary,
        GameSubType gameSubType,
        List<MatchPlayerData> players,
        MatchmakingConfiguration conf,
        DateTime now,
        IAccountProvider accountProvider,
        IMatchHistoryProvider matchHistoryProvider,
        IAccountUpdater accountUpdater)
    {
        if (gameSummary is null
            || gameSummary.GameResult != GameResult.TeamAWon && gameSummary.GameResult != GameResult.TeamBWon
            || gameInfo.GameConfig.GameType != GameType.PvP)
        {
            return;
        }
        
        // TODO proper eloKey for 4lancer
        
        Dictionary<long, MatchPlayerData> matchPlayerDatas = players.ToDictionary(p => p.AccountId);
        List<MatchPlayerData> teamA = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamA())
            .DistinctBy(pgs => pgs.AccountId)
            .Select(pgs => matchPlayerDatas[pgs.AccountId])
            .ToList();
        List<MatchPlayerData> teamB = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamB())
            .DistinctBy(pgs => pgs.AccountId)
            .Select(pgs => matchPlayerDatas[pgs.AccountId])
            .ToList();
        
        log.Info($"Game {gameInfo.Name} ended, " +
                 $"{string.Join(", ", teamA.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamAWon ? "won" : "lost")}, " +
                 $"{string.Join(", ", teamB.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamBWon ? "won" : "lost")}");

        lock (EloLock)
        {
            foreach (MatchPlayerData data in teamA.Concat(teamB))
            {
                UpdateConfidence(data, gameInfo.GameConfig.GameType, data.EloKey, conf, now);
            }
            int result = gameSummary.GameResult == GameResult.TeamAWon ? 1 : 0;
            float eloChange = GetEloChange(teamA, teamB, conf, result);
            AwardEloTeam(teamA, conf, eloChange, accountUpdater);
            AwardEloTeam(teamB, conf, -eloChange, accountUpdater);
        }
    }

    private static void UpdateConfidence(
        MatchPlayerData player,
        GameType gameType,
        string eloKey,
        MatchmakingConfiguration conf,
        DateTime now)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(player.AccountId);
        List<PersistedCharacterMatchData> matches = DB.Get().MatchHistoryDao.Find(player.AccountId);
        PersistedCharacterMatchData lastMatch = matches
            .FirstOrDefault(m => m.MatchComponent.GameType == gameType); // TODO also check SubTypeLocTag?

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

            int eloConfidenceLevel = player.GetEloConfidenceLevel();
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

        int currentConfLevel = player.GetEloConfidenceLevel();
        log.Info($"Updating {player.Handle}'s {eloKey} elo confidence level " +
                 $"{currentConfLevel} -> {Math.Max(0, currentConfLevel + confidenceLevelDelta)}");
        
        account.ExperienceComponent.EloValues.ApplyDelta(eloKey, 0, confidenceLevelDelta);
        DB.Get().AccountDao.UpdateExperienceComponent(account);
    }

    public static float GetTeamElo(List<MatchPlayerData> team)
    {
        return team.Select(p => p.GetElo() * p.NumControlledCharacters).Sum()
               / team.Select(p => p.NumControlledCharacters).Sum();
    }

    private static float GetEloChange(
        List<MatchPlayerData> teamA,
        List<MatchPlayerData> teamB,
        MatchmakingConfiguration conf,
        int result)
    {
        float k = conf.EloBasePot * (teamA.Select(p => GetEloConfidenceFactor(p, conf)).Sum() / (2 * teamA.Count) +
                                  teamB.Select(p => GetEloConfidenceFactor(p, conf)).Sum() / (2 * teamB.Count));
        return k * (result - GetPrediction(teamA, teamB));
    }

    private static float GetPrediction(List<MatchPlayerData> teamA, List<MatchPlayerData> teamB)
    {
        return GetPrediction(GetTeamElo(teamA), GetTeamElo(teamB));
    }

    public static float GetPrediction(float teamAElo, float teamBElo)
    {
        return 1.0f / (1 + MathF.Pow(10, (teamBElo - teamAElo) / 400.0f));
    }

    private static float GetEloConfidenceFactor(MatchPlayerData data, MatchmakingConfiguration conf)
    {
        int cf = data.GetEloConfidenceLevel();
        return conf.EloConfidenceFactor[Math.Clamp(cf, 0, conf.EloConfidenceFactor.Count-1)];
    }

    private static void AwardElo(MatchPlayerData data, string eloKey, float delta, IAccountUpdater accountUpdater)
    {
        float currentElo = data.GetElo();
        log.Info($"Updating {data.Handle}'s {eloKey} elo {currentElo} -> {currentElo + delta}");
        var acc = DB.Get().AccountDao.GetAccount(data.AccountId);
        acc.ExperienceComponent.EloValues.ApplyDelta(eloKey, delta, 0);
        accountUpdater(acc);
    }

    private static void AwardEloTeam(List<MatchPlayerData> team, MatchmakingConfiguration conf, float eloDelta, IAccountUpdater accountUpdater)
    {
        float avgConf = team.Select(p => GetEloConfidenceFactor(p, conf)).Sum() / team.Count;
        foreach (MatchPlayerData data in team)
        {
            AwardElo(data, data.EloKey, eloDelta * GetEloConfidenceFactor(data, conf) * data.NumControlledCharacters / avgConf, accountUpdater);
        }
    }
}