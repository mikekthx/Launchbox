using Microsoft.UI.Xaml;

namespace Launchbox;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine($"UNHANDLED EXCEPTION: {e.Exception}");
        // Prevent crash if possible, though for WinUI 3 unhandled exceptions often terminate anyway
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        // This MUST be here for the tray icon to receive input
        _window.Activate();
    }
}
