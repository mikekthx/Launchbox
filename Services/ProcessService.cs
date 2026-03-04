using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessService : IProcessService
{
    public bool IsProcessRunning(string processName)
    {
        Process[] processes = System.Array.Empty<Process>();
        try
        {
            processes = GetProcessesByName(processName);
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    protected virtual Process[] GetProcessesByName(string processName)
    {
        return Process.GetProcessesByName(processName);
    }
}
