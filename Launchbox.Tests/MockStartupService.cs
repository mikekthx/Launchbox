using Launchbox.Services;
using System;
using System.Threading.Tasks;

namespace Launchbox.Tests;

public class MockStartupService : IStartupService
{
    public bool IsEnabled { get; set; } = false;
    public bool ShouldFail { get; set; } = false;
    public bool Success { get; set; } = true;
    public bool IsSupported { get; set; } = true;

    public Task<bool> IsRunAtStartupEnabledAsync()
    {
        if (ShouldFail) throw new Exception("Startup check failed");
        return Task.FromResult(IsEnabled);
    }

    /// <summary>
    /// When non-null, returned from <see cref="TryEnableStartupAsync"/> instead of completing immediately.
    /// Lets tests simulate an in-flight async operation.
    /// </summary>
    public Task<bool>? EnableTask { get; set; }

    public Task<bool> TryEnableStartupAsync()
    {
        if (ShouldFail) throw new Exception("Enable startup failed");
        if (!Success) return Task.FromResult(false);
        if (EnableTask != null) return EnableTask;
        IsEnabled = true;
        return Task.FromResult(true);
    }

    public Task DisableStartupAsync()
    {
        if (ShouldFail) throw new Exception("Disable startup failed");
        IsEnabled = false;
        return Task.CompletedTask;
    }
}
