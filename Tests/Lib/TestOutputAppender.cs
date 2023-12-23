using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using Xunit.Abstractions;

namespace Tests.Lib;

public sealed class TestOutputAppender : AppenderSkeleton
{
    private readonly ITestOutputHelper _xunitTestOutputHelper;

    public TestOutputAppender(ITestOutputHelper xunitTestOutputHelper)
    {
        _xunitTestOutputHelper = xunitTestOutputHelper;
        Name = "TestOutputAppender";
        Layout = new PatternLayout("%-5p %d %5rms %-22.22c{1} %-18.18M - %m%n");
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
        _xunitTestOutputHelper.WriteLine(RenderLoggingEvent(loggingEvent).TrimEnd());
    }
}