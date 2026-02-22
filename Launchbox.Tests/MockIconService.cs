using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockIconService : IIconService
{
    private readonly Dictionary<string, byte[]> _iconData = [];
    private readonly HashSet<string> _throwingPaths = [];

    public void AddIcon(string path, byte[] data)
    {
        _iconData[path] = data;
    }

    public void SetThrowOnExtract(string path)
    {
        _throwingPaths.Add(path);
    }

    public byte[]? ExtractIconBytes(string path)
    {
        if (_throwingPaths.Contains(path))
        {
            throw new Exception($"Simulated extraction failure for {path}");
        }

        return _iconData.TryGetValue(path, out var data) ? data : null;
    }

    public int PruneCache(IEnumerable<string> activePaths)
    {
        return 0;
    }
}
