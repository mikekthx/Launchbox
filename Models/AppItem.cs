using Launchbox.Helpers;

namespace Launchbox.Models;

public class AppItem : ObservableObject
{
    private object? _icon;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
}
