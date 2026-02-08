using Microsoft.UI.Xaml;

namespace Launchbox
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            // This MUST be here for the tray icon to receive input
            m_window.Activate();
        }

        private Window? m_window;
    }
}