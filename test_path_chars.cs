using System;
using System.IO;

class Program {
    static void Main() {
        string[] paths = { "path|with|pipe", "path<with<bracket", "path>with>bracket", "path\"with\"quote" };
        foreach (var p in paths) {
            try {
                Console.WriteLine($"Testing: {p}");
                Path.GetFullPath(p);
                Console.WriteLine("  Path.GetFullPath succeeded");
            } catch (Exception ex) {
                Console.WriteLine($"  Path.GetFullPath failed: {ex.GetType().Name}");
            }
        }
    }
}
