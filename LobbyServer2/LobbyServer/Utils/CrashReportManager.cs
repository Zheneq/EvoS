using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using LobbyGameClientMessages;
using log4net;

namespace CentralServer.LobbyServer.Utils;

public static class CrashReportManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CrashReportManager));
    
    private static readonly Dictionary<long, Guid> PendingCrashReportGuids = new();

    public static Action<long, Stream> OnArchive = delegate {};
    public static Action<long, ClientStatusReport> OnStatusReport = delegate {};
    public static Action<long, uint, uint, string, ClientErrorDao.Entry> OnErrorReport = delegate {};
    public static Action<long, ClientErrorReport> OnNewError = delegate {};

    public static Guid Add(long accountId)
    {
        Guid guid = Guid.NewGuid();
        PendingCrashReportGuids[accountId] = guid;
        return guid;
    }

    public static long Pop(Guid guid)
    {
        long accountId = PendingCrashReportGuids.FirstOrDefault(e => e.Value == guid).Key;
        PendingCrashReportGuids.Remove(accountId);
        return accountId;
    }

    public static void ProcessArchive(long accountId, Stream archive)
    {
        OnArchive(accountId, archive);
    }

    public static void ProcessClientStatusReport(long accountId, ClientStatusReport report)
    {
        OnStatusReport(accountId, report);
    }

    // error summary reporting mode (LobbyStatusNotification.ErrorReportRate > 0)
    public static void ProcessClientErrorSummary(long accountId, ClientErrorSummary summary)
    {
        var clientErrorDao = DB.Get().ClientErrorDao;

        foreach (var (stackTraceHash, errorCount) in summary.ReportCount)
        {
            ClientErrorDao.Entry entry = clientErrorDao.GetEntry(stackTraceHash);
            if (entry is null)
            {
                // request details on errors we've never seen
                ClientNotifier.Get().Send(accountId, new ErrorReportSummaryRequest { CrashReportHash = stackTraceHash });
            }

            HandleErrorOccurrence(accountId, stackTraceHash, errorCount, entry);
        }
    }

    // asap error reporting mode (LobbyStatusNotification.ErrorReportRate = 0)
    public static void ProcessClientErrorReport(long accountId, ClientErrorReport report)
    {
        ClientErrorDao.Entry entry = DB.Get().ClientErrorDao.GetEntry(report.StackTraceHash);
        if (entry is null)
        {
            HandleNewError(accountId, report);
        }
        
        HandleErrorOccurrence(accountId, report.StackTraceHash, 1, entry);
    }

    public static void ProcessErrorReportSummaryResponse(long accountId, ErrorReportSummaryResponse response)
    {
        ClientErrorDao.Entry entry = DB.Get().ClientErrorDao.GetEntry(response.ClientErrorReport.StackTraceHash);
        if (entry is not null)
        {
            log.Warn($"Client {LobbyServerUtils.GetHandleForLog(accountId)} sent us an error {
                response.ClientErrorReport.StackTraceHash} summary that we already have");
            return;
        }
        
        HandleNewError(accountId, response.ClientErrorReport);
    }

    private static void HandleNewError(long accountId, ClientErrorReport report)
    {
        DB.Get().ClientErrorDao.SaveEntry(ClientErrorDao.Entry.Of(report));
        OnNewError(accountId, report);
    }

    private static void HandleErrorOccurrence(
        long accountId,
        uint stackTraceHash,
        uint errorCount,
        ClientErrorDao.Entry entry)
    {
        string clientVersion = SessionManager.GetSessionInfo(accountId)?.BuildVersion ?? "";
        DB.Get().ClientErrorReportDao.SaveEntry(new ClientErrorReportDao.Entry
        {
            AccountId = accountId,
            StackTraceHash = stackTraceHash,
            Time = DateTime.UtcNow,
            Count = errorCount,
            Version = clientVersion
        });
        OnErrorReport(accountId, stackTraceHash, errorCount, clientVersion, entry);
    }
}