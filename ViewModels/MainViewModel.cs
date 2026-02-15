using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Launchbox.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ShortcutService _shortcutService;
    private readonly IconService _iconService;
    private readonly IImageFactory _imageFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IAppLauncher _appLauncher;
    private readonly IFileSystem _fileSystem;
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;

    public ObservableCollection<AppItem> Apps { get; } = new();

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set
        {
            if (_isEmpty != value)
            {
                _isEmpty = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand LoadAppsCommand { get; }
    public ICommand LaunchAppCommand { get; }
    public ICommand OpenShortcutsFolderCommand { get; }
    public ICommand ToggleWindowCommand { get; }

    public MainViewModel(
        ShortcutService shortcutService,
        IconService iconService,
        IImageFactory imageFactory,
        IDispatcher dispatcher,
        IAppLauncher appLauncher,
        IFileSystem fileSystem,
        SettingsService settingsService,
        IWindowService windowService)
    {
        _shortcutService = shortcutService;
        _iconService = iconService;
        _imageFactory = imageFactory;
        _dispatcher = dispatcher;
        _appLauncher = appLauncher;
        _fileSystem = fileSystem;
        _settingsService = settingsService;
        _windowService = windowService;

        _settingsService.PropertyChanged += SettingsService_PropertyChanged;

        LoadAppsCommand = new SimpleCommand(async () => await LoadAppsAsync());
        LaunchAppCommand = new SimpleCommand(LaunchApp);
        OpenShortcutsFolderCommand = new SimpleCommand(OpenShortcutsFolder);
        ToggleWindowCommand = new SimpleCommand(() => _windowService.ToggleVisibility());
    }

    private void SettingsService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.ShortcutsPath))
        {
            // Reload apps when folder path changes
            _ = LoadAppsAsync();
        }
    }

    public async Task LoadAppsAsync()
    {
        var shortcutFolder = _settingsService.ShortcutsPath;
        var files = await Task.Run(() => _shortcutService.GetShortcutFiles(shortcutFolder, Constants.ALLOWED_EXTENSIONS));

        var localAppItems = new List<AppItem>();

        if (files != null)
        {
            foreach (var file in files)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var appItem = new AppItem { Name = name, Path = file };
                    localAppItems.Add(appItem);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to load app {file}: {ex.Message}");
                }
            }
        }
        else
        {
            Trace.WriteLine($"Shortcut folder not found: {shortcutFolder}");
        }

        await _dispatcher.EnqueueAsync(() =>
        {
             Apps.Clear();
             foreach(var item in localAppItems) Apps.Add(item);
             IsEmpty = Apps.Count == 0;
             return Task.CompletedTask;
        });

        await Parallel.ForEachAsync(localAppItems, async (item, ct) =>
        {
            try
            {
                var iconBytes = _iconService.ExtractIconBytes(item.Path);
                if (iconBytes != null)
                {
                    await _dispatcher.EnqueueAsync(async () =>
                    {
                        var image = await _imageFactory.CreateImageAsync(iconBytes);
                        item.Icon = image;
                    });
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to load icon for {item.Path}: {ex.Message}");
            }
        });
    }

    private void LaunchApp(object? parameter)
    {
        if (parameter is AppItem app)
        {
            _windowService.Hide();
            _appLauncher.Launch(app.Path);
        }
    }

    private void OpenShortcutsFolder()
    {
        var shortcutFolder = _settingsService.ShortcutsPath;
        if (!_fileSystem.DirectoryExists(shortcutFolder))
        {
            _fileSystem.CreateDirectory(shortcutFolder);
        }
        _appLauncher.OpenFolder(shortcutFolder);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
