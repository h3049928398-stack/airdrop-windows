using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace LiquidGlassWinAirDrop;

// Original vector-drawn identity. No imported art or third-party icon font.
public static class IconFactory
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 256f, size / 256f);
        using var outline = new GraphicsPath();
        const int inset = 9, arc = 105, edge = 256 - 2 * inset;
        outline.AddArc(inset, inset, arc, arc, 180, 90);
        outline.AddArc(inset + edge - arc, inset, arc, arc, 270, 90);
        outline.AddArc(inset + edge - arc, inset + edge - arc, arc, arc, 0, 90);
        outline.AddArc(inset, inset + edge - arc, arc, arc, 90, 90);
        outline.CloseFigure();
        using var fill = new LinearGradientBrush(new Rectangle(0, 0, 256, 256), Color.FromArgb(247, 254, 255), Color.FromArgb(133, 153, 227), 55);
        graphics.FillPath(fill, outline);
        using var rim = new Pen(Color.FromArgb(240, 255, 255, 255), 4);
        graphics.DrawPath(rim, outline);
        using var outer = new Pen(Color.FromArgb(245, 255, 255, 255), 6);
        using var middle = new Pen(Color.FromArgb(174, 190, 244), 5);
        using var center = new Pen(Color.FromArgb(109, 132, 213), 6);
        graphics.DrawEllipse(outer, 47, 47, 162, 162);
        graphics.DrawEllipse(middle, 71, 71, 114, 114);
        graphics.DrawEllipse(center, 95, 95, 66, 66);
        using var triangle = new SolidBrush(Color.FromArgb(103, 124, 203));
        graphics.FillPolygon(triangle, [new PointF(128, 106), new PointF(108, 139), new PointF(148, 139)]);
        return bitmap;
    }

    public static Icon CreateIcon(int size)
    {
        using var bitmap = Draw(size);
        var handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }

    public static void WriteIcon(string path)
    {
        int[] sizes = [16, 32, 48, 256];
        var images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using var bitmap = Draw(sizes[i]);
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            images[i] = memory.ToArray();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(images[i].Length); writer.Write(offset);
            offset += images[i].Length;
        }
        foreach (var png in images) writer.Write(png);
    }
}
