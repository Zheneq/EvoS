using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Xunit.Abstractions;

namespace Tests.Lib;

public class EvosTest : IDisposable
{
    private readonly IAppenderAttachable _attachable;
    private readonly TestOutputAppender _appender;

    protected EvosTest(ITestOutputHelper output)
    {
        XmlConfigurator.Configure(new FileInfo("log4net.xml"));
        _attachable = ((Hierarchy)LogManager.GetRepository()).Root;

        _appender = new TestOutputAppender(output);
        _attachable.AddAppender(_appender);
    }

    public void Dispose()
    {
        _attachable.RemoveAppender(_appender);
    }
}