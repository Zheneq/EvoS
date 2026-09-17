using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

public static class QueuePenaltyManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(QueuePenaltyManager));
    
    public static void IssueQueuePenalties(long accountId, Game game)
    {
        if (!LobbyConfiguration.GetMatchAbandoningPenalty() ||
            game.GameInfo?.GameConfig is null ||
            game.GameInfo.GameConfig.GameType != GameType.PvP ||
            (!game.IsDraft && game.GameInfo.GameResult == GameResult.NoResult))
        {
            return;
        }

        lock (game)
        {
            if (game.IsDraft && game.GameStatus <= GameStatus.Started)
            {
                //Left in Draft, punish harder, no leaving Draft cause they dont like the map or the Draft
                SetQueuePenalty(accountId, GameType.PvP, TimeSpan.FromMinutes(5), escalate: true);
                return;
            }
            int replacedWithBotsNum = game.TeamInfo.TeamPlayerInfo.Count(i => i.ReplacedWithBots);
            if (replacedWithBotsNum == game.TeamInfo.TeamPlayerInfo.Count)
            {
                CapQueuePenalties(game, presentPlayersOnly: false);
                return;
            }
            if (replacedWithBotsNum * 2 > game.TeamInfo.TeamPlayerInfo.Count)
            {
                return;
            }
            if (game.GameStatus != GameStatus.Stopped)
            {
                SetQueuePenalty(accountId, GameType.PvP, TimeSpan.FromSeconds(200), escalate: true);
            }
            else if (game.StopTime > DateTime.UtcNow)
            {
                SetQueuePenalty(accountId, GameType.PvP, DateTime.UtcNow.Subtract(game.StopTime).Add(TimeSpan.FromSeconds(30)), escalate: true);
            }
        }
    }

    public static void CapQueuePenalties(Game game, bool presentPlayersOnly = true)
    {
        IEnumerable<long> players = game.TeamInfo.TeamPlayerInfo
            .Where(p => !p.IsAIControlled && (!presentPlayersOnly || !p.ReplacedWithBots))
            .Select(p => p.AccountId)
            .Distinct();

        foreach (long accountId in players)
        {
            TimeSpan duration = TimeSpan.FromSeconds(15);
            LocalizationArg argDuration = LocalizationArg_TimeSpan.Create(duration);
            LocalizationPayload msg =
                LocalizationPayload.Create("QueueDodgerPenaltyAppliedToSelf", "Matchmaking", argDuration);
            if (SetQueuePenalty(accountId, GameType.PvP, duration, capPenalty: true))
            {
                log.Info($"{LobbyServerUtils.GetHandle(accountId)}'s queue penalty is pardoned (reset to {duration})");
                SessionManager.GetClientConnection(accountId)?.SendSystemMessage(msg);
            }
        }
    }

    public static bool ClearQueuePenalties(long accountId)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        if (account is null)
        {
            return false;
        }
        QueuePenalties penalties = account.AdminComponent.ActiveQueuePenalties?.GetValueOrDefault(GameType.PvP);
        if (penalties is null)
        {
            return true;
        }
        penalties.ResetQueueDodge();
        account.AdminComponent.ActiveQueuePenalties[GameType.PvP] = penalties;
        DB.Get().AccountDao.UpdateAdminComponent(account);
        log.Info($"{GameType.PvP} queue penalty cleared for {account.Handle}");
        return true;
    }

    internal readonly struct PenaltyEvaluation(
        bool apply,
        int count,
        DateTime blockTimeout,
        DateTime paroleTimeout,
        TimeSpan appliedSpan)
    {
        public bool Apply { get; } = apply;
        public int Count { get; } = count;
        public DateTime BlockTimeout { get; } = blockTimeout;
        public DateTime ParoleTimeout { get; } = paroleTimeout;
        public TimeSpan AppliedSpan { get; } = appliedSpan;
    }

    internal static PenaltyEvaluation EvaluatePenalty(
        QueuePenalties current,
        TimeSpan requestedSpan,
        DateTime now,
        bool overridePenalty,
        bool capPenalty,
        bool escalate,
        TimeSpan escalationCap,
        TimeSpan paroleWindow)
    {
        int count = current.QueueDodgeCount;
        TimeSpan span = requestedSpan;

        // Repeat-offender escalation: scale the penalty by 4^(count-1), clamped to the cap.
        // The count decays whenever the parole window has lapsed without a new offense.
        if (escalate)
        {
            if (current.QueueDodgeParoleTimeout != DateTime.MinValue
                && current.QueueDodgeParoleTimeout < now)
            {
                count = 0;
            }
            count += 1;
            double multiplier = Math.Pow(4, count - 1);
            span = TimeSpan.FromTicks((long)Math.Min(requestedSpan.Ticks * multiplier, escalationCap.Ticks));
        }

        DateTime newTimeout = now.Add(span);
        DateTime oldTimeout = current.QueueDodgeBlockTimeout;

        // Normal mode applies only if it raises the timeout; cap mode only if it lowers it;
        // overridePenalty bypasses the direction check.
        bool lowersTimeout = oldTimeout > newTimeout;
        if (oldTimeout == newTimeout || (capPenalty != lowersTimeout && !overridePenalty))
        {
            return new PenaltyEvaluation(false, current.QueueDodgeCount, oldTimeout, current.QueueDodgeParoleTimeout, span);
        }

        int resultCount = current.QueueDodgeCount;
        DateTime resultParole = current.QueueDodgeParoleTimeout;
        if (escalate)
        {
            resultCount = count;
            resultParole = now.Add(paroleWindow);
        }
        else if (capPenalty)
        {
            // A pardon forgives this incident's contribution to the escalation streak,
            // not just the current block (e.g. the player reconnected, or the game collapsed).
            resultCount = Math.Max(0, current.QueueDodgeCount - 1);
        }

        return new PenaltyEvaluation(true, resultCount, newTimeout, resultParole, span);
    }

    internal static bool IsQueueBlocked(QueuePenalties penalties, DateTime now)
    {
        return penalties is not null
               && penalties.QueueDodgeBlockTimeout > now.Add(TimeSpan.FromSeconds(1));
    }

    private static bool SetQueuePenalty(
        long accountId,
        GameType gameType,
        TimeSpan timeSpan,
        bool overridePenalty = false,
        bool capPenalty = false,
        bool escalate = false)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        if (account is null)
        {
            return false;
        }
        account.AdminComponent.ActiveQueuePenalties ??= new Dictionary<GameType, QueuePenalties>();
        QueuePenalties penalties = account.AdminComponent.ActiveQueuePenalties.GetValueOrDefault(gameType);
        DateTime referenceDateTime = DateTime.UtcNow;
        if (penalties is null)
        {
            penalties = new QueuePenalties();
            penalties.ResetQueueDodge();
        }

        PenaltyEvaluation eval = EvaluatePenalty(
            penalties,
            timeSpan,
            referenceDateTime,
            overridePenalty,
            capPenalty,
            escalate,
            LobbyConfiguration.GetQueuePenaltyEscalationCap(),
            LobbyConfiguration.GetQueuePenaltyParoleWindow());
        if (!eval.Apply)
        {
            return false;
        }

        DateTime oldTimeout = penalties.QueueDodgeBlockTimeout;
        penalties.QueueDodgeCount = eval.Count;
        penalties.QueueDodgeParoleTimeout = eval.ParoleTimeout;
        penalties.QueueDodgeBlockTimeout = eval.BlockTimeout;
        log.Info($"{gameType} queue penalty for {account.Handle}: {eval.AppliedSpan}"
                 + (escalate ? $" (offense #{eval.Count})" : "")
                 + (oldTimeout > referenceDateTime ? $" (was {oldTimeout.Subtract(referenceDateTime)})" : ""));

        account.AdminComponent.ActiveQueuePenalties[gameType] = penalties;
        DB.Get().AccountDao.UpdateAdminComponent(account);

        GroupInfo playerGroup = GroupManager.GetPlayerGroup(accountId);
        if (playerGroup is not null)
        {
            MatchmakingManager.RemoveGroupFromQueue(playerGroup, true);
        }

        return true;
    }

    public static LocalizationPayload CheckQueuePenalties(long accountId, GameType selectedGameType, long requestedBy = 0)
    {
        if (!LobbyConfiguration.GetMatchAbandoningPenalty())
        {
            return null;
        }
        if (requestedBy == 0) requestedBy = accountId;

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        QueuePenalties queuePenalties = account?.AdminComponent.ActiveQueuePenalties?.GetValueOrDefault(selectedGameType);
        if (IsQueueBlocked(queuePenalties, DateTime.UtcNow))
        {
            TimeSpan duration = queuePenalties.QueueDodgeBlockTimeout.Subtract(DateTime.UtcNow);
            LocalizationArg argDuration = LocalizationArg_TimeSpan.Create(duration);
            LocalizationPayload failure = accountId == requestedBy
                ? MakeSelfBlockedMessage(argDuration)
                : MakeGroupmateBlockedMessage(accountId, argDuration);
            log.Info($"{account.Handle} cannot join {selectedGameType} queue until {queuePenalties.QueueDodgeBlockTimeout}");
            
            GroupInfo group = GroupManager.GetPlayerGroup(accountId);
            if (group != null)
            {
                foreach (long groupMember in group.Members)
                {
                    if (groupMember == requestedBy) continue;
                    LobbyServerProtocol conn = SessionManager.GetClientConnection(groupMember);
                    LocalizationPayload localizationPayload = accountId != groupMember
                        ? MakeGroupmateBlockedMessage(account.AccountId, argDuration)
                        : MakeSelfBlockedMessage(argDuration);
                    
                    conn?.SendSystemMessage(localizationPayload);
                }
            }
            
            // QueueDodgerPenaltyAppliedToSelf@Matchmaking,Text,0: timespan,,"Because you left the previous game after a match was found, you will not be allowed to queue for {0}."
            // QueueDodgerPenaltyAppliedToGroupmate@Matchmaking,Text,"0: playername, 1: timespan",,{0} left their previous game after a match was found. They cannot re-queue for {1}.
            // QueueDodgePenaltyBlocksQueueEntry@Matchmaking,Text,0: playername,,{0} is currently blocked from queueing because they left a recent game after a match was found.
            // QueueDodgerPenaltyCleared@Matchmaking,Text,0: gametype,,You have been cleared of any penalty for dodging a {0} game.
            // LeftTooManyActiveGamesToQueue@Matchmaking,Text,0: playername,,{0} has left too many games recently to be allowed to queue.
            // AllGroupMembersHaveLeftTooManyActiveGamesToQueue@Matchmaking,Text,,,At least one group member must eliminate their Leaver penalty.
            // CannotQueueUntilTimeout@Ranked,Text,0: datetime,,You cannot queue for this mode until {0} due to queue dodging.
            // CannotQueueMembersPenalized@Ranked,Text,0: members banned,,"You cannot queue for this mode because {0} have been penalized"
            // UnableToQueue@RankMode,Text,,,Unable To Queue

            return failure;
        }

        return null;
    }

    private static LocalizationPayload MakeSelfBlockedMessage(LocalizationArg argDuration)
    {
        return LocalizationPayload.Create(
            "QueueDodgerPenaltyAppliedToSelf",
            "Matchmaking",
            argDuration);
    }

    public static LocalizationPayload MakeGroupmateBlockedMessage(long accountId, LocalizationArg argDuration)
    {
        return LocalizationPayload.Create(
            "QueueDodgerPenaltyAppliedToGroupmate",
            "Matchmaking",
            LocalizationArg_Handle.Create(LobbyServerUtils.GetHandle(accountId)),
            argDuration);
    }
}