using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Launchbox.Services;

public class WinUIStartupService : IStartupService
{
    private const string TaskId = "LaunchboxStartup";

    public bool IsSupported => true;

    public async Task<bool> IsRunAtStartupEnabledAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to get StartupTask state: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TryEnableStartupAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            var state = await task.RequestEnableAsync();
            return state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to enable StartupTask: {ex.Message}");
            return false;
        }
    }

    public async Task DisableStartupAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            task.Disable();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to disable StartupTask: {ex.Message}");
        }
    }
}
