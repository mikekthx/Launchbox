using Launchbox.Services;
using System;
using System.Diagnostics;

namespace Launchbox.Tests;

public class MockProcessStarter : IProcessStarter
{
    public Exception? ExceptionToThrow { get; set; }
    public ProcessStartInfo? LastStartInfo { get; private set; }
    public bool WasStarted { get; private set; }

    public Process? Start(ProcessStartInfo startInfo)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        LastStartInfo = startInfo;
        WasStarted = true;
        return null;
    }
}
