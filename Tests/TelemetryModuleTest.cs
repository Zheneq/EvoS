using System;
using System.Collections.Generic;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.WebSocket;
using LobbyGameClientMessages;
using Tests.Lib;
using Xunit.Abstractions;

namespace Tests;

public class TelemetryModuleTest : EvosTest
{
    public TelemetryModuleTest(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Minimal IHandlerRegistry that lets tests dispatch messages directly into a module.
    /// </summary>
    private sealed class CapturingRegistry : IHandlerRegistry
    {
        private readonly Dictionary<Type, Action<WebSocketMessage>> _handlers = new();

        void IHandlerRegistry.Register<T>(Action<T> handler)
        {
            _handlers[typeof(T)] = msg => handler((T)msg);
        }

        public void Dispatch<T>(T message) where T : WebSocketMessage
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
                handler(message);
            else
                throw new InvalidOperationException($"No handler registered for {typeof(T).Name}");
        }
    }

    private static (TelemetryModule module, RecordingClientConnection conn, CapturingRegistry registry)
        MakeModule(long accountId)
    {
        var conn = new RecordingClientConnection { AccountId = accountId };
        var module = new TelemetryModule(conn);
        var registry = new CapturingRegistry();
        module.Register(registry);
        return (module, conn, registry);
    }

    private static long UniqueId() => (long)(Guid.NewGuid().GetHashCode() & 0x7FFFFFFF) + 2_000_000L;

    // --- CrashReportArchiveNameRequest ---

    [Fact]
    public void CrashReportArchiveName_NoSession_ReturnsFailed()
    {
        // No session registered in SessionManager (the test default).
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        registry.Dispatch(new CrashReportArchiveNameRequest { RequestId = 1 });

        // Exactly one response, with Success = false.
        Assert.Single(conn.Sent);
        var response = Assert.IsType<CrashReportArchiveNameResponse>(conn.Sent[0]);
        Assert.False(response.Success);
    }

    // --- ClientFeedbackReport ---

    [Fact]
    public void ClientFeedbackReport_NoCurrentGame_DoesNotThrow()
    {
        // CurrentGame defaults to null on RecordingClientConnection.
        // DiscordManager.SendPlayerFeedback early-returns when no Discord channel is configured
        // — safe in tests.
        // NOTE: UserFeedbackMockDao.Save is a no-op and Get always returns empty, so we can
        // only assert no-throw here; the DB write path is exercised in integration tests.
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var message = new ClientFeedbackReport
        {
            Message = "test feedback",
            Reason = ClientFeedbackReport.FeedbackReason.Bug,
            ReportedPlayerAccountId = 0,
            ReportedPlayerHandle = null
        };

        // Must not throw.
        var exception = Record.Exception(() => registry.Dispatch(message));
        Assert.Null(exception);
    }

    // --- ClientErrorReport and ClientErrorSummary ---

    [Fact]
    public void ClientErrorReport_AndClientErrorSummary_DoNotThrow()
    {
        // ProcessClientErrorSummary for an unknown hash routes an ErrorReportSummaryRequest
        // through ClientNotifier — the account is offline in tests, so a no-op.
        // We assert no-throw only (simpler than joining [Collection("ClientNotifierSeam")]).
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var errorReport = new ClientErrorReport
        {
            StackTraceHash = 0xABC123,
            LogString = "NullReferenceException",
            StackTrace = "at Foo.Bar()",
            Time = 1.0f
        };

        var errorSummary = new ClientErrorSummary
        {
            ReportCount = new Dictionary<uint, uint> { { 0xABC123, 3 } }
        };

        var exception1 = Record.Exception(() => registry.Dispatch(errorReport));
        var exception2 = Record.Exception(() => registry.Dispatch(errorSummary));

        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    // --- ClientPerformanceReport and UIActionNotification ---

    [Fact]
    public void ClientPerformanceReport_AndUIActionNotification_DoNotThrow()
    {
        long accountId = UniqueId();
        var (_, conn, registry) = MakeModule(accountId);

        var exception1 = Record.Exception(() =>
            registry.Dispatch(new ClientPerformanceReport()));
        var exception2 = Record.Exception(() =>
            registry.Dispatch(new UIActionNotification()));

        Assert.Null(exception1);
        Assert.Null(exception2);
    }
}
