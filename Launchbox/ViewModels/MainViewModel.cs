using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Launchbox.ViewModels;

public class MainViewModel
{
    private readonly ShortcutService _shortcutService;
    private readonly IconService _iconService;
    private readonly IImageFactory _imageFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IAppLauncher _appLauncher;
    private readonly string _shortcutFolder;

    public ObservableCollection<AppItem> Apps { get; } = new();

    public ICommand LoadAppsCommand { get; }
    public ICommand LaunchAppCommand { get; }

    public MainViewModel(
        ShortcutService shortcutService,
        IconService iconService,
        IImageFactory imageFactory,
        IDispatcher dispatcher,
        IAppLauncher appLauncher,
        string shortcutFolder)
    {
        _shortcutService = shortcutService;
        _iconService = iconService;
        _imageFactory = imageFactory;
        _dispatcher = dispatcher;
        _appLauncher = appLauncher;
        _shortcutFolder = shortcutFolder;

        LoadAppsCommand = new SimpleCommand(async () => await LoadAppsAsync());
        LaunchAppCommand = new SimpleCommand(LaunchApp);
    }

    private async Task LoadAppsAsync()
    {
        var files = await Task.Run(() => _shortcutService.GetShortcutFiles(_shortcutFolder, Constants.ALLOWED_EXTENSIONS));

        if (files == null)
        {
            Trace.WriteLine($"Shortcut folder not found: {_shortcutFolder}");
            return;
        }

        var localAppItems = new List<AppItem>();
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

        _dispatcher.TryEnqueue(() =>
        {
             Apps.Clear();
             foreach(var item in localAppItems) Apps.Add(item);
        });

        await Parallel.ForEachAsync(localAppItems, (item, ct) =>
        {
            try
            {
                var iconBytes = _iconService.ExtractIconBytes(item.Path);
                if (iconBytes != null)
                {
                    _dispatcher.TryEnqueue(async () =>
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
            return ValueTask.CompletedTask;
        });
    }

    private void LaunchApp(object? parameter)
    {
        if (parameter is AppItem app)
        {
            _appLauncher.Launch(app.Path);
        }
    }
}
