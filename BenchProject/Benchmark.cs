using System;
using System.Collections.Concurrent;
using System.Diagnostics;

class FileSystem
{
    public int GetLastWriteTime(string p) => 1;
}

class IconService
{
    private FileSystem _fileSystem = new FileSystem();
    private ConcurrentDictionary<string, Lazy<(int, DateTime)>> _fileTimestampCache = new();

    public void Warmup()
    {
        _fileTimestampCache.TryAdd("test", new Lazy<(int, DateTime)>(() => (1, DateTime.UtcNow)));
    }

    public void RunUnoptimized(int iterations)
    {
        string path = "test";
        for (int i = 0; i < iterations; i++)
        {
            _fileTimestampCache.GetOrAdd(path, p => new Lazy<(int, DateTime)>(() =>
            {
                return (_fileSystem.GetLastWriteTime(p), DateTime.UtcNow);
            }));
        }
    }

    public void RunOptimized(int iterations)
    {
        string path = "test";
        for (int i = 0; i < iterations; i++)
        {
            _fileTimestampCache.GetOrAdd(path, static (p, fs) => new Lazy<(int, DateTime)>(() => (fs.GetLastWriteTime(p), DateTime.UtcNow)), _fileSystem);
        }
    }
}

class Program
{
    static void Main()
    {
        var service = new IconService();
        service.Warmup();

        long unoptimizedAllocations = 0;
        var sw = new Stopwatch();

        // Warmup
        service.RunUnoptimized(10000);

        long memBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Start();
        service.RunUnoptimized(10000000);
        sw.Stop();
        long memAfter = GC.GetAllocatedBytesForCurrentThread();
        unoptimizedAllocations = memAfter - memBefore;

        Console.WriteLine($"Unoptimized (Cache Hit): {sw.ElapsedMilliseconds}ms, Allocated: {unoptimizedAllocations:N0} bytes");

        // Optimized
        service.RunOptimized(10000);

        memBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();
        service.RunOptimized(10000000);
        sw.Stop();
        memAfter = GC.GetAllocatedBytesForCurrentThread();
        long optimizedAllocations = memAfter - memBefore;

        Console.WriteLine($"Optimized (Cache Hit): {sw.ElapsedMilliseconds}ms, Allocated: {optimizedAllocations:N0} bytes");
    }
}
