using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LiquidGlassWinAirDrop;

public partial class MainWindow : Window
{
    private readonly PreferenceStore _store;
    private readonly FolderCatalog _catalog = new();
    private readonly MainViewModel _model = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string? _preview;
    private Preferences _saved;
    private bool _scanning, _closed;
    private int _folderRevision;
    private readonly Dictionary<Border, Brush> _originalSurfaces = [];
    private System.Windows.Forms.NotifyIcon? _tray;

    public MainWindow(PreferenceStore store, Preferences preferences, string? preview)
    {
        _store = store;
        _saved = preferences;
        _preview = preview;
        InitializeComponent();
        _model.Load(preferences);
        DataContext = _model;
        _catalog.Changed += OnFolderChanged;
        _model.PropertyChanged += (_, e) =>
        {
            if (IsLoaded && e.PropertyName is nameof(MainViewModel.MotionEnabled) or nameof(MainViewModel.HighContrast)) ApplyAppearance();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _catalog.Watch(_saved.ReceiveDirectory);
            await RefreshFilesAsync();
            await RefreshHardwareAsync();
            if (_closed) return;
            ApplyAppearance();
            if (_preview is not null)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                Capture(_preview);
                foreach (var page in new[] { "devices", "settings", "history" })
                {
                    Descendants<RadioButton>(this).First(x => (string?)x.Tag == page).IsChecked = true;
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    Capture(Path.Combine(Path.GetDirectoryName(_preview)!, page + ".png"));
                }
                ReceiveNav.IsChecked = true;
                Width = MinWidth;
                Height = MinHeight;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                Capture(Path.Combine(Path.GetDirectoryName(_preview)!, "compact.png"));
                Close();
            }
            else CreateTray();
        }
        catch (Exception error)
        {
            if (_preview is not null)
            {
                File.WriteAllText(_preview + ".error.txt", error.ToString());
                System.Windows.Application.Current.Shutdown(1);
            }
            else Feedback(error.Message);
        }
    }

    private void CreateTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "LiquidGlassWinAirDrop · 开发预览",
            Icon = IconFactory.CreateIcon(32),
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开 LiquidGlassWinAirDrop", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("打开接收目录", null, (_, _) => Dispatcher.Invoke(() => OpenPath(_saved.ReceiveDirectory)));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        _tray.ContextMenuStrip = menu;
    }

    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Ancestors(source).Any(x => x is System.Windows.Controls.Button)) return;
        if (e.ClickCount == 2) ToggleMaximize(); else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject current)
    {
        for (var next = current; next is not null; next = VisualTreeHelper.GetParent(next)) yield return next;
    }
    private void Minimize(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
    private void Maximize(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void ToggleMaximize() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || ReceivePage is null) return;
        var page = (sender as RadioButton)?.Tag as string ?? "receive";
        ReceivePage.Visibility = page == "receive" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = page == "history" ? Visibility.Visible : Visibility.Collapsed;
        DevicesPage.Visibility = page == "devices" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        (PageTitle.Text, PageSubtitle.Text) = page switch
        {
            "history" => ("文件记录", "每一份文件，都有自己的位置。"),
            "devices" => ("设备与连接", "看清连接状态，再让文件出发。"),
            "settings" => ("偏好设置", "一点个性，恰到好处。"),
            _ => ("接收中心", "把灵感和日常，轻盈地留在这里。")
        };
    }

    private void ShowDevices(object sender, RoutedEventArgs e)
    {
        var devicesNav = Descendants<RadioButton>(this).First(x => (string?)x.Tag == "devices");
        devicesNav.IsChecked = true;
    }

    private async void ScanHardware(object sender, RoutedEventArgs e) => await RefreshHardwareAsync();
    private async Task RefreshHardwareAsync()
    {
        if (_scanning || _closed) return;
        _scanning = true;
        ScanButton.IsEnabled = false;
        ScanButton.Content = "正在检查…";
        try
        {
            var snapshot = await Task.Run(HardwareInspector.Capture);
            if (!_closed)
            {
                _model.UpdateHardware(snapshot);
                Feedback(snapshot.Error ?? "已完成只读检查，未更换驱动或启动无线广播。");
            }
        }
        catch (Exception error) { if (!_closed) Feedback("检查失败：" + error.Message); }
        finally
        {
            _scanning = false;
            if (!_closed) { ScanButton.IsEnabled = true; ScanButton.Content = "⌁  检查连接"; }
        }
    }

    private void OnFolderChanged()
    {
        if (_closed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(async () => await RefreshFilesAsync());
    }

    private async Task RefreshFilesAsync()
    {
        var directory = _saved.ReceiveDirectory;
        var revision = Interlocked.Increment(ref _folderRevision);
        try
        {
            var files = await Task.Run(() => FolderCatalog.Read(directory));
            if (_closed || revision != _folderRevision) return;
            _model.SetFiles(files, SearchBox.Text);
            UpdateEmptyState();
        }
        catch (Exception error) { if (!_closed) Feedback("文件目录读取失败：" + error.Message); }
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _model.Filter(SearchBox.Text);
        UpdateEmptyState();
    }
    private void UpdateEmptyState()
    {
        EmptyHistory.Visibility = _model.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHistory.Text = string.IsNullOrEmpty(SearchBox.Text) ? "还没有文件记录。\n接收目录中的文件会显示在这里。" : "没有找到匹配的文件。";
    }

    private void OpenFolder(object sender, RoutedEventArgs e) => OpenPath(_saved.ReceiveDirectory);
    private void OpenSelectedFile(object sender, MouseButtonEventArgs e)
    {
        if (FilesList.SelectedItem is not FileEntry file) return;
        // Reveal only: received executable/document files are never auto-run.
        if (File.Exists(file.FullPath))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + file.FullPath + "\"" });
        else { Feedback("文件已被移动或删除。"); _ = RefreshFilesAsync(); }
    }
    private void OpenPath(string directory)
    {
        try { Directory.CreateDirectory(directory); Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true }); }
        catch (Exception error) { Feedback(error.Message); }
    }
    private void ChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 LiquidGlassWinAirDrop 接收目录", InitialDirectory = _saved.ReceiveDirectory };
        if (dialog.ShowDialog(this) == true) _model.ReceiveDirectory = dialog.FolderName;
    }
    private async void SaveSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var preferences = PreferenceStore.Validate(_model.Preferences);
            Directory.CreateDirectory(preferences.ReceiveDirectory);
            // Verify a write before switching away from the known working folder.
            var probe = Path.Combine(preferences.ReceiveDirectory, ".liquidglass-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { } }
            finally { if (File.Exists(probe)) File.Delete(probe); }
            _store.Save(preferences);
            _saved = preferences;
            _catalog.Watch(preferences.ReceiveDirectory);
            await RefreshFilesAsync();
            ApplyAppearance();
            Feedback("偏好设置已保存。");
        }
        catch (Exception error) { Feedback("未能保存：" + error.Message); }
    }

    private void ApplyAppearance()
    {
        var animate = _model.MotionEnabled && SystemParameters.ClientAreaAnimation && _preview is null;
        Halo.BeginAnimation(OpacityProperty, null);
        Halo.Opacity = 0.8;
        if (animate) Halo.BeginAnimation(OpacityProperty, new DoubleAnimation(0.45, 0.9, TimeSpan.FromSeconds(4.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        foreach (var border in Descendants<Border>(this).Where(x => x.Style == FindResource("GlassPanel")))
        {
            if (!_originalSurfaces.ContainsKey(border)) _originalSurfaces[border] = border.Background;
            border.Background = _model.HighContrast ? new SolidColorBrush(Color.FromRgb(249, 250, 255)) : _originalSurfaces[border];
        }
        if (!animate) { HaloMotion.X = 0; HaloMotion.Y = 0; }
    }

    private void OnPointerMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_model.MotionEnabled || !SystemParameters.ClientAreaAnimation || _preview is not null) return;
        var position = e.GetPosition(this);
        HaloMotion.X = (position.X / ActualWidth - 0.5) * 20;
        HaloMotion.Y = (position.Y / ActualHeight - 0.5) * 16;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private void Feedback(string message) { if (!_closed) FeedbackText.Text = message; }

    public void Capture(string path)
    {
        UpdateLayout();
        var frame = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        frame.Render(this);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(frame));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        png.Save(stream);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closed = true;
        _lifetime.Cancel();
        _catalog.Dispose();
        _tray?.Dispose();
        _lifetime.Dispose();
    }
}
