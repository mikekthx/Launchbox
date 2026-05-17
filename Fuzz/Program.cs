using System.IO;
using System.Text;
using Launchbox.Helpers;
using SharpFuzz;

// Single entry point: the first byte selects which code path to exercise so
// one binary covers all five security-sensitive targets. libFuzzer drives
// input generation; the CI workflow replays a seed corpus for regression.
Fuzzer.LibFuzzer.Run(stream =>
{
    if (stream.Length == 0) return;

    int tag = stream.ReadByte();
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    byte[] data = ms.ToArray();

    switch (tag % 5)
    {
        case 0:
            PathSecurity.ContainsUncPath(Encoding.UTF8.GetString(data));
            break;
        case 1:
            PathSecurity.IsUnsafePath(Encoding.UTF8.GetString(data));
            break;
        case 2:
            PathSecurity.RedactPath(Encoding.UTF8.GetString(data));
            break;
        case 3:
            ImageHeaderParser.GetPngDimensions(new MemoryStream(data));
            break;
        case 4:
            ImageHeaderParser.GetMaxIcoDimensions(new MemoryStream(data));
            break;
    }
});
