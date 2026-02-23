using System.Diagnostics;

namespace Launchbox.Services;

public interface IProcessStarter
{
    Process? Start(ProcessStartInfo startInfo);
}
