using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Launchbox.Tests;

[Trait("Category", "Performance")]
public class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_ImageHeaderParser_IsFast()
    {
        // 1. Setup: Create a minimal valid PNG header in memory (512x512)
        int width = 512;
        int height = 512;
        byte[] pngBytes = CreatePng(width, height);

        int iterations = 10000;

        // 2. Measure ImageHeaderParser
        var swParser = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using (var ms = new MemoryStream(pngBytes))
            {
                var dims = ImageHeaderParser.GetPngDimensions(ms);
                // Force evaluation
                if (dims == null) throw new Exception("Parsing failed");
            }
        }
        swParser.Stop();

        long elapsedMs = swParser.ElapsedMilliseconds;
        double perOp = (double)elapsedMs * 1000 / iterations; // microseconds

        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"ImageHeaderParser Total Time: {elapsedMs} ms");
        _output.WriteLine($"Time per op: {perOp:F2} us");

        // Assert that Parser is very fast (e.g., < 0.05ms per op -> 50us)
        // 10,000 ops should be < 500ms easily.
        Assert.True(elapsedMs < 500, $"ImageHeaderParser is too slow! Took {elapsedMs}ms for {iterations} iterations.");
    }

    private static byte[] CreatePng(int width, int height) => TestDataHelpers.CreatePng(width, height);
}
