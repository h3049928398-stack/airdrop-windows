using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LiquidGlassWinAirDrop;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _deviceName = "";
    private string _receiveDirectory = "";
    private bool _motion = true, _contrast, _alwaysOnTop = true;
    private HardwareSnapshot? _snapshot;
    private IReadOnlyList<FileEntry> _allFiles = Array.Empty<FileEntry>();
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<FileEntry> Files { get; } = [];
    public ObservableCollection<HardwareCard> HardwareCards { get; } = [];
    public string DeviceName { get => _deviceName; set { _deviceName = value; Notify(); } }
    public string ReceiveDirectory { get => _receiveDirectory; set { _receiveDirectory = value; Notify(); } }
    public bool MotionEnabled { get => _motion; set { _motion = value; Notify(); } }
    public bool HighContrast { get => _contrast; set { _contrast = value; Notify(); } }
    public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; Notify(); } }
    public string FileCountLabel => $"{_allFiles.Count} 个文件";
    public string LatestTitle => _allFiles.Count == 0 ? "还没有文件，留一点期待。" : _allFiles[0].Name;
    public string LatestSubtitle => _allFiles.Count == 0 ? "接收目录中的文件将自动出现在这里。" : _allFiles[0].Detail + " · " + _allFiles[0].SizeLabel;
    public string CompactHardware => _snapshot is null ? "正在读取设备信息…" :
        $"USB 无线网卡  {(_snapshot.UsbRadio is null ? "未检测到" : "已检测到")}\n蓝牙适配器      {(_snapshot.BluetoothRadios.Length == 0 ? "未检测到" : "已检测到")}";
    public string LastScanLabel => _snapshot is null ? "尚未检查" : "更新于 " + _snapshot.CapturedAt.ToString("HH:mm:ss");
    public string HardwareHeadline => _snapshot?.Error ?? "无线接收需要兼容的专用 USB 网卡，蓝牙可辅助发现。当前版本尚未接入原生接收引擎。";

    public void Load(Preferences preferences)
    {
        DeviceName = preferences.DeviceName;
        ReceiveDirectory = preferences.ReceiveDirectory;
        MotionEnabled = preferences.MotionEnabled;
        HighContrast = preferences.HighContrast;
        AlwaysOnTop = preferences.AlwaysOnTop;
    }

    public Preferences Preferences => new() { DeviceName = DeviceName, ReceiveDirectory = ReceiveDirectory, MotionEnabled = MotionEnabled, HighContrast = HighContrast, AlwaysOnTop = AlwaysOnTop };

    public void UpdateHardware(HardwareSnapshot snapshot)
    {
        _snapshot = snapshot;
        HardwareCards.Clear();
        var radio = snapshot.UsbRadio;
        HardwareCards.Add(new("USB 无线网卡", radio is null ? "未检测到" : radio.ProblemCode == 0 ? "已检测到" : "设备异常",
            radio is null ? "请接入现有 RTL8811CU 或 RTL8821CU，再重新检查。" :
            radio.Name + "\n" + (radio.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase) ? "WinUSB 驱动已绑定，原始无线能力仍需验证。" : "当前为普通 Wi-Fi 驱动；本次检查不会更换驱动。") + $"（{radio.Service}）"));
        HardwareCards.Add(new("独立蓝牙", snapshot.BluetoothRadios.Length > 0 ? "已检测到" : "未检测到",
            snapshot.BluetoothRadios.Length > 0 ? string.Join("、", snapshot.BluetoothRadios.Select(x => x.Name)) + "\n已检测到设备，不代表实际 BLE 广播已经验证。" : "未检测到 USB 蓝牙适配器，广播能力待确认。"));
        HardwareCards.Add(new("当前 Wi-Fi", snapshot.WifiLinks.Any(x => x.State == "Up") ? "已连接" : "未连接",
            snapshot.WifiLinks.Length == 0 ? "未发现无线网络适配器。" : string.Join("\n", snapshot.WifiLinks.Select(x => x.Description + " · " + (x.State == "Up" ? "已连接" : "未连接")))));
        HardwareCards.Add(new("原生隔空投送", "开发中", new NativeAirDropReceiver().UnavailableReason));
        Notify(nameof(CompactHardware)); Notify(nameof(LastScanLabel)); Notify(nameof(HardwareHeadline));
    }

    public void SetFiles(IReadOnlyList<FileEntry> files, string query)
    {
        _allFiles = files;
        Filter(query);
        Notify(nameof(FileCountLabel)); Notify(nameof(LatestTitle)); Notify(nameof(LatestSubtitle));
    }

    public void Filter(string query)
    {
        Files.Clear();
        foreach (var file in _allFiles.Where(x => x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))) Files.Add(file);
    }

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
