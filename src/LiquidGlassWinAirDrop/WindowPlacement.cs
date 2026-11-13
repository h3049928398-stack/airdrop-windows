using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LiquidGlassWinAirDrop;

public static class WindowPlacement
{
    public sealed record Position(int RightGap, int BottomGap, int PixelWidth, int PixelHeight);
    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Bounds bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    public static void Dock(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds)) return;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var work = screen.WorkingArea;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        int margin = (int)Math.Round(12 * dpi.DpiScaleX);
        int x = Math.Max(work.Left, work.Right - (bounds.Right - bounds.Left) - margin);
        int y = Math.Max(work.Top, work.Bottom - (bounds.Bottom - bounds.Top) - margin);
        // Position only: preserve WPF-owned size, focus, and topmost state.
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x15);
    }
    public static Position Inspect(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle,out var bounds)) throw new InvalidOperationException("Window bounds unavailable");
        var work = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        return new(work.Right-bounds.Right,work.Bottom-bounds.Bottom,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);
    }
}
