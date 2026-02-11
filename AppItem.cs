using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Launchbox;

public class AppItem : INotifyPropertyChanged
{
    private object? _icon;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public object? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
