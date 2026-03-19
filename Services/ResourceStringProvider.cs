using Microsoft.Windows.ApplicationModel.Resources;

namespace Launchbox.Services;

internal sealed class ResourceStringProvider : IStringProvider
{
    private readonly ResourceLoader _loader = new();

    public string GetString(string key) => _loader.GetString(key);
}
