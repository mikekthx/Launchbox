using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessService : IProcessService
{
    public bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }
}
