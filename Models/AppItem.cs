using CommunityToolkit.Mvvm.ComponentModel;

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

    /// <summary>
    /// The extracted icon image. Typed as <see cref="object"/> to keep this model WinUI-free,
    /// enabling unit testing without a WinUI application host. The view casts it to
    /// <c>ImageSource</c> via <c>{x:Bind (media:ImageSource)Icon}</c>.
    /// </summary>
    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string FolderLabel { get; init; } = string.Empty;

    /// <summary>
    /// Stable identity for grouping — the folder's original path.
    /// Labels can be duplicated across folders; paths cannot.
    /// </summary>
    public string FolderPath { get; init; } = string.Empty;

    public override string ToString() => $"{Name} ({Path})";
}
