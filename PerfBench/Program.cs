using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PerfBench
{
    class Program
    {
        static void Main(string[] args)
        {
            var files = new List<string>();
            for (int i = 0; i < 10000; i++)
            {
                files.Add($"file{i}.txt");
                files.Add($"file{i}.lnk");
                files.Add($"file{i}.url");
                files.Add($"file{i}.exe");
                files.Add($"file{i}.jpg");
            }

            var allowedExtensions = new List<string> { ".lnk", ".url", ".desktop", ".app", ".bat", ".cmd" };

            // Baseline
            var sw = Stopwatch.StartNew();
            for(int i = 0; i < 100; i++)
            {
                var result = files.Where(f => allowedExtensions.Contains(Path.GetExtension(f) ?? string.Empty, StringComparer.OrdinalIgnoreCase)).ToArray();
            }
            sw.Stop();
            Console.WriteLine($"Baseline: {sw.ElapsedMilliseconds} ms");

            // Optimized
            var hashSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
            sw.Restart();
            for(int i = 0; i < 100; i++)
            {
                var result = files.Where(f => hashSet.Contains(Path.GetExtension(f) ?? string.Empty)).ToArray();
            }
            sw.Stop();
            Console.WriteLine($"Optimized: {sw.ElapsedMilliseconds} ms");
        }
    }
}
