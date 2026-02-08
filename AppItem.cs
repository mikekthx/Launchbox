using Microsoft.UI.Xaml.Media.Imaging;

namespace Launchbox;

public class AppItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public BitmapImage? Icon { get; set; }
}
