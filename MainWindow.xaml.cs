using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using WinIcon = System.Drawing.Icon;

namespace Launchbox
{
    public sealed partial class MainWindow : Window
    {
        private readonly string ShortcutFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Shortcuts");
        public ObservableCollection<AppItem> Apps { get; } = new();

        private const int MOD_ALT = 0x0001;
        private const int VK_S = 0x53;
        private const int HOTKEY_ID = 9000;
        private const int SW_RESTORE = 9;

        private static readonly string[] ALLOWED_EXTENSIONS = { ".lnk", ".url" };

        private static WndProcDelegate? _wndProcDelegate;
        private readonly IntPtr oldWndProc;
        private bool _hasPositioned = false;
        private ScrollViewer? _internalScrollViewer;

        // Window dragging state
        private bool _isDraggingWindow = false;
        private Windows.Graphics.PointInt32 _dragStartWindowPos;
        private Windows.Foundation.Point _dragStartPointerPos;

        public System.Windows.Input.ICommand ToggleWindowCommand { get; }
        public System.Windows.Input.ICommand ExitCommand { get; }
        public System.Windows.Input.ICommand ResetPositionCommand { get; }

        public MainWindow()
        {
            this.InitializeComponent();

            ToggleWindowCommand = new SimpleCommand(ToggleWindowVisibility);
            ExitCommand = new SimpleCommand(ExitApplication);
            ResetPositionCommand = new SimpleCommand(ResetWindowPosition);

            // 1. WINDOW SETUP
            this.ExtendsContentIntoTitleBar = true;
            this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            this.AppWindow.IsShownInSwitchers = false;

            // 2. WINDOW DRAGGING - Use custom pointer tracking
            RootGrid.PointerPressed += RootGrid_PointerPressed;
            RootGrid.PointerMoved += RootGrid_PointerMoved;
            RootGrid.PointerReleased += RootGrid_PointerReleased;

            // 3. SCROLL SHOULD WORK NATIVELY NOW
            AppGrid.Loaded += (s, e) =>
            {
                FindScrollViewer(AppGrid);
                System.Diagnostics.Debug.WriteLine($"AppGrid loaded. Scrollable height: {_internalScrollViewer?.ScrollableHeight ?? 0}");
            };

            // 4. START OFF-SCREEN
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(650, 700));
            appWindow.Move(new Windows.Graphics.PointInt32(-10000, -10000));
            appWindow.Changed += AppWindow_Changed;

            // 5. EVENT HOOKS
            this.Activated += MainWindow_Activated;

            if (!RegisterHotKey(hWnd, HOTKEY_ID, MOD_ALT, VK_S))
            {
                System.Diagnostics.Debug.WriteLine("Failed to register Alt+S hotkey.");
            }

            _wndProcDelegate = new WndProcDelegate(NewWndProc);
            oldWndProc = SetWindowLongPtr(hWnd, -4, _wndProcDelegate);
            if (oldWndProc == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Failed to set WndProc hook.");
            }

            // 6. LOAD APPS
            AppGrid.ItemsSource = Apps;
            _ = LoadAppsAsync();
        }

        // --- WINDOW DRAGGING ---
        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Check if we're clicking inside a GridViewItem
            var clickedElement = e.OriginalSource as DependencyObject;
            if (clickedElement != null)
            {
                var parent = clickedElement;
                int depth = 0;
                while (parent != null && parent != RootGrid && depth < 20)
                {
                    if (parent is GridViewItem)
                    {
                        // Clicked on an item, don't start dragging
                        return;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                    depth++;
                }
            }

            // Start dragging - capture pointer and record initial positions
            _isDraggingWindow = true;
            _dragStartWindowPos = this.AppWindow.Position;
            _dragStartPointerPos = e.GetCurrentPoint(null).Position;
            RootGrid.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingWindow) return;

            var currentPointerPos = e.GetCurrentPoint(null).Position;
            var deltaX = (int)(currentPointerPos.X - _dragStartPointerPos.X);
            var deltaY = (int)(currentPointerPos.Y - _dragStartPointerPos.Y);

            var newX = _dragStartWindowPos.X + deltaX;
            var newY = _dragStartWindowPos.Y + deltaY;

