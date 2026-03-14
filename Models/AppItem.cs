using Launchbox.Helpers;

namespace Launchbox.Models;

public class AppItem : ObservableObject
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private object? _icon;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public override string ToString() => $"{Name} ({Path})";
}
