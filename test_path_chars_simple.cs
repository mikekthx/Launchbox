using System;
using System.IO;

class Program {
    static void Main() {
        string[] paths = { "path|with|pipe", "path<with<bracket", "path>with>bracket", "path\"with\"quote" };
        foreach (var p in paths) {
            Console.WriteLine($"Testing: {p}");
            try {
                // On Linux .NET Core, many of these characters are valid in filenames,
                // unlike Windows. This test script might pass on Linux but fail on Windows.
                // However, we need to know what happens in the CI env (Windows).
                Path.GetFullPath(p);
                Console.WriteLine("  Path.GetFullPath succeeded (Linux allows this?)");
            } catch (Exception ex) {
                Console.WriteLine($"  Path.GetFullPath failed: {ex.GetType().Name}");
            }
        }
    }
}
