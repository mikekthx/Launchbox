using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Launchbox.Services;

// DWMBlurGlass is a third-party tool that applies custom window blur effects.
// When active, it conflicts with WinUI's system acrylic backdrop, so we detect it
// and skip applying our own backdrop to let DWMBlurGlass handle transparency.
public class BackdropService : IBackdropService
{
    private readonly IBackdropWindowWrapper _windowWrapper;
    private readonly IProcessService _processService;
    private readonly Func<DateTime> _timeProvider;

    private DateTime _lastBackdropCheck = DateTime.MinValue;
    private bool _isDwmBlurGlassRunning = false;
    private static readonly TimeSpan BACKDROP_CHECK_INTERVAL = TimeSpan.FromSeconds(60);

    public BackdropService(
        IProcessService processService,
        IBackdropWindowWrapper windowWrapper,
        Func<DateTime>? timeProvider = null)
    {
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _windowWrapper = windowWrapper ?? throw new ArgumentNullException(nameof(windowWrapper));
        _timeProvider = timeProvider ?? (() => DateTime.Now);
    }

    public async Task UpdateBackdropAsync()
    {
        try
        {
            var now = _timeProvider();
            if (now - _lastBackdropCheck >= BACKDROP_CHECK_INTERVAL)
            {
                _lastBackdropCheck = now;
                _isDwmBlurGlassRunning = await Task.Run(() =>
                {
                    try
                    {
                        return _processService.IsProcessRunning(Constants.DWM_BLUR_GLASS_PROCESS_NAME);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error checking if process {Constants.DWM_BLUR_GLASS_PROCESS_NAME} is running: {PathSecurity.GetSafeExceptionMessage(ex)}");
                        return false;
                    }
                });
            }

            if (_isDwmBlurGlassRunning)
            {
                // DWMBlurGlass detected, disable system backdrop to let it handle transparency
                if (_windowWrapper.IsBackdropSet)
                {
                    _windowWrapper.ClearBackdrop();
                }
            }
            else
            {
                // DWMBlurGlass not running — apply system acrylic backdrop.
                if (!_windowWrapper.IsDesktopAcrylicBackdropSet)
                {
                    _windowWrapper.SetDesktopAcrylicBackdrop();
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error updating backdrop: {PathSecurity.GetSafeExceptionMessage(ex)}");

            // Attempt acrylic as a fallback, but isolate this so a second failure
            // (e.g., unsupported system) doesn't propagate out of the method.
            try
            {
                if (!_windowWrapper.IsDesktopAcrylicBackdropSet)
                {
                    _windowWrapper.SetDesktopAcrylicBackdrop();
                }
            }
            catch (Exception fallbackEx)
            {
                Trace.WriteLine($"Fallback backdrop also failed: {PathSecurity.GetSafeExceptionMessage(fallbackEx)}");
            }
        }
    }
}
