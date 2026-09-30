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
            game.GameInfo.GameConfig.GameType != GameType.PvP)
        {
            return;
        }

        lock (game)
        {
            // Only leaving a started match is penalized, plus leaving the draft itself in Draft.
            // Dropping while loading is way more likely to be a technical issue than malice.
            bool leftDraft = game.IsDraft && game.GameStatus is >= GameStatus.FreelancerSelecting and <= GameStatus.Launching;
            if (!game.MatchStarted && !leftDraft)
            {
                return;
            }
            bool draftInProgress = game.IsDraft && game.GameStatus <= GameStatus.Started;
            
            // Everyone leaving a running match, suggesting the game has broken rather than been abandoned
            if (game.GameStatus != GameStatus.Stopped
                && game.TeamInfo.TeamPlayerInfo.Where(IsHumanPlayer).All(p => p.ReplacedWithBots))
            {
                PardonQueuePenalties(game, presentPlayersOnly: false);
                return;
            }
            
            // Once enough players have left, the game has collapsed and further leavers are not to blame
            int alreadyReplacedNum = game.TeamInfo.TeamPlayerInfo.Count(p => p.ReplacedWithBots && p.AccountId != accountId);
            if (alreadyReplacedNum >= LobbyConfiguration.GetQueuePenaltyCollapseThreshold())
            {
                return;
            }
            if (draftInProgress)
            {
                //Left in Draft, punish harder, no leaving Draft cause they dont like the map or the Draft
                SetQueuePenalty(accountId, GameType.PvP, LobbyConfiguration.GetQueuePenaltyDraftBaseDuration(), escalate: true);
                game.PenalizedPlayers.Add(accountId);
                return;
            }
            if (game.GameStatus != GameStatus.Stopped)
            {
                SetQueuePenalty(accountId, GameType.PvP, LobbyConfiguration.GetQueuePenaltyPvPBaseDuration(), escalate: true);
                game.PenalizedPlayers.Add(accountId);
            }
            else if (game.HasResult && game.StopTime > DateTime.UtcNow)
            {
                SetQueuePenalty(accountId, GameType.PvP, DateTime.UtcNow.Subtract(game.StopTime).Add(TimeSpan.FromSeconds(30)), escalate: false);
            }
        }
    }

    public static void OnGameEnded(Game game)
    {
        // A canceled match has no result, but whoever caused the cancellation is to blame for that,
        // so only started matches count
        if (!game.MatchStarted)
        {
            return;
        }

        if (!game.HasResult)
        {
            // Leaving a game that ended without a result is not penalized
            PardonQueuePenalties(game, presentPlayersOnly: false);
        }
        else
        {
            // Leavers who came back and stayed until the end are forgiven
            PardonQueuePenalties(game, presentPlayersOnly: true);
        }
    }

    // A human player's own slot: not a bot, and not a character remote-controlled by another player.
    // IsAIControlled/IsHumanControlled can't be used, as they also account for ReplacedWithBots.
    private static bool IsHumanPlayer(LobbyServerPlayerInfo player)
    {
        return !player.IsNPCBot && !player.IsLoadTestBot && !player.IsRemoteControlled && !player.IsSpectator;
    }

    // Forgives players penalized for leaving this game: the offense no longer counts towards escalation,
    // and the block is cut down to a few seconds. Each penalty is pardoned at most once.
    public static void PardonQueuePenalties(Game game, bool presentPlayersOnly = true)
    {
        lock (game)
        {
            List<long> players = game.PenalizedPlayers
                .Where(accountId => !presentPlayersOnly || game.GetPlayerInfo(accountId)?.ReplacedWithBots == false)
                .ToList();

            foreach (long accountId in players)
            {
                game.PenalizedPlayers.Remove(accountId);
                log.Info($"{LobbyServerUtils.GetHandle(accountId)}'s queue penalty for leaving {game.ProcessCode} is pardoned");
                TimeSpan duration = TimeSpan.FromSeconds(15);
                if (SetQueuePenalty(accountId, GameType.PvP, duration, capPenalty: true))
                {
                    LocalizationArg argDuration = LocalizationArg_TimeSpan.Create(duration);
                    LocalizationPayload msg =
                        LocalizationPayload.Create("QueueDodgerPenaltyAppliedToSelf", "Matchmaking", argDuration);
                    ClientNotifier.Get().SendSystemMessage(accountId, msg);
                }
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
        double escalationRatio,
        TimeSpan escalationCap,
        TimeSpan paroleWindow)
    {
        int count = current.QueueDodgeCount;
        TimeSpan span = requestedSpan;

        // Repeat-offender escalation: scale the penalty by ratio^(count-1), clamped to the cap.
        // The count decays whenever the parole window has lapsed without a new offense.
        if (escalate)
        {
            if (current.QueueDodgeParoleTimeout != DateTime.MinValue
                && current.QueueDodgeParoleTimeout < now)
            {
                count = 0;
            }
            count += 1;
            double multiplier = Math.Pow(escalationRatio, count - 1);
            span = TimeSpan.FromTicks((long)Math.Min(requestedSpan.Ticks * multiplier, escalationCap.Ticks));
        }

        DateTime newTimeout = now.Add(span);
        DateTime oldTimeout = current.QueueDodgeBlockTimeout;

        // Normal mode moves the timeout only if it raises it; cap mode only if it lowers it;
        // overridePenalty bypasses the direction check.
        bool lowersTimeout = oldTimeout > newTimeout;
        bool moveTimeout = oldTimeout != newTimeout && (capPenalty == lowersTimeout || overridePenalty);

        // An escalating offense is recorded (count + parole) even when it cannot extend the block,
        // otherwise a repeat offense during a longer active block would not count towards the streak.
        // Likewise, a pardon forgives the offense even when the block has already run out.
        if (!moveTimeout && !escalate && !capPenalty)
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

        return new PenaltyEvaluation(true, resultCount, moveTimeout ? newTimeout : oldTimeout, resultParole, span);
    }

    internal static bool IsQueueBlocked(QueuePenalties penalties, DateTime now)
    {
        return penalties is not null
               && penalties.QueueDodgeBlockTimeout > now.Add(TimeSpan.FromSeconds(1));
    }

    // Returns whether the block timeout has changed
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
            LobbyConfiguration.GetQueuePenaltyEscalationRatio(),
            LobbyConfiguration.GetQueuePenaltyEscalationCap(),
            LobbyConfiguration.GetQueuePenaltyParoleWindow());
        if (!eval.Apply)
        {
            return false;
        }

        DateTime oldTimeout = penalties.QueueDodgeBlockTimeout;
        bool blockChanged = eval.BlockTimeout != oldTimeout;
        penalties.QueueDodgeCount = eval.Count;
        penalties.QueueDodgeParoleTimeout = eval.ParoleTimeout;
        penalties.QueueDodgeBlockTimeout = eval.BlockTimeout;
        log.Info($"{gameType} queue penalty for {account.Handle}: "
                 + (blockChanged
                     ? $"{eval.AppliedSpan}"
                     : oldTimeout > referenceDateTime
                         ? $"block unchanged ({oldTimeout.Subtract(referenceDateTime)} remaining)"
                         : "not blocked")
                 + (escalate ? $" (offense #{eval.Count})" : "")
                 + (capPenalty ? $" (pardoned, {eval.Count} offenses left)" : "")
                 + (blockChanged && oldTimeout > referenceDateTime ? $" (was {oldTimeout.Subtract(referenceDateTime)})" : ""));

        account.AdminComponent.ActiveQueuePenalties[gameType] = penalties;
        DB.Get().AccountDao.UpdateAdminComponent(account);

        if (!capPenalty)
        {
            GroupInfo playerGroup = GroupManager.GetPlayerGroup(accountId);
            if (playerGroup is not null)
            {
                MatchmakingManager.RemoveGroupFromQueue(playerGroup, true);
            }
        }

        return blockChanged;
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
                    LocalizationPayload localizationPayload = accountId != groupMember
                        ? MakeGroupmateBlockedMessage(account.AccountId, argDuration)
                        : MakeSelfBlockedMessage(argDuration);
                    ClientNotifier.Get().SendSystemMessage(groupMember, localizationPayload);
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