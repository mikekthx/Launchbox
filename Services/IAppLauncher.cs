namespace Launchbox.Services;

public interface IAppLauncher
{
    void Launch(string path);
    void OpenFolder(string path);
}