            this.AppWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            e.Handled = true;
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDraggingWindow)
            {
                _isDraggingWindow = false;
                RootGrid.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        // --- SCROLL LOGIC ---
        private bool FindScrollViewer(DependencyObject root)
        {
            if (root == null) return false;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer sv)
                {
                    _internalScrollViewer = sv;
                    _internalScrollViewer.VerticalScrollMode = ScrollMode.Enabled;
                    _internalScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    System.Diagnostics.Debug.WriteLine($"ScrollViewer found! Scrollable height: {sv.ScrollableHeight}");
                    return true;
                }
                if (FindScrollViewer(child))
                {
                    return true;
                }
            }
            return false;
        }

        // --- WINDOW LOGIC ---
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (_hasPositioned && (args.DidPositionChange || args.DidSizeChange))
            {
                SaveWindowPosition();
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                this.AppWindow.Hide();
            }
        }

        private void ExitApplication()
        {
            try
            {
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                UnregisterHotKey(hWnd, HOTKEY_ID);
                TrayIcon?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during exit: {ex.Message}");
            }
            this.Close();
        }

        private void ResetWindowPosition()
        {
            try
            {
                _hasPositioned = true;
                CenterWindow();
                this.AppWindow.Show();
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetForegroundWindow(hWnd);
                SaveWindowPosition();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset window position: {ex.Message}");
            }
        }

        private void SaveWindowPosition()
        {
            try
            {
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                var pos = this.AppWindow.Position;
                var size = this.AppWindow.Size;
                settings["WinX"] = pos.X;
                settings["WinY"] = pos.Y;
                settings["WinW"] = size.Width;
                settings["WinH"] = size.Height;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save window position: {ex.Message}");
            }
        }

        private bool RestoreWindowPosition()
        {
            try
            {
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                if (settings.TryGetValue("WinX", out var winX) &&
                    settings.TryGetValue("WinY", out var winY) &&
                    settings.TryGetValue("WinW", out var winW) &&
                    settings.TryGetValue("WinH", out var winH) &&
                    winX is int x && winY is int y && winW is int w && winH is int h)
                {
                    var rect = new Windows.Graphics.RectInt32(x, y, w, h);
                    var displayArea = DisplayArea.GetFromRect(rect, DisplayAreaFallback.None);
                    if (displayArea != null)
                    {
                        this.AppWindow.MoveAndResize(rect);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore window position: {ex.Message}");
            }
            return false;
        }

        private void ToggleWindowVisibility()
        {
            try
            {
                if (this.Visible && _hasPositioned)
                {
                    this.AppWindow.Hide();
                }
                else
                {
                    if (!_hasPositioned)
                    {
                        _hasPositioned = true;
                        if (!RestoreWindowPosition()) CenterWindow();
                    }
                    this.AppWindow.Show();
                    IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to toggle window visibility: {ex.Message}");
            }
        }

        private void CenterWindow()
        {
            try
            {
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                appWindow.Resize(new Windows.Graphics.SizeInt32(650, 700));
                var x = (displayArea.WorkArea.Width - 650) / 2;
                var y = (displayArea.WorkArea.Height - 700) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to center window: {ex.Message}");
            }
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr NewWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            const uint WM_HOTKEY = 0x0312;
            const uint WM_NCLBUTTONDBLCLK = 0x00A3;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindowVisibility();
                return IntPtr.Zero;
            }
            if (msg == WM_NCLBUTTONDBLCLK) return IntPtr.Zero;

            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        // --- WIN32 IMPORTS ---
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate dwNewLong)
        {
            if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        // --- APP LOADING ---
        private async Task LoadAppsAsync()
        {
            if (!Directory.Exists(ShortcutFolder))
            {
                System.Diagnostics.Debug.WriteLine($"Shortcut folder not found: {ShortcutFolder}");
                return;
            }

            var files = Directory.GetFiles(ShortcutFolder)
                .Where(f => ALLOWED_EXTENSIONS.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var iconBytes = await Task.Run(() => ExtractIconBytes(file));
                    BitmapImage? icon = null;
                    if (iconBytes != null)
                    {
                        icon = await CreateBitmapImageAsync(iconBytes);
                    }
                    Apps.Add(new AppItem { Name = name, Path = file, Icon = icon });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load app {file}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {Apps.Count} apps");

            await Task.Delay(100);
            if (_internalScrollViewer != null)
            {
                _internalScrollViewer.UpdateLayout();
                System.Diagnostics.Debug.WriteLine($"After loading - Scrollable height: {_internalScrollViewer.ScrollableHeight}");
            }
        }

        private void AppGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                string extension = Path.GetExtension(app.Path).ToLowerInvariant();
                if (!ALLOWED_EXTENSIONS.Contains(extension))
                {
                    System.Diagnostics.Debug.WriteLine($"Blocked execution of unauthorized file: {app.Path}");
                    return;
                }

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app.Path) { UseShellExecute = true });
                    this.AppWindow.Hide();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to launch {app.Path}: {ex.Message}");
                }
            }
        }

        // --- ICON EXTRACTION ---
        private byte[]? ExtractIconBytes(string path)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    string iconFile = GetIniValue(path, "InternetShortcut", "IconFile");
                    if (File.Exists(iconFile)) path = iconFile;
                }

                PrivateExtractIcons(path, 0, 128, 128, ref hIcon, IntPtr.Zero, 1, 0);
                if (hIcon == IntPtr.Zero) return null;

                using var icon = WinIcon.FromHandle(hIcon);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract icon for {path}: {ex.Message}");
                return null;
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                    DestroyIcon(hIcon);
            }
        }

        private async Task<BitmapImage?> CreateBitmapImageAsync(byte[] imageBytes)
        {
            try
            {
                var image = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream.GetOutputStreamAt(0));
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                stream.Seek(0);
                await image.SetSourceAsync(stream);
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create BitmapImage: {ex.Message}");
                return null;
            }
        }

        [DllImport("user32.dll")] private static extern uint PrivateExtractIcons(string l, int n, int cx, int cy, ref IntPtr p, IntPtr id, uint ni, uint fl);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("kernel32.dll")] private static extern int GetPrivateProfileString(string s, string k, string d, System.Text.StringBuilder r, int z, string f);

        private string GetIniValue(string p, string s, string k)
        {
            var sb = new System.Text.StringBuilder(255);
            GetPrivateProfileString(s, k, "", sb, 255, p);
            return sb.ToString();
        }
    }

    // --- HELPER CLASSES ---
    public class AppItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public BitmapImage? Icon { get; set; }
    }

    public class SimpleCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public SimpleCommand(Action action) => _action = action;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}