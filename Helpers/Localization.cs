using Launchbox.Services;
using System.Threading;

namespace Launchbox.Helpers;

internal static class Localization
{
    // Volatile.Read/Write ensures SetProvider assignments are immediately visible to all threads.
    private static IStringProvider _provider = new DefaultStringProvider();

    internal static void SetProvider(IStringProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Volatile.Write(ref _provider, provider);
    }

    public static string GetString(string key) => Volatile.Read(ref _provider).GetString(key);

    // Fallback provider that returns the key itself; used until the production
    // ResourceStringProvider is set in App.xaml.cs, and as a test seam.
    private sealed class DefaultStringProvider : IStringProvider
    {
        public string GetString(string key) => key;
    }
}
