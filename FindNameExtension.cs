using Microsoft.UI.Xaml;

namespace Launchbox
{
    public partial class MainWindow : Window
    {
        public object? FindName(string name)
        {
            return (this.Content as FrameworkElement)?.FindName(name);
        }
    }
}
