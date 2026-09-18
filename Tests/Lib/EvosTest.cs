using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Xunit.Abstractions;

namespace Tests.Lib;

public class EvosTest : IDisposable
{
    // xUnit runs test classes in parallel and log4net's root appender list is process-wide.
    // Configure must run exactly once: re-running it resets the appender list under running
    // tests' feet, making their RemoveAppender in Dispose throw.
    static EvosTest()
    {
        XmlConfigurator.Configure(new FileInfo("log4net.xml"));
    }

    private readonly IAppenderAttachable _attachable;
    private readonly TestOutputAppender _appender;

    protected EvosTest(ITestOutputHelper output)
    {
        _attachable = ((Hierarchy)LogManager.GetRepository()).Root;

        _appender = new TestOutputAppender(output);
        _attachable.AddAppender(_appender);
    }

    public void Dispose()
    {
        // Stop forwarding before detaching: a log call from another test's thread may still be
        // fanned out to this appender, and writing to a finished test's output helper throws.
        _appender.Stop();
        _attachable.RemoveAppender(_appender);
    }
}
