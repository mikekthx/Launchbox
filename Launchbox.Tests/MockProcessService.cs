using Launchbox.Services;
using System;

namespace Launchbox.Tests;

public class MockProcessService : IProcessService
{
    public bool ShouldReturnTrue { get; set; }
    public bool ShouldThrow { get; set; }
    public int CallCount { get; private set; }

    public bool IsProcessRunning(string processName)
    {
        CallCount++;
        if (ShouldThrow) throw new Exception("Process check failed");
        return ShouldReturnTrue;
    }
}
