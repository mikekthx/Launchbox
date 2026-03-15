using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

class Program
{
    static void Main()
    {
        var cache = new ConcurrentDictionary<string, int>();
        for (int i = 0; i < 10000; i++) cache[i.ToString()] = i;

        var activePaths = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 5000; i++) activePaths.Add(i.ToString());

        // Warmup
        int count1 = 0;
        foreach (var key in cache.Keys) if (!activePaths.Contains(key)) count1++;
        int count2 = 0;
        foreach (var kvp in cache) if (!activePaths.Contains(kvp.Key)) count2++;

        var sw = new Stopwatch();

        sw.Restart();
        long mem1 = GC.GetAllocatedBytesForCurrentThread();
        for(int i=0; i<1000; i++)
        {
            foreach (var key in cache.Keys) if (!activePaths.Contains(key)) { }
        }
        long alloc1 = GC.GetAllocatedBytesForCurrentThread() - mem1;
        sw.Stop();
        var time1 = sw.ElapsedMilliseconds;

        sw.Restart();
        long mem2 = GC.GetAllocatedBytesForCurrentThread();
        for(int i=0; i<1000; i++)
        {
            foreach (var kvp in cache) if (!activePaths.Contains(kvp.Key)) { }
        }
        long alloc2 = GC.GetAllocatedBytesForCurrentThread() - mem2;
        sw.Stop();
        var time2 = sw.ElapsedMilliseconds;

        Console.WriteLine($"Baseline (.Keys): {time1}ms, Allocated: {alloc1} bytes");
        Console.WriteLine($"Optimized (direct): {time2}ms, Allocated: {alloc2} bytes");
    }
}
