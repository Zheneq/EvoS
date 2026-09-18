using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using Xunit.Abstractions;

namespace Tests.Lib;

public sealed class TestOutputAppender : AppenderSkeleton
{
    private readonly ITestOutputHelper _xunitTestOutputHelper;
    private volatile bool _stopped;

    public TestOutputAppender(ITestOutputHelper xunitTestOutputHelper)
    {
        _xunitTestOutputHelper = xunitTestOutputHelper;
        Name = "TestOutputAppender";
        Layout = new PatternLayout("%-5p %d %5rms %-22.22c{1} %-18.18M - %m%n");
    }

    public void Stop()
    {
        _stopped = true;
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
        if (_stopped)
        {
            return;
        }
        try
        {
            _xunitTestOutputHelper.WriteLine(RenderLoggingEvent(loggingEvent).TrimEnd());
        }
        catch (InvalidOperationException)
        {
            // The test this helper belongs to has already finished; drop the message.
        }
    }
}