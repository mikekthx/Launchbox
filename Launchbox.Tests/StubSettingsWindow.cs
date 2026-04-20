using Launchbox.Services;
using Microsoft.UI.Xaml;

namespace Launchbox;

// Compilation stub — SettingsWindow is referenced by WindowService but never instantiated
// via the internal test constructor (which has no IFilePickerService).
internal sealed partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService) { }
}
