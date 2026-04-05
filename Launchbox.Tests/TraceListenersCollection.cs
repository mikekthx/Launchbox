using Xunit;

namespace Launchbox.Tests;

[CollectionDefinition("TraceListeners", DisableParallelization = true)]
public class TraceListenersCollection
{
    // Disable parallelization because tests in this collection mutate the global
    // Trace.Listeners state. Running them concurrently would interleave trace output
    // and cause flaky assertions when capturing logs via TextWriterTraceListener.
}
