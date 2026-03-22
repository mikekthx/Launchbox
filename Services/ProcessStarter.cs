using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;

namespace Launchbox.Services;

// Shortcut (.lnk/.url) metadata validation is the caller's responsibility.
// This class only validates what is visible in the ProcessStartInfo fields.
public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        if (PathSecurity.IsUnsafePath(startInfo.FileName))
        {
            throw new UnauthorizedAccessException($"Execution of unsafe path '{PathSecurity.RedactPath(startInfo.FileName)}' is denied.");
        }

        if (!string.IsNullOrEmpty(startInfo.WorkingDirectory) && PathSecurity.IsUnsafePath(startInfo.WorkingDirectory))
        {
            throw new UnauthorizedAccessException("Execution with unsafe working directory is denied.");
        }

        if (PathSecurity.ContainsUncPath(startInfo.Arguments))
        {
            throw new UnauthorizedAccessException("Execution with unsafe arguments is denied.");
        }

        try
        {
            return Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            Trace.WriteLine($"Win32Exception in ProcessStarter: {PathSecurity.GetSafeExceptionMessage(ex)}");
            throw new Win32Exception(ex.NativeErrorCode, "A system error occurred while starting the process.");
        }
        catch (FileNotFoundException ex)
        {
            Trace.WriteLine($"FileNotFoundException in ProcessStarter: {PathSecurity.GetSafeExceptionMessage(ex)}");
            throw new FileNotFoundException("The specified executable could not be found.");
        }
    }
}
