using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework;
using log4net;

namespace CentralServer.LobbyServer.Matchmaking;

/// <summary>
/// Tracks short-lived, in-memory "queue priority credits" granted to players whose match was
/// cancelled through no fault of their own (a dodge/disconnect during setup, a server/launch
/// failure, or a started game that collapsed early into a NoResult/Draw). If such a player
/// requeues within the requeue window, their group is stamped with their original queue time
/// instead of the current time, so they keep the position in line they had earned.
/// Nothing here is persisted.
/// </summary>
public static class QueuePriorityManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(QueuePriorityManager));

    private record PriorityCredit(DateTime OriginalQueueTime, DateTime ExpiresAt);

    private static readonly ConcurrentDictionary<long, PriorityCredit> Credits = new();

    /// <summary>
    /// Grants (or refreshes) a priority credit for a player, remembering the queue time they had
    /// before their match was cancelled. The credit expires after the configured requeue window.
    /// </summary>
    public static void GrantCredit(long accountId, DateTime originalQueueTime)
    {
        GrantCredit(accountId, originalQueueTime, DateTime.UtcNow + LobbyConfiguration.GetQueuePriorityRequeueWindow());
    }

    public static void GrantCredit(long accountId, DateTime originalQueueTime, DateTime expiresAt)
    {
        Credits[accountId] = new PriorityCredit(originalQueueTime, expiresAt);
        log.Info($"Granted queue priority credit to {LobbyServerUtils.GetHandle(accountId)} "
                 + $"(original queue time {originalQueueTime:o}, expires {expiresAt:o})");
    }

    /// <summary>
    /// If every member has a live (non-expired) priority credit, returns true and outputs the
    /// latest of their original queue times (conservative). Does not consume the credits.
    /// </summary>
    public static bool TryGetGroupQueueTime(IEnumerable<long> members, out DateTime queueTime)
    {
        PruneExpired();
        queueTime = default;

        DateTime now = DateTime.UtcNow;
        DateTime latest = DateTime.MinValue;
        bool any = false;
        foreach (long accountId in members)
        {
            any = true;
            if (!Credits.TryGetValue(accountId, out PriorityCredit credit) || credit.ExpiresAt <= now)
            {
                return false;
            }
            if (credit.OriginalQueueTime > latest)
            {
                latest = credit.OriginalQueueTime;
            }
        }

        if (!any)
        {
            return false;
        }

        queueTime = latest;
        return true;
    }

    /// <summary>
    /// Removes the priority credits of the given members. Call after they have successfully
    /// requeued so a credit cannot be reused.
    /// </summary>
    public static void Consume(IEnumerable<long> members)
    {
        foreach (long accountId in members)
        {
            Credits.TryRemove(accountId, out _);
        }
    }

    private static void PruneExpired()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<long, PriorityCredit> entry in Credits.ToList())
        {
            if (entry.Value.ExpiresAt <= now)
            {
                Credits.TryRemove(entry.Key, out _);
            }
        }
    }
}
