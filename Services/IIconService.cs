using System.Collections.Generic;

namespace Launchbox.Services;

public interface IIconService
{
    byte[]? ExtractIconBytes(string path, System.Threading.CancellationToken ct = default);
    int PruneCache(IEnumerable<string> activePaths);
}
