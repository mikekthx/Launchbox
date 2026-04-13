using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        var paths = new List<string>();
        for(int i=0; i<100; i++)
        {
            paths.Add(@"%USERPROFILE%\Documents\Folder" + (i % 20));
        }

        // Warmup
        for(int i=0; i<10; i++)
        {
            var r = paths
                .Where(f => !string.IsNullOrEmpty(f))
                .Where(f => Environment.ExpandEnvironmentVariables(f).Length > 0)
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        var sw = Stopwatch.StartNew();
        for(int i=0; i<10000; i++)
        {
            var r = paths
                .Where(f => !string.IsNullOrEmpty(f))
                .Where(f => Environment.ExpandEnvironmentVariables(f).Length > 0)
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        sw.Stop();
        Console.WriteLine($"Baseline (Inside Where): {sw.Elapsed.TotalMilliseconds} ms");

        sw.Restart();
        for(int i=0; i<10000; i++)
        {
            var r = paths
                .Where(f => !string.IsNullOrEmpty(f))
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(f => Environment.ExpandEnvironmentVariables(f).Length > 0)
                .ToList();
        }
        sw.Stop();
        Console.WriteLine($"Hoist (After GroupBy): {sw.Elapsed.TotalMilliseconds} ms");
    }
}
