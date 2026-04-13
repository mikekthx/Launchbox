using Launchbox.Helpers;
using Launchbox.Services;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Launchbox;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        Localization.SetProvider(new ResourceStringProvider());
        this.UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Trace.WriteLine($"UNHANDLED EXCEPTION (UI thread): {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(e.Exception)}");

        if (IsRecoverable(e.Exception))
        {
            e.Handled = true;
            return;
        }

        // Prevent the default ungraceful crash, then exit cleanly.
        // WinUI's rendering thread may be dead, so use a native MessageBox.
        e.Handled = true;
        string title, message;
        try
        {
            title = Localization.GetString("Error_CriticalTitle");
            message = Localization.GetString("Error_CriticalMessage");
        }
        catch
        {
            // ResourceLoader may not be functional during critical failures
            title = "Launchbox";
            message = "Launchbox encountered a critical error and needs to close.";
        }
        NativeMethods.MessageBox(
            IntPtr.Zero,
            message,
            title,
            NativeMethods.MB_OK | NativeMethods.MB_ICONERROR);
        Environment.Exit(1);
    }

    private static bool IsRecoverable(Exception ex)
    {
        // Only COMException (transient WinUI/WinRT failures such as RPC_E_DISCONNECTED) is
        // genuinely recoverable. Swallowing NullReferenceException, InvalidOperationException,
        // etc. masks real bugs and leaves the app in a broken state.
        return ex is COMException;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Trace.WriteLine($"FATAL (background thread): {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(ex)}");
        }
        else
        {
            Trace.WriteLine("FATAL (background thread): [Unknown Error]");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Trace.WriteLine($"UNOBSERVED TASK: {Launchbox.Helpers.PathSecurity.GetSafeExceptionMessage(e.Exception)}");
        e.SetObserved();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        // This MUST be here for the tray icon to receive input
        _window.Activate();
    }
}
