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
        ArgumentNullException.ThrowIfNull(startInfo);

        string expandedFileName = Environment.ExpandEnvironmentVariables(startInfo.FileName ?? string.Empty);
        if (PathSecurity.IsUnsafePath(expandedFileName))
        {
            throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(expandedFileName)}' is denied.");
        }
        startInfo.FileName = expandedFileName;

        string expandedWorkingDirectory = Environment.ExpandEnvironmentVariables(startInfo.WorkingDirectory ?? string.Empty);
        if (!string.IsNullOrEmpty(expandedWorkingDirectory) && PathSecurity.IsUnsafePath(expandedWorkingDirectory))
        {
            throw new UnauthorizedAccessException("Execution with unsafe working directory is denied.");
        }
        startInfo.WorkingDirectory = expandedWorkingDirectory;

        if (PathSecurity.ContainsUncPath(startInfo.Arguments))
        {
            throw new UnauthorizedAccessException("Execution with unsafe arguments is denied.");
        }

        return Process.Start(startInfo);
    }
}
