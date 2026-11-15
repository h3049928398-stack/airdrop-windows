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
    private readonly string? _preview;
    private readonly string? _uiReport;
    private readonly bool _startReceiving;
    private readonly bool _startHidden;
    private readonly WindowsStartup _startup = new(Environment.ProcessPath ?? throw new InvalidOperationException("无法定位应用程序。"));
    private Preferences _saved;
    private bool _scanning, _closed, _exitRequested;
    private int _folderRevision;
    private DispatcherOperation? _backdropRequest;
    private System.Windows.Forms.NotifyIcon? _tray;
    private readonly NativeAirDropReceiver _receiver = new();

    public MainWindow(PreferenceStore store, Preferences preferences, string? preview, string? uiReport = null,bool startReceiving=false,bool startHidden=false)
    {
        _store = store;
        _saved = preferences;
        _preview = preview;
        _uiReport = uiReport;
        _startReceiving=startReceiving;
        _startHidden=startHidden;
        InitializeComponent();
        BottomNavigation.SelectionChanged += Navigate;
        _model.Load(preferences);
        DataContext = _model;
        _catalog.Changed += OnFolderChanged;
        _receiver.StateChanged += message =>
        {
            if(!_closed) Dispatcher.BeginInvoke(() => { if(!_closed) { _model.ReceiverStatus=message; ScanButton.Content=_receiver.IsRunning?"停止接收":"开启接收"; Feedback(message); } });
        };
        _receiver.RequestReceived += request =>
        {
            if(!_closed) Dispatcher.BeginInvoke(async () =>
            {
                if(_closed)return;
                _requests[request.Id]=request;
                if(DateTimeOffset.UtcNow<_controlLease)return;
                ShowFromTray();
                var names=string.Join(Environment.NewLine,request.Files.Take(6));
                var answer=System.Windows.MessageBox.Show(this,"来自："+request.Sender+Environment.NewLine+names+Environment.NewLine+Environment.NewLine+"是否接收这些文件？", "AirDrop 接收请求",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No);
                try { await _receiver.RespondAsync(request.Id,answer==MessageBoxResult.Yes); }
                catch(IOException) { Feedback("接收请求已结束，请从手机重新发送。"); }
            });
        };
        _receiver.FilesReceived += OnFolderChanged;
        _receiver.ProtocolMessage += ControlProtocol;
        _model.PropertyChanged += (_, e) =>
        {
            if (IsLoaded && e.PropertyName is nameof(MainViewModel.MotionEnabled) or nameof(MainViewModel.HighContrast) or nameof(MainViewModel.AlwaysOnTop)) ApplyAppearance();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_preview is null) { WindowPlacement.Dock(this); CreateTray(); }
            if(_preview is null && _uiReport is null)StartControl();
            if(_startHidden) { Hide(); Opacity=1; }
            if(_preview is null && _uiReport is null)
            {
                try { _model.StartWithWindows=_startup.IsEnabled(); }
                catch(Exception error) { Feedback("无法读取开机自启动状态："+error.Message); }
            }
            _catalog.Watch(_saved.ReceiveDirectory);
            await RefreshFilesAsync();
            await RefreshHardwareAsync();
            if (_closed) return;
            ApplyAppearance();
            CaptureBackdrop();
            if(_startReceiving && !_closed)ToggleReceiver(this,new RoutedEventArgs());
            if (_uiReport is not null) { await RunUiCheckAsync(_uiReport); return; }
            if (_preview is not null)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                Capture(_preview);
                foreach (var item in new[] { (Index: 2, Name: "devices"), (Index: 3, Name: "settings"), (Index: 1, Name: "history") })
                {
                    BottomNavigation.Select(item.Index, false);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    CaptureBackdrop();
                    Capture(Path.Combine(Path.GetDirectoryName(_preview)!, item.Name + ".png"));
                }
                BottomNavigation.Select(0, false);
                Width = MinWidth;
                Height = MinHeight;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                CaptureBackdrop();
                Capture(Path.Combine(Path.GetDirectoryName(_preview)!, "compact.png"));
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(_preview)!, "layout-check.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { Width, Height, NavWidth = BottomNavigation.ActualWidth,
                        NavHeight = BottomNavigation.ActualHeight, BottomGap = ActualHeight - BottomNavigation.TranslatePoint(new Point(0, BottomNavigation.ActualHeight), this).Y,
                        DesktopCaptured = false, NativeAirDropAvailable = false }, PreferenceStore.JsonOptions));
                Close();
            }
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
            Text = "AirDrop · 开发预览",
            Icon = IconFactory.CreateIcon(32),
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开 AirDrop", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("打开接收目录", null, (_, _) => Dispatcher.Invoke(() => OpenPath(_saved.ReceiveDirectory)));
        menu.Items.Add("停靠右下角", null, (_, _) => Dispatcher.Invoke(() => { ShowFromTray(); WindowPlacement.Dock(this); }));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke(async () => { await _receiver.StopAsync(); _exitRequested = true; Close(); }));
        _tray.ContextMenuStrip = menu;
    }

    private async Task RunUiCheckAsync(string path)
    {
        var passed = new List<string>();
        string? error = null;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed.Add(label); }
        try
        {
            if(_startHidden)
            {
                Check(!IsVisible && _tray?.Visible==true,"startup mode hides the window and creates its tray icon");
                ShowFromTray();
                Check(IsVisible && Opacity==1,"startup tray restore reveals a fully opaque live window");
            }
            var placement = WindowPlacement.Inspect(this);
            Check(placement.RightGap >= 0 && placement.RightGap < 50 && placement.BottomGap >= 0 && placement.BottomGap < 50,
                "native window is inside the monitor working area at its bottom-right corner");
            Check(Width == 352 && Height == 480 && BottomNavigation.ActualWidth < 320, "compact window and four-item bottom navigation are laid out");
            BottomNavigation.Select(2);
            await Task.Delay(1000);
            Check(PageTitle.Text == "连接" && DevicesPage.Visibility == Visibility.Visible && !BottomNavigation.IsAnimating,
                "real WPF navigation changes pages and the spring unsubscribes after settling");
            BottomNavigation.Select(1); await Task.Delay(45);
            BottomNavigation.Select(3); await Task.Delay(1000);
            Check(PageTitle.Text == "设置" && !BottomNavigation.IsAnimating, "interrupted navigation settles on the final requested page");
            _model.MotionEnabled = false;
            BottomNavigation.Select(0);
            Check(!BottomNavigation.IsAnimating && ReceivePage.Visibility == Visibility.Visible, "reduced motion places the selector immediately");
            _model.AlwaysOnTop = false; Check(!Topmost, "pin state updates the actual WPF topmost property");
            Close(); Check(!IsVisible && !_closed, "close hides the window and keeps its tray lifecycle alive");
            ShowFromTray(); Check(IsVisible && _tray?.Visible == true, "tray restore shows the same live window");
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new { Success = error is null, Passed = passed, Error = error,
                Input = "application-owned WPF state transitions; physical pointer drag not automated" },PreferenceStore.JsonOptions));
            _exitRequested = true; Close();
            if (error is not null) System.Windows.Application.Current.Shutdown(1);
        }
    }

    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Ancestors(source).Any(x => x is System.Windows.Controls.Button)) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject current)
    {
        for (var next = current; next is not null; next = VisualTreeHelper.GetParent(next)) yield return next;
    }
    private void Minimize(object sender, RoutedEventArgs e) => Hide();
    private void CloseWindow(object sender, RoutedEventArgs e) => Hide();
    private void TogglePin(object sender, RoutedEventArgs e)
    {
        _model.AlwaysOnTop = !_model.AlwaysOnTop;
        try { _saved = _saved with { AlwaysOnTop = _model.AlwaysOnTop }; _store.Save(_saved); }
        catch (Exception error) { Feedback("置顶已切换，保存失败：" + error.Message); }
    }

    private void Navigate(int index)
    {
        if (!IsInitialized || ReceivePage is null) return;
        var page = new[] { "receive", "history", "devices", "settings" }[index];
        ReceivePage.Visibility = page == "receive" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = page == "history" ? Visibility.Visible : Visibility.Collapsed;
        DevicesPage.Visibility = page == "devices" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch
        {
            "history" => "文件",
            "devices" => "连接",
            "settings" => "设置",
            _ => "接收"
        };
        ScheduleBackdrop();
    }

    private void ShowDevices(object sender, RoutedEventArgs e)
    {
        BottomNavigation.Select(2);
    }

    private async void ScanHardware(object sender, RoutedEventArgs e) => await RefreshHardwareAsync();
    private async void ToggleReceiver(object sender,RoutedEventArgs e)
    {
        ScanButton.IsEnabled=false;
        try
        {
            if(_receiver.IsRunning) { await _receiver.StopAsync(); _model.ReceiverStatus="接收已停止"; }
            else await _receiver.StartAsync(_saved);
        }
        catch(Exception error)
        {
            _model.ReceiverStatus=error.Message;
            Feedback(error.Message);
            BottomNavigation.Select(2);
        }
        finally { ScanButton.IsEnabled=true; ScanButton.Content=_receiver.IsRunning?"停止接收":"开启接收"; ScheduleBackdrop(); }
    }
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
                if(!_receiver.IsRunning && !_receiver.IsStarting)
                    _model.ReceiverStatus=snapshot.UsbRadio?.Service.Equals("WinUSB",StringComparison.OrdinalIgnoreCase)==true?
                        (_receiver.IsAvailable?"可以启动接收，等待真机验证":"接收组件尚未打包") : "专用 USB 无线驱动尚未就绪";
                Feedback(snapshot.Error ?? "硬件已检查 · 原生接收开发中");
                ScheduleBackdrop();
            }
        }
        catch (Exception error) { if (!_closed) Feedback("检查失败：" + error.Message); }
        finally
        {
            _scanning = false;
            if (!_closed) { ScanButton.IsEnabled = true; ScanButton.Content = _receiver.IsRunning?"停止接收":"开启接收"; }
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
        EmptyHistoryDetail.Text = string.IsNullOrEmpty(SearchBox.Text) ? "接收目录中的文件会显示在这里" : "没有找到匹配的文件";
        ScheduleBackdrop();
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
        var dialog = new OpenFolderDialog { Title = "选择 AirDrop 接收目录", InitialDirectory = _saved.ReceiveDirectory };
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
            if(_preview is not null || _uiReport is not null) _store.Save(preferences);
            else _startup.Save(preferences.StartWithWindows,()=>_store.Save(preferences));
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
        Topmost = _preview is null && _model.AlwaysOnTop;
        PinButton.Opacity = _model.AlwaysOnTop ? 1 : 0.45;
        PinButton.ToolTip = _model.AlwaysOnTop ? "取消置顶" : "保持置顶";
        BottomNavigation.SetPreferences(_model.MotionEnabled && _preview is null, _model.HighContrast);
        ScheduleBackdrop();
    }

    private void ContentScrolled(object sender, ScrollChangedEventArgs e) => ScheduleBackdrop();
    private void ClipScene(object sender, SizeChangedEventArgs e)
    {
        ContentLayer.Clip = new RectangleGeometry(new Rect(new Point(),e.NewSize),22,22);
        ScheduleBackdrop();
    }
    private void ScheduleBackdrop()
    {
        if (!IsLoaded || _closed || _backdropRequest?.Status == DispatcherOperationStatus.Pending) return;
        _backdropRequest = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => { _backdropRequest = null; CaptureBackdrop(); });
    }
    private void CaptureBackdrop()
    {
        if (_closed || BottomNavigation.ActualWidth < 10) return;
        ContentLayer.UpdateLayout();
        var point = BottomNavigation.TranslatePoint(new Point(), ContentLayer);
        int width = (int)BottomNavigation.ActualWidth, height = (int)BottomNavigation.ActualHeight;
        var brush = new VisualBrush(ContentLayer) { ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(point.X, point.Y, width, height), Stretch = Stretch.Fill };
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen()) context.DrawRectangle(brush, null, new Rect(0,0,width,height));
        var image = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        image.Render(drawing); image.Freeze();
        BottomNavigation.SetBackdrop(image);
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
    private void Feedback(string message) { if (!_closed) { FeedbackText.Text = message; BottomNavigation.ToolTip = message; } }

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
        if (_preview is null && !_exitRequested) { e.Cancel = true; Hide(); return; }
        _closed = true;
        _controlTimer?.Stop(); _controlPipe?.Dispose();
        _backdropRequest?.Abort();
        _catalog.Dispose();
        _receiver.Dispose();
        _tray?.Dispose();
    }
}
