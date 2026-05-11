using Launchbox.Services;

namespace Launchbox.Tests;

public class MockAppLauncher : IAppLauncher
{
    public string? LastLaunchedPath { get; private set; }
    public string? LastOpenedFolder { get; private set; }
    public bool ThrowOnLaunch { get; set; }

    public void Launch(string path)
    {
        if (ThrowOnLaunch)
            throw new InvalidOperationException("Simulated launch failure");
        LastLaunchedPath = path;
    }

    public void OpenFolder(string path)
    {
        LastOpenedFolder = path;
    }
}
