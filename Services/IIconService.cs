using System.Collections.Generic;

namespace Launchbox.Services;

public interface IIconService
{
    byte[]? ExtractIconBytes(string path);
    int PruneCache(IEnumerable<string> activePaths);
}
