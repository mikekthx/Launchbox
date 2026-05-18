using Launchbox.Helpers;
using SharpFuzz;
using System.IO;
using System.Text;

// Single entry point: the first byte (mod 5) selects which code path to exercise so
// one binary covers all five security-sensitive targets. libFuzzer drives
// input generation; the CI workflow replays a seed corpus for regression.
Fuzzer.LibFuzzer.Run((ReadOnlySpan<byte> data) =>
{
    if (data.IsEmpty) return;

    int tag = data[0];
    var payload = data[1..];

    switch (tag % 5)
    {
        case 0:
            PathSecurity.ContainsUncPath(Encoding.UTF8.GetString(payload));
            break;
        case 1:
            PathSecurity.IsUnsafePath(Encoding.UTF8.GetString(payload));
            break;
        case 2:
            PathSecurity.RedactPath(Encoding.UTF8.GetString(payload));
            break;
        case 3:
            ImageHeaderParser.GetPngDimensions(new MemoryStream(payload.ToArray()));
            break;
        case 4:
            ImageHeaderParser.GetMaxIcoDimensions(new MemoryStream(payload.ToArray()));
            break;
    }
});
