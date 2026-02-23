namespace Launchbox.Services;

public interface IProcessService
{
    bool IsProcessRunning(string processName);
}
