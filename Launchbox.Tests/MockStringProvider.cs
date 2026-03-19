using Launchbox.Services;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockStringProvider : IStringProvider
{
    private readonly Dictionary<string, string> _strings;

    public MockStringProvider(Dictionary<string, string> strings)
    {
        _strings = strings;
    }

    public string GetString(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;
}
