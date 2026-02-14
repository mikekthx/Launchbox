using System.Threading.Tasks;

namespace Launchbox.Services;

public interface IStartupService
{
    Task<bool> IsRunAtStartupEnabledAsync();
    Task<bool> TryEnableStartupAsync();
    Task DisableStartupAsync();
    bool IsSupported { get; }
}
