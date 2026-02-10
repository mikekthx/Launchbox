using Microsoft.UI.Xaml;

namespace Launchbox;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        // This MUST be here for the tray icon to receive input
        _window.Activate();
    }
}
