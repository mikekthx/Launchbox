using System;
using System.Threading.Tasks;
using Launchbox.Services;

namespace Launchbox.Tests;

public class MockStartupService : IStartupService
{
    public bool IsEnabled { get; set; } = false;
    public bool ShouldFail { get; set; } = false;
    public bool IsSupported { get; set; } = true;

    public Task<bool> IsRunAtStartupEnabledAsync()
    {
        if (ShouldFail)
        {
            throw new Exception("Startup check failed");
        }
        return Task.FromResult(IsEnabled);
    }

    public Task<bool> TryEnableStartupAsync()
    {
        if (ShouldFail)
        {
            throw new Exception("Enable startup failed");
        }
        IsEnabled = true;
        return Task.FromResult(true);
    }

    public Task DisableStartupAsync()
    {
        if (ShouldFail)
        {
            throw new Exception("Disable startup failed");
        }
        IsEnabled = false;
        return Task.CompletedTask;
    }
}
