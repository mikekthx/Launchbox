using Launchbox.Services;
using System.Diagnostics;

namespace Launchbox.Tests;

public class MockProcessStarter : IProcessStarter
{
    public ProcessStartInfo? LastStartInfo { get; private set; }
    public bool WasStarted { get; private set; }

    public Process? Start(ProcessStartInfo startInfo)
    {
        LastStartInfo = startInfo;
        WasStarted = true;
        return null;
    }
}
