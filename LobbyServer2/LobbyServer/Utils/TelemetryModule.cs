using CentralServer.LobbyServer.Discord;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using LobbyGameClientMessages;
using log4net;
using static EvoS.Framework.Misc.GameUtils;

namespace CentralServer.LobbyServer.Utils;

public class TelemetryModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(TelemetryModule));
    private readonly IClientConnection _conn;

    public TelemetryModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<UIActionNotification>(HandleUIActionNotification);
        registry.Register<CrashReportArchiveNameRequest>(HandleCrashReportArchiveNameRequest);
        registry.Register<ClientStatusReport>(HandleClientStatusReport);
        registry.Register<ClientErrorSummary>(HandleClientErrorSummary);
        registry.Register<ClientErrorReport>(HandleClientErrorReport);
        registry.Register<ErrorReportSummaryResponse>(HandleErrorReportSummaryResponse);
        registry.Register<ClientFeedbackReport>(HandleClientFeedbackReport);
        registry.Register<ClientPerformanceReport>(HandleClientPerformanceReport);
    }

    private void HandleUIActionNotification(UIActionNotification notify)
    {
    }

    private void HandleCrashReportArchiveNameRequest(CrashReportArchiveNameRequest request)
    {
        CrashReportArchiveNameResponse response = new CrashReportArchiveNameResponse
        {
            Success = false,
            ResponseId = request.RequestId
        };

        LobbySessionInfo sessionInfo = SessionManager.GetSessionInfo(_conn.AccountId);
        if (sessionInfo is not null)
        {
            BuildVersionInfo info = sessionInfo.BuildVersionInfo;
            if (info.IsPatched)
            {
                response.Success = true;
                response.ArchiveName = CrashReportManager.Add(_conn.AccountId).ToString();
            }
        }
        _conn.Send(response);
    }

    private void HandleClientStatusReport(ClientStatusReport msg)
    {
        string shortDetails = msg.StatusDetails != null ? msg.StatusDetails.Split('\n', 2)[0] : "";
        log.Info($"ClientStatusReport {msg.Status}: {shortDetails} ({msg.UserMessage})");
        CrashReportManager.ProcessClientStatusReport(_conn.AccountId, msg);
    }

    private void HandleClientErrorSummary(ClientErrorSummary msg)
    {
        foreach (var (key, count) in msg.ReportCount)
        {
            log.Info($"ClientErrorSummary {key}: {count}");
        }
        CrashReportManager.ProcessClientErrorSummary(_conn.AccountId, msg);
    }

    private void HandleClientErrorReport(ClientErrorReport msg)
    {
        log.Info($"ClientErrorReport {msg.StackTraceHash}: {msg.LogString} {msg.StackTrace} {msg.Time}");
        CrashReportManager.ProcessClientErrorReport(_conn.AccountId, msg);
    }

    private void HandleErrorReportSummaryResponse(ErrorReportSummaryResponse response)
    {
        log.Info($"ErrorReportSummaryResponse {response.ClientErrorReport.StackTraceHash}: {
            response.ClientErrorReport.LogString} {response.ClientErrorReport.StackTrace
            } {response.ClientErrorReport.Time}");
        CrashReportManager.ProcessErrorReportSummaryResponse(_conn.AccountId, response);
    }

    private void HandleClientFeedbackReport(ClientFeedbackReport message)
    {
        string context = _conn.CurrentGame is not null ? GameIdString(_conn.CurrentGame.GameInfo) : "";
        if (message.ReportedPlayerAccountId == 0 && message.ReportedPlayerHandle is not null)
        {
            message.ReportedPlayerAccountId = LobbyServerUtils.ResolveAccountId(message.ReportedPlayerHandle);
        }
        DB.Get().UserFeedbackDao.Save(new UserFeedbackDao.UserFeedback(_conn.AccountId, message, context));
        DiscordManager.Get().SendPlayerFeedback(_conn.AccountId, message);
    }

    private void HandleClientPerformanceReport(ClientPerformanceReport msg)
    {
        log.Info($"ClientPerformanceReport {msg.PerformanceInfo}");
    }
}
