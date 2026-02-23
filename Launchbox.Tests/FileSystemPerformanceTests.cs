using Launchbox.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Launchbox.Tests;

public class FileSystemPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public FileSystemPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_GetIniValue_Performance()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _output.WriteLine("Skipping Windows-specific benchmark on non-Windows platform.");
            return;
        }

        // Setup
        string tempFile = Path.GetTempFileName();
        string section = "Benchmark";
        string key = "Key1";
        string value = new string('A', 500); // 500 chars fits in 512 buffer
        string longValue = new string('B', 2000); // 2000 chars needs resize from 512, fits in 4096

        try
        {
            // Write INI file manually
            File.WriteAllText(tempFile, $"[{section}]\r\n{key}={value}\r\nKey2={longValue}\r\n");

            var fileSystem = new FileSystem();

            // Warmup
            fileSystem.GetIniValue(tempFile, section, key);

            int iterations = 1000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var v1 = fileSystem.GetIniValue(tempFile, section, key);
                Assert.Equal(value, v1);

                var v2 = fileSystem.GetIniValue(tempFile, section, "Key2");
                Assert.Equal(longValue, v2);
            }

            sw.Stop();
            _output.WriteLine($"Time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");
            double avgTime = (double)sw.ElapsedMilliseconds / iterations;
            _output.WriteLine($"Average time per iteration: {avgTime:F4} ms");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
