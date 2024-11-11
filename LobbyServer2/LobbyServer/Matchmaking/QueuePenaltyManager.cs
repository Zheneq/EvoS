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
                SetQueuePenalty(accountId, GameType.PvP, TimeSpan.FromMinutes(5));
                return;
            }
            int replacedWithBotsNum = game.TeamInfo.TeamPlayerInfo.Count(i => i.ReplacedWithBots);
            if (replacedWithBotsNum == game.TeamInfo.TeamPlayerInfo.Count)
            {
                CapQueuePenalties(game);
                return;
            }
            if (replacedWithBotsNum * 2 > game.TeamInfo.TeamPlayerInfo.Count)
            {
                return;
            }
            if (game.GameStatus != GameStatus.Stopped)
            {
                SetQueuePenalty(accountId, GameType.PvP, TimeSpan.FromSeconds(200));
            }
            else if (game.StopTime > DateTime.UtcNow)
            {
                SetQueuePenalty(accountId, GameType.PvP, DateTime.UtcNow.Subtract(game.StopTime).Add(TimeSpan.FromSeconds(30)));
            }
        }
    }

    public static void CapQueuePenalties(Game game)
    {
        foreach (long accountId in game.GetPlayers())
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

    private static bool SetQueuePenalty(
        long accountId,
        GameType gameType,
        TimeSpan timeSpan,
        bool overridePenalty = false,
        bool capPenalty = false)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        if (account is null)
        {
            return false;
        }
        account.AdminComponent.ActiveQueuePenalties ??= new Dictionary<GameType, QueuePenalties>();
        QueuePenalties penalties = account.AdminComponent.ActiveQueuePenalties.GetValueOrDefault(gameType);
        DateTime referenceDateTime = DateTime.UtcNow;
        DateTime newTimeout = referenceDateTime.Add(timeSpan);
        if (penalties is null)
        {
            penalties = new QueuePenalties();
            penalties.ResetQueueDodge();
        }

        DateTime oldTimeout = penalties.QueueDodgeBlockTimeout;

        if (oldTimeout == newTimeout)
        {
            return false;
        }
        if (capPenalty != oldTimeout > newTimeout && !overridePenalty)
        {
            return false;
        }
        
        penalties.QueueDodgeBlockTimeout = newTimeout;
        log.Info($"{gameType} queue penalty for {account.Handle}: {timeSpan}" 
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

    public static LocalizationPayload CheckQueuePenalties(long accountId, GameType selectedGameType)
    {
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        QueuePenalties queuePenalties = account?.AdminComponent.ActiveQueuePenalties?.GetValueOrDefault(selectedGameType);
        if (queuePenalties is not null && queuePenalties.QueueDodgeBlockTimeout > DateTime.UtcNow.Add(TimeSpan.FromSeconds(5)))
        {
            TimeSpan duration = queuePenalties.QueueDodgeBlockTimeout.Subtract(DateTime.UtcNow);
            LocalizationArg argDuration = LocalizationArg_TimeSpan.Create(duration);
            LocalizationPayload failure = LocalizationPayload.Create("QueueDodgerPenaltyAppliedToSelf", "Matchmaking", argDuration);
            log.Info($"{account.Handle} cannot join {selectedGameType} queue until {queuePenalties.QueueDodgeBlockTimeout}");
            
            GroupInfo group = GroupManager.GetPlayerGroup(accountId);
            if (group != null)
            {
                LocalizationArg argHandle = LocalizationArg_Handle.Create(account.Handle);
                foreach (long groupMember in group.Members)
                {
                    if (groupMember == accountId) continue;
                    LobbyServerProtocol conn = SessionManager.GetClientConnection(groupMember);
                    conn?.SendSystemMessage(
                        LocalizationPayload.Create("QueueDodgerPenaltyAppliedToGroupmate", "Matchmaking", argHandle, argDuration));
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
}