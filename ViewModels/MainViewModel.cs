using Launchbox.Helpers;
using Launchbox.Models;
using Launchbox.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Launchbox.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IIconService _iconService;
    private readonly IImageFactory _imageFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IAppLauncher _appLauncher;
    private readonly IFileSystem _fileSystem;
    private readonly SettingsService _settingsService;
    private readonly IWindowService _windowService;
    private CancellationTokenSource? _loadCts;

    public BulkObservableCollection<AppItem> Apps { get; } = [];

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public ICommand LoadAppsCommand { get; }
    public ICommand LaunchAppCommand { get; }
    public ICommand OpenShortcutsFolderCommand { get; }
    public ICommand ToggleWindowCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public MainViewModel(
        IShortcutService shortcutService,
        IIconService iconService,
        IImageFactory imageFactory,
        IDispatcher dispatcher,
        IAppLauncher appLauncher,
        IFileSystem fileSystem,
        SettingsService settingsService,
        IWindowService windowService)
    {
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
        _imageFactory = imageFactory ?? throw new ArgumentNullException(nameof(imageFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _appLauncher = appLauncher ?? throw new ArgumentNullException(nameof(appLauncher));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));

        _settingsService.PropertyChanged += SettingsService_PropertyChanged;

        LoadAppsCommand = new AsyncSimpleCommand(LoadAppsAsync);
        LaunchAppCommand = new SimpleCommand(LaunchApp);
        OpenShortcutsFolderCommand = new SimpleCommand(OpenShortcutsFolder);
        ToggleWindowCommand = new SimpleCommand(() => _windowService.ToggleVisibility());
        ExitCommand = new SimpleCommand(() => _windowService.Exit());
        OpenSettingsCommand = new SimpleCommand(() => _windowService.OpenSettings());
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
        // Cancel any in-flight load
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;

        try
        {
            var shortcutFolder = _settingsService.ShortcutsPath;
            var files = await Task.Run(() => _shortcutService.GetShortcutFiles(shortcutFolder, Constants.ALLOWED_EXTENSIONS), ct);

            ct.ThrowIfCancellationRequested();

            _iconService.PruneCache(files ?? []);

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
                        Trace.WriteLine($"Failed to load app {PathSecurity.RedactPath(file)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
                    }
                }
            }
            else
            {
                Trace.WriteLine($"Shortcut folder not found: {PathSecurity.RedactPath(shortcutFolder)}");
            }

            ct.ThrowIfCancellationRequested();

            await _dispatcher.EnqueueAsync(() =>
            {
                Apps.ReplaceAll(localAppItems);
                IsEmpty = Apps.Count == 0;
                return Task.CompletedTask;
            });

            await Parallel.ForEachAsync(localAppItems, ct, async (item, token) =>
            {
                try
                {
                    var iconBytes = _iconService.ExtractIconBytes(item.Path);
                    if (iconBytes != null && !token.IsCancellationRequested)
                    {
                        await _dispatcher.EnqueueAsync(async () =>
                        {
                            var image = await _imageFactory.CreateImageAsync(iconBytes);
                            item.Icon = image;
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to load icon for {PathSecurity.RedactPath(item.Path)}: {PathSecurity.GetSafeExceptionMessage(ex)}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Load was superseded by a newer call -- expected, not an error
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Unexpected error in LoadAppsAsync: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
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
        try
        {
            var shortcutFolder = _settingsService.ShortcutsPath;
            if (!_fileSystem.DirectoryExists(shortcutFolder))
            {
                _fileSystem.CreateDirectory(shortcutFolder);
            }
            _appLauncher.OpenFolder(shortcutFolder);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to open shortcuts folder: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _settingsService.PropertyChanged -= SettingsService_PropertyChanged;
        GC.SuppressFinalize(this);
    }
}
