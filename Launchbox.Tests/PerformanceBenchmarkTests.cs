using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Launchbox.Helpers;

namespace Launchbox.Tests;

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
                if (dims == null)
                {
                    throw new Exception("Parsing failed");
                }
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

    private byte[] CreatePng(int width, int height)
    {
        var header = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // Sig
            0x00, 0x00, 0x00, 0x0D, // IHDR Len
            0x49, 0x48, 0x44, 0x52  // IHDR Type
            // ... (Width/Height set below)
        };

        // Pad to > 24 bytes
        var result = new byte[100];
        Array.Copy(header, result, header.Length);

        // Header layout:
        // 0-7: Sig
        // 8-11: Len
        // 12-15: Type (IHDR)
        // 16-19: Width (Big Endian)
        // 20-23: Height (Big Endian)

        result[16] = (byte)((width >> 24) & 0xFF);
        result[17] = (byte)((width >> 16) & 0xFF);
        result[18] = (byte)((width >> 8) & 0xFF);
        result[19] = (byte)(width & 0xFF);

        result[20] = (byte)((height >> 24) & 0xFF);
        result[21] = (byte)((height >> 16) & 0xFF);
        result[22] = (byte)((height >> 8) & 0xFF);
        result[23] = (byte)(height & 0xFF);

        return result;
    }
}
