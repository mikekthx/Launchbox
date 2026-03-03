using Launchbox.Helpers;
using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo != null && PathSecurity.IsUnsafePath(startInfo.FileName))
        {
            Trace.WriteLine($"Blocked Process.Start for unsafe path: {PathSecurity.RedactPath(startInfo.FileName)}");
            return null;
        }

        return startInfo != null ? Process.Start(startInfo) : null;
    }
}
