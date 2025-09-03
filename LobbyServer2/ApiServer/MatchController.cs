using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using Microsoft.AspNetCore.Http;

namespace CentralServer.ApiServer;

public static class MatchController
{
    public class MatchComponentResponseModel
    {
        public DateTime MatchTime { get; set; }
        public string Result { get; set; }
        public int Kills { get; set; }
        public string CharacterUsed { get; set; }
        public string GameType { get; set; }
        public string MapName { get; set; }
        public int TurnsPlayed { get; set; }
        public string GameMode { get; set; }
        public List<MatchActorResponseModel> Participants { get; set; } = new();
        
        public static MatchComponentResponseModel Of(MatchComponent match)
        {
            return new MatchComponentResponseModel
            {
                MatchTime = match.MatchTime,
                Result = match.Result.ToString(),
                Kills = match.Kills,
                CharacterUsed = match.CharacterUsed.ToString(),
                GameType = match.GameType.ToString(),
                MapName = match.MapName,
                TurnsPlayed = match.NumOfTurns,
                GameMode = match.GetSubTypeNameTerm(),
                Participants = match.Actors.Select(a => new MatchActorResponseModel
                {
                    Character = a.Character.ToString(),
                    Team = a.Team.ToString(),
                    IsPlayer = a.IsPlayer
                }).ToList()
            };
        }
    }

    public class MatchActorResponseModel
    {
        public string Character { get; set; }
        public string Team { get; set; }
        public bool IsPlayer { get; set; }
    }
    
    public class MatchDetailsResponseModel
    {
        public int Deaths { get; set; }
        public int Takedowns { get; set; }
        public int DamageDealt { get; set; }
        public int DamageTaken { get; set; }
        public int Healing { get; set; }
        public int Contribution { get; set; }
        public MatchResultsStatsModel MatchResults { get; set; }
        public int GroupSize { get; set; }
        public string Tier { get; set; }
        public float Points { get; set; }

        public static MatchDetailsResponseModel Of(MatchDetailsComponent details)
        {
            return new MatchDetailsResponseModel
            {
                Deaths = details.Deaths,
                Takedowns = details.Takedowns,
                DamageDealt = details.DamageDealt,
                DamageTaken = details.DamageTaken,
                Healing = details.Healing,
                Contribution = details.Contribution,
                MatchResults = MatchResultsStatsModel.Of(details.MatchResults),
                GroupSize = details.GroupSize,
                Tier = details.RankedTierLocTag,
                Points = details.RankedPoints
            };
        }

    }

    public class MatchResultsStatsModel
    {
        public List<TeamStatlineModel> FriendlyTeamStats { get; set; } = new();
        public List<TeamStatlineModel> EnemyTeamStats { get; set; } = new();
        public ScoreModel Score { get; set; } = new();
        public string VictoryCondition { get; set; }
        public int VictoryConditionTurns { get; set; }
        public float GameDuration { get; set; }

        public static MatchResultsStatsModel Of(MatchResultsStats stats)
        {
            if (stats == null)
            {
                return new MatchResultsStatsModel();
            }

            return new MatchResultsStatsModel
            {
                FriendlyTeamStats = stats.FriendlyStatlines?
                    .Select(TeamStatlineModel.Of)
                    .ToList() ?? new(),
                EnemyTeamStats = stats.EnemyStatlines?
                    .Select(TeamStatlineModel.Of)
                    .ToList() ?? new(),
                Score = new ScoreModel
                {
                    TeamAScore = stats.RedScore,
                    TeamBScore = stats.BlueScore
                },
                VictoryCondition = stats.VictoryCondition,
                VictoryConditionTurns = stats.VictoryConditionTurns,
                GameDuration = stats.GameTime
            };
        }
    }

    public class ScoreModel
    {
        public int TeamAScore { get; set; }
        public int TeamBScore { get; set; }
    }

    public class TeamStatlineModel
    {
        public PlayerIdentityModel Player { get; set; }
        public CharacterInfoModel Character { get; set; }
        public CombatStatsModel CombatStats { get; set; }
        public PerformanceStatsModel Performance { get; set; }
        public PlayerCustomizationModel Customization { get; set; }
        public List<int> AbilityMods { get; set; }
        public CatalystPhaseInfoModel UnusedCatalysts { get; set; }

