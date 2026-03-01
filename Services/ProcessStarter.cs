using Launchbox.Helpers;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo != null && PathSecurity.IsUnsafePath(startInfo.FileName))
        {
            Trace.WriteLine($"Blocked process start for unsafe path: {PathSecurity.RedactPath(startInfo.FileName)}");
            throw new UnauthorizedAccessException($"Execution of unsafe path blocked.");
        }

        return Process.Start(startInfo!);
    }
}
