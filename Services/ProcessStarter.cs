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
            throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(startInfo.FileName)}' is denied.");
        }

        return startInfo != null ? Process.Start(startInfo) : null;
    }
}
