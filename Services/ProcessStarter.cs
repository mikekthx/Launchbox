using System.Diagnostics;

namespace Launchbox.Services;

public class ProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        return Process.Start(startInfo);
    }
}
