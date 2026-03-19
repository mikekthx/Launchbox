using Launchbox.Helpers;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

// Shortcut (.lnk/.url) metadata validation is the caller's responsibility.
// This class only validates what is visible in the ProcessStartInfo fields.
public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo != null)
        {
            if (PathSecurity.IsUnsafePath(startInfo.FileName))
            {
                throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(startInfo.FileName)}' is denied.");
            }

            if (!string.IsNullOrEmpty(startInfo.WorkingDirectory) && PathSecurity.IsUnsafePath(startInfo.WorkingDirectory))
            {
                throw new UnauthorizedAccessException("Execution with unsafe working directory is denied.");
            }

            if (!string.IsNullOrEmpty(startInfo.Arguments) &&
                (startInfo.Arguments.Contains(@"\\") || startInfo.Arguments.Contains("//") ||
                 startInfo.Arguments.Contains(@"\/") || startInfo.Arguments.Contains(@"/\")))
            {
                throw new UnauthorizedAccessException("Execution with unsafe arguments is denied.");
            }

            return Process.Start(startInfo);
        }

        return null;
    }
}