        public static TeamStatlineModel Of(MatchResultsStatline statline)
        {
            if (statline == null)
            {
                return new TeamStatlineModel();
            }

            return new TeamStatlineModel
            {
                Player = new PlayerIdentityModel
                {
                    PlayerId = statline.PlayerId,
                    AccountId = statline.AccountID,
                    DisplayName = statline.DisplayName,
                    IsPerspectivePlayer = statline.IsPerspective,
                    IsAlly = statline.IsAlly,
                    PlayerType = GetPlayerType(statline)
                },
                Character = new CharacterInfoModel
                {
                    Type = statline.Character.ToString()
                },
                CombatStats = new CombatStatsModel
                {
                    Kills = statline.TotalPlayerKills,
                    Deaths = statline.TotalDeaths,
                    DamageDealt = statline.TotalPlayerDamage,
                    DamageTaken = statline.TotalPlayerDamageReceived,
                    Healing = statline.TotalPlayerHealingFromAbility,
                    Assists = statline.TotalPlayerAssists,
                    AbsorbedDamage = statline.TotalPlayerAbsorb
                },
                Performance = new PerformanceStatsModel
                {
                    TurnsPlayed = statline.TotalPlayerTurns,
                    AverageLockInTime = statline.TotalPlayerLockInTime,
                    ContributionScore = statline.TotalPlayerContribution
                },
                Customization = new PlayerCustomizationModel
                {
                    TitleId = statline.TitleID,
                    TitleLevel = statline.TitleLevel,
                    BannerId = statline.BannerID,
                    EmblemId = statline.EmblemID,
                    RibbonId = statline.RibbonID
                },
                AbilityMods = statline.AbilityEntries?.Select(entry => entry.AbilityModId).ToList() ?? new List<int>(),
                UnusedCatalysts = new CatalystPhaseInfoModel
                {
                    HasPrepPhase = statline.CatalystHasPrepPhase,
                    HasDashPhase = statline.CatalystHasDashPhase,
                    HasBlastPhase = statline.CatalystHasBlastPhase
                }
            };
        }

        private static string GetPlayerType(MatchResultsStatline statline)
        {
            if (statline.IsHumanControlled)
            {
                if (statline.HumanReplacedByBot)
                    return "ReplacedByBot";
                return "Human";
            }
            if (statline.IsBotMasqueradingAsHuman)
                return "BotMasqueradingAsHuman";
            return "Bot";
        }
    }

    public class PlayerIdentityModel
    {
        public int PlayerId { get; set; }
        public long AccountId { get; set; }
        public string DisplayName { get; set; }
        public bool IsPerspectivePlayer { get; set; }
        public bool IsAlly { get; set; }
        public string PlayerType { get; set; }
    }

    public class CharacterInfoModel
    {
        public string Type { get; set; }
    }

