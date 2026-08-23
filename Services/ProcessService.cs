using Launchbox.Helpers;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessService : IProcessService
{
    public bool IsProcessRunning(string processName)
    {
        Process[] processes = [];
        try
        {
            processes = GetProcessesByName(processName);
            return processes.Length > 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error checking if process {processName} is running: {PathSecurity.GetSafeExceptionMessage(ex)}");
            return false;
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
