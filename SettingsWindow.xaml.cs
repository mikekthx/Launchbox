using Launchbox.Services;
using Launchbox.ViewModels;
using Microsoft.UI.Xaml;

namespace Launchbox;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsService settingsService, IWindowService windowService, IFilePickerService filePickerService)
    {
        ViewModel = new SettingsViewModel(settingsService, windowService, filePickerService);

        this.InitializeComponent();

        this.Title = "Launchbox Settings";
        this.ExtendsContentIntoTitleBar = true;
    }
}
