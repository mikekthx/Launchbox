using Launchbox.Helpers;
using System;
using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessStarter : IProcessStarter
{
    private readonly IShortcutResolver _shortcutResolver;

    public ProcessStarter(IShortcutResolver shortcutResolver)
    {
        _shortcutResolver = shortcutResolver;
    }

    public Process? Start(ProcessStartInfo startInfo)
    {
        if (startInfo != null)
        {
            if (PathSecurity.IsUnsafePath(startInfo.FileName))
            {
                throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(startInfo.FileName)}' is denied.");
            }

            if (startInfo.UseShellExecute && startInfo.FileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var metadata = _shortcutResolver.Resolve(startInfo.FileName);
                string? args = metadata?.Arguments;
                if (!string.IsNullOrEmpty(args) && (args.Contains(@"\\") || args.Contains("//") || args.Contains(@"\/") || args.Contains(@"/\")))
                {
                    throw new UnauthorizedAccessException("Execution with unsafe arguments is denied.");
                }

                string? workingDir = metadata?.WorkingDirectory;
                if (!string.IsNullOrEmpty(workingDir) && PathSecurity.IsUnsafePath(workingDir))
                {
                    throw new UnauthorizedAccessException("Execution with unsafe working directory is denied.");
                }
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