    public class CombatStatsModel
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int DamageDealt { get; set; }
        public int DamageTaken { get; set; }
        public int Healing { get; set; }
        public int Assists { get; set; }
        public int AbsorbedDamage { get; set; }
    }

    public class PerformanceStatsModel
    {
        public int TurnsPlayed { get; set; }
        public float AverageLockInTime { get; set; }
        public int ContributionScore { get; set; }
    }

    public class PlayerCustomizationModel
    {
        public int TitleId { get; set; }
        public int TitleLevel { get; set; }
        public int BannerId { get; set; }
        public int EmblemId { get; set; }
        public int RibbonId { get; set; }
    }

    public class CatalystPhaseInfoModel
    {
        public bool HasPrepPhase { get; set; }
        public bool HasDashPhase { get; set; }
        public bool HasBlastPhase { get; set; }
    }
    
    public class MatchFreelancerStatsResponseModel
    {
        public CharacterType CharacterType { get; set; }
        public float TotalAssists { get; set; }
        public float TotalDeaths { get; set; }
        public float TotalBadgePoints { get; set; }
        public float EnergyGainPerTurn { get; set; }
        public float DamagePerTurn { get; set; }
        public float DamageEfficiency { get; set; }
        public float KillParticipation { get; set; }
        public float SupportPerTurn { get; set; }
        public float DamageTakenPerTurn { get; set; }
        public float DamageDonePerLife { get; set; }
        public float MMR { get; set; }
        public float TeamMitigation { get; set; }
        public float TotalTurns { get; set; }

        public static MatchFreelancerStatsResponseModel Of(MatchFreelancerStats stats)
        {
            return new MatchFreelancerStatsResponseModel
            {
                CharacterType = stats.CharacterType,
                TotalAssists = stats.TotalAssists,
                TotalDeaths = stats.TotalDeaths,
                TotalBadgePoints = stats.TotalBadgePoints ?? 0,
                EnergyGainPerTurn = stats.EnergyGainPerTurn,
                DamagePerTurn = stats.DamagePerTurn ?? 0,
                DamageEfficiency = stats.DamageEfficiency,
                KillParticipation = stats.KillParticipation,
                SupportPerTurn = stats.SupportPerTurn ?? 0,
                DamageTakenPerTurn = stats.DamageTakenPerTurn ?? 0,
                DamageDonePerLife = stats.DamageDonePerLife ?? 0,
                MMR = stats.MMR ?? 0,
                TeamMitigation = stats.TeamMitigation ?? 0,
                TotalTurns = stats.TotalTurns
            };
        }
    }

    public class MatchDataResponseModel
    {
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public string GameServerProcessCode { get; set; }
        public MatchComponentResponseModel MatchComponent { get; set; }
        public MatchDetailsResponseModel MatchDetailsComponent { get; set; }
        public MatchFreelancerStatsResponseModel MatchFreelancerStats { get; set; }

        public static MatchDataResponseModel Of(PersistedCharacterMatchData data)
        {
            return new MatchDataResponseModel
            {
                CreateDate = data.CreateDate,
                UpdateDate = data.UpdateDate,
                GameServerProcessCode = data.GameServerProcessCode,
                MatchComponent = MatchComponentResponseModel.Of(data.MatchComponent),
                MatchDetailsComponent = MatchDetailsResponseModel.Of(data.MatchDetailsComponent),
                MatchFreelancerStats = MatchFreelancerStatsResponseModel.Of(data.MatchFreelancerStats)
            };
        }
    }
    
    public static IResult GetMatch(
        long accountId,
        string matchId,
        ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        MatchHistoryDao dao = DB.Get().MatchHistoryDao;
        PersistedCharacterMatchData match = dao.FindByProcessCode(accountId, matchId) ?? dao.FindByTimestamp(accountId, matchId);

        if (match == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(MatchDataResponseModel.Of(match));
    }

    public class MatchHistoryEntryModel
    {
        public string MatchId { get; set; }
        public DateTime MatchTime { get; set; }
        public string Character { get; set; }
        public string GameType { get; set; }
        public string SubType { get; set; }
        public string MapName { get; set; }
        public int NumOfTurns { get; set; }
        public int TeamAScore { get; set; }
        public int TeamBScore { get; set; }
        public string Team { get; set; }
        public string Result { get; set; }

        public static MatchHistoryEntryModel From(PersistedCharacterMatchData data)
        {
            return new MatchHistoryEntryModel
            {
                MatchId = data.GameServerProcessCode,
                MatchTime = data.MatchComponent.MatchTime,
                Character = data.MatchComponent.CharacterUsed.ToString(),
                GameType = data.MatchComponent.GameType.ToString(),
                SubType = data.MatchComponent.SubTypeLocTag,
                MapName = data.MatchComponent.MapName,
                NumOfTurns = data.MatchComponent.NumOfTurns,
                TeamAScore = data.MatchDetailsComponent.MatchResults.RedScore,
                TeamBScore = data.MatchDetailsComponent.MatchResults.BlueScore,
                Team = data.MatchComponent.Actors.Find(player => player.IsPlayer).Team.ToString(),
                Result = data.MatchComponent.Result.ToString()
            };
        }
    }

    public class MatchHistoryResponseModel
    {
        public List<MatchHistoryEntryModel> Matches { get; set; }
    }
    
    public static IResult GetMatchHistory(
        long accountId,
        long? after,
        long? before,
        int? limit,
        ClaimsPrincipal user)
    {
        if (!AdminController.ValidateAdmin(user, out IResult error, out long adminAccountId, out string adminHandle))
        {
            return error;
        }

        if (before is null == after is null)
        {
            return Results.BadRequest(new { message = "Either specify 'before' or 'after'" });
        }
        
        int finalLimit = limit ?? 100;

        DateTime time = DateTimeOffset.FromUnixTimeSeconds(after ?? (long)before).UtcDateTime;
        List<PersistedCharacterMatchData> matches = DB.Get()
            .MatchHistoryDao.Find(
                accountId,
                after is not null,
                time,
                finalLimit);

        return Results.Ok(
            new MatchHistoryResponseModel
            {
                Matches = matches.Select(MatchHistoryEntryModel.From).ToList()
            });
    }
}