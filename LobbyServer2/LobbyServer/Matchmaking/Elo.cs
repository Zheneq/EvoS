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
        GameSubType gameSubType,
        string eloKey, // TODO move eloKey into conf
        MatchmakingConfiguration conf,
        DateTime now,
        IAccountProvider accountProvider,
        IMatchHistoryProvider matchHistoryProvider,
        IAccountUpdater accountUpdater,
        IAsymmetricEloCalculator asymmetricCalculator = null,
        Dictionary<long, int> asymmetricSlots = null,
        Dictionary<long, string> playerEloKeys = null)
    {
        if (gameSummary is null
            || gameSummary.GameResult != GameResult.TeamAWon && gameSummary.GameResult != GameResult.TeamBWon
            || gameInfo.GameConfig.GameType != GameType.PvP)
        {
            return;
        }
        
        if (gameSubType is null
            || (gameSubType.Mods.Contains(GameSubType.SubTypeMods.ControlAllBots) && asymmetricCalculator == null))
        {
            log.Info($"{gameInfo.GameServerProcessCode} was a fourlancer game, not updating elo");
            return;
        }
        
        List<PersistedAccountData> teamA = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamA())
            .DistinctBy(pgs => pgs.AccountId)
            .Select(pgs => accountProvider(pgs.AccountId))
            .ToList();
        List<PersistedAccountData> teamB = gameSummary.PlayerGameSummaryList
            .Where(pgs => pgs.IsInTeamB())
            .DistinctBy(pgs => pgs.AccountId)
            .Select(pgs => accountProvider(pgs.AccountId))
            .ToList();
        
        log.Info($"Game {gameInfo.Name} ended, " +
                 $"{string.Join(", ", teamA.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamAWon ? "won" : "lost")}, " +
                 $"{string.Join(", ", teamB.Select(acc => acc.Handle))} {(gameSummary.GameResult == GameResult.TeamBWon ? "won" : "lost")}");
        
        lock (EloLock)
        {
            foreach (PersistedAccountData acc in teamA.Concat(teamB))
            {
                string key = playerEloKeys?.GetValueOrDefault(acc.AccountId) ?? eloKey;
                UpdateConfidence(acc, gameInfo.GameConfig.GameType, key, conf, now, matchHistoryProvider);
            }
            int result = gameSummary.GameResult == GameResult.TeamAWon ? 1 : 0;
            float eloChange = asymmetricCalculator != null
                ? asymmetricCalculator.CalculateEloChange(teamA, teamB, asymmetricSlots ?? new Dictionary<long, int>(), playerEloKeys ?? new Dictionary<long, string>(), eloKey, conf, result)
                : GetEloChange(teamA, teamB, eloKey, conf, result);
            AwardEloTeam(teamA, eloKey, playerEloKeys, conf, eloChange, accountUpdater);
            AwardEloTeam(teamB, eloKey, playerEloKeys, conf, -eloChange, accountUpdater);
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

    private static void AwardElo(PersistedAccountData acc, string eloKey, float delta, IAccountUpdater accountUpdater)
    {
        float currentElo = GetElo(acc, eloKey);
        log.Info($"Updating {acc.Handle}'s {eloKey} elo {currentElo} -> {currentElo + delta}");
        acc.ExperienceComponent.EloValues.ApplyDelta(eloKey, delta, 0);
        accountUpdater(acc);
    }

    private static void AwardEloTeam(List<PersistedAccountData> team, string baseEloKey, Dictionary<long, string> playerEloKeys, MatchmakingConfiguration conf, float eloDelta, IAccountUpdater accountUpdater)
    {
        // Confidence factor normalization always uses the base key so all players are on the same scale
        // TODO is this ok?
        float avgConf = team.Select(p => GetEloConfidenceFactor(p, baseEloKey, conf)).Sum() / team.Count;
        foreach (PersistedAccountData acc in team)
        {
            string key = playerEloKeys?.GetValueOrDefault(acc.AccountId) ?? baseEloKey;
            AwardElo(acc, key, eloDelta * GetEloConfidenceFactor(acc, baseEloKey, conf) / avgConf, accountUpdater);
        }
    }
}