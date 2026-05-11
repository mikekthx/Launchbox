using Launchbox.Services;

namespace Launchbox.Tests;

public class MockAppLauncher : IAppLauncher
{
    public string? LastLaunchedPath { get; private set; }
    public string? LastOpenedFolder { get; private set; }

    public void Launch(string path)
    {
        LastLaunchedPath = path;
    }

    public void OpenFolder(string path)
    {
        LastOpenedFolder = path;
    }
}
