using Launchbox.Services;

namespace Launchbox.Helpers;

internal static class Localization
{
    private static IStringProvider _provider = new DefaultStringProvider();

    internal static void SetProvider(IStringProvider provider) => _provider = provider;

    public static string GetString(string key) => _provider.GetString(key);

    // Temporary default that returns the key itself; replaced by ResourceStringProvider
    // once the .resw files exist (Task 3).
    private sealed class DefaultStringProvider : IStringProvider
    {
        public string GetString(string key) => key;
    }
}
