using System;
using System.IO;
using System.Linq;

string path = "Launchbox.Tests/ProcessStarterSecurityTests.cs";
string[] lines = File.ReadAllLines(path);
var usingLines = lines.Where(l => l.StartsWith("using ")).OrderBy(l => l).ToList();
var otherLines = lines.Where(l => !l.StartsWith("using ")).ToList();
File.WriteAllLines(path, usingLines.Concat(otherLines).ToArray());
