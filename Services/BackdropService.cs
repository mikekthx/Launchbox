using Launchbox.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Launchbox.Services;

public class BackdropService : IBackdropService
{
    private readonly Window _window;
    private DateTime _lastBackdropCheck = DateTime.MinValue;
    private bool _isDwmBlurGlassRunning = false;
    private static readonly TimeSpan BackdropCheckInterval = TimeSpan.FromSeconds(60);

    public BackdropService(Window window)
    {
        _window = window;
    }

    public async Task UpdateBackdropAsync()
    {
        try
        {
            if (DateTime.Now - _lastBackdropCheck >= BackdropCheckInterval)
            {
                _lastBackdropCheck = DateTime.Now;
                _isDwmBlurGlassRunning = await Task.Run(() =>
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(Constants.DWM_BLUR_GLASS_PROCESS_NAME);
                        try
                        {
                            return processes.Length > 0;
                        }
                        finally
                        {
                            foreach (var p in processes) p.Dispose();
                        }
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
                if (_window.SystemBackdrop != null)
                {
                    _window.SystemBackdrop = null;
                }
            }
            else
            {
                // Default behavior
                if (_window.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    _window.SystemBackdrop = new DesktopAcrylicBackdrop();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for DWMBlurGlass: {ex.Message}");
            // Fallback to default
            if (_window.SystemBackdrop is not DesktopAcrylicBackdrop)
            {
                _window.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
    }
}
