using Launchbox.Helpers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Launchbox.Services;

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
        _processService = processService;
        _windowWrapper = windowWrapper;
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
                    catch
                    {
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
                // Default behavior
                if (!_windowWrapper.IsDesktopAcrylicBackdropSet)
                {
                    _windowWrapper.SetDesktopAcrylicBackdrop();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for DWMBlurGlass: {ex.Message}");
            // Fallback to default
            if (!_windowWrapper.IsDesktopAcrylicBackdropSet)
            {
                _windowWrapper.SetDesktopAcrylicBackdrop();
            }
        }
    }
}
