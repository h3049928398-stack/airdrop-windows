using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace LiquidGlassWinAirDrop;

public sealed record DeviceInfo(string Name, string HardwareId, string Class, string Service, uint ProblemCode);
public sealed record WifiLink(string Name, string Description, string State);
public sealed record HardwareSnapshot(DateTimeOffset CapturedAt, DeviceInfo[] Devices, WifiLink[] WifiLinks, string? Error)
{
    public DeviceInfo? UsbRadio => Devices.FirstOrDefault(d =>
        d.HardwareId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) &&
        (d.HardwareId.Contains("VID_0BDA&PID_C811", StringComparison.OrdinalIgnoreCase) ||
        (d.HardwareId.Contains("VID_0BDA&PID_C820", StringComparison.OrdinalIgnoreCase) && d.HardwareId.Contains("MI_02", StringComparison.OrdinalIgnoreCase))));
    public DeviceInfo[] BluetoothRadios => Devices.Where(d => d.Class.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase) && d.HardwareId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)).ToArray();
}

// Read-only SetupAPI enumeration. No dependency on WMI, a downloaded helper,
// administrative rights, or the previously investigated application.
public static class HardwareInspector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceData { public uint Size; public Guid ClassGuid; public uint Instance; public UIntPtr Reserved; }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref DeviceData data, uint property, out uint type, byte[] buffer, uint capacity, out uint required);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint instance, uint flags);

    private static string Property(IntPtr set, ref DeviceData data, uint property)
    {
        var buffer = new byte[8192];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref data, property, out _, buffer, (uint)buffer.Length, out var size)) return "";
        return Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(size, buffer.Length)).TrimEnd('\0').Replace('\0', ';');
    }

    public static HardwareSnapshot Capture()
    {
        var devices = new List<DeviceInfo>();
        var links = Array.Empty<WifiLink>();
        var failures = new List<string>();
        try
        {
            var set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, 6); // PRESENT | ALLCLASSES
            if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                for (uint index = 0; ; index++)
                {
                    var data = new DeviceData { Size = (uint)Marshal.SizeOf<DeviceData>() };
                    if (!SetupDiEnumDeviceInfo(set, index, ref data))
                    {
                        if (Marshal.GetLastWin32Error() != 259) throw new Win32Exception(Marshal.GetLastWin32Error());
                        break;
                    }
                    var id = Property(set, ref data, 1);
                    var category = Property(set, ref data, 7);
                    if (!(id.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) || category == "Net")) continue;
                    var name = Property(set, ref data, 12);
                    if (string.IsNullOrEmpty(name)) name = Property(set, ref data, 0);
                    var service = Property(set, ref data, 4);
                    var result = CM_Get_DevNode_Status(out _, out var problem, data.Instance, 0);
                    devices.Add(new(name, id, category, service, result == 0 ? problem : uint.MaxValue));
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        catch (Exception error) { failures.Add("设备信息读取失败：" + error.Message); }
        try
        {
            links = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && devices.Any(d =>
                    d.Class.Equals("Net", StringComparison.OrdinalIgnoreCase) &&
                    (d.HardwareId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) || d.HardwareId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) &&
                    d.Name.Equals(n.Description, StringComparison.OrdinalIgnoreCase)))
                .Select(n => new WifiLink(n.Name, n.Description, n.OperationalStatus.ToString())).ToArray();
        }
        catch (Exception error) { failures.Add("网络状态读取失败：" + error.Message); }
        return new(DateTimeOffset.Now, devices.ToArray(), links, failures.Count == 0 ? null : string.Join("；", failures));
    }
}

public sealed record HardwareCard(string Title, string State, string Description);

// Application-owned engine contract; no wrapper/launcher for another app.
public interface IAirDropReceiver
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
}

public sealed class NativeAirDropReceiver : IAirDropReceiver
{
    public bool IsAvailable => false;
    public string UnavailableReason => "原生接收引擎尚未实现；当前版本无法接收 iPhone 隔空投送。";
}
