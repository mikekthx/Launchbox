using Launchbox.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Launchbox.Tests;

public class WinUIDispatcherTests : IDisposable
{
    private DispatcherQueueController? _controller;
    private DispatcherQueue? _queue;
    private readonly bool _isWindows;

    // P/Invoke for WinUI 3 DispatcherQueueController creation
    [DllImport("Microsoft.ui.dispatching.dll", EntryPoint = "CreateDispatcherQueueController", ExactSpelling = true)]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [Out, MarshalAs(UnmanagedType.IUnknown)] out object dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    private const int DQTYPE_THREAD_DEDICATED = 1;
    private const int DQTAT_COM_STA = 2;

    public WinUIDispatcherTests()
    {
        _isWindows = OperatingSystem.IsWindows();
        if (!_isWindows) return;

        try
        {
            var options = new DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                threadType = DQTYPE_THREAD_DEDICATED,
                apartmentType = DQTAT_COM_STA
            };

            int hr = CreateDispatcherQueueController(options, out object controllerObj);
            if (hr == 0 && controllerObj is DispatcherQueueController controller)
            {
                _controller = controller;
                _queue = _controller.DispatcherQueue;
            }
        }
        catch
        {
            // Ignore init errors in non-UI environments
        }
    }

    public void Dispose()
    {
        if (!_isWindows || _controller == null) return;

        try
        {
            // ShutdownQueueAsync cleanly shuts down the dedicated thread
            _controller.ShutdownQueueAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore shutdown errors in test cleanup
        }
    }

    [Fact]
    public void TryEnqueue_ExecutesActionOnQueue()
    {
        if (!_isWindows || _queue == null) return;

        var dispatcher = new WinUIDispatcher(_queue);
        bool executed = false;
        var manualResetEvent = new ManualResetEvent(false);

        dispatcher.TryEnqueue(() =>
        {
            executed = true;
            manualResetEvent.Set();
        });

        // Wait for the action to be pumped by the thread
        manualResetEvent.WaitOne(TimeSpan.FromSeconds(2));

        Assert.True(executed);
    }

    [Fact]
    public async Task EnqueueAsync_ExecutesAndReturnsTask()
    {
        if (!_isWindows || _queue == null) return;

        var dispatcher = new WinUIDispatcher(_queue);
        bool executed = false;

        await dispatcher.EnqueueAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(executed);
    }

    [Fact]
    public async Task EnqueueAsync_WhenActionThrows_ThrowsException()
    {
        if (!_isWindows || _queue == null) return;

        var dispatcher = new WinUIDispatcher(_queue);
        var expectedMessage = "Test exception from async queue";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.EnqueueAsync(() => throw new InvalidOperationException(expectedMessage)));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Constructor_AcceptsNull_ThrowsOnUsage()
    {
#pragma warning disable CS8625
        var dispatcher = new WinUIDispatcher(null);
#pragma warning restore CS8625

        Assert.Throws<NullReferenceException>(() => dispatcher.TryEnqueue(() => { }));
    }
}
