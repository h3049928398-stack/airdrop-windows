using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiquidGlassWinAirDrop;

// Original bounded lens sampler. Only app-owned navigation backdrops are read;
// native labels/hit targets remain separate. No desktop/screen capture.
public static class LiquidMaterial
{
    public sealed record Backdrop(int Width, int Height, byte[] Pixels);
    public static Backdrop Read(BitmapSource bitmap, int blurRadius = 0)
    {
        var source = bitmap.Format == PixelFormats.Bgra32 ? bitmap : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        if (blurRadius > 0)
        {
            var intermediate = Blur(pixels,source.PixelWidth,source.PixelHeight,blurRadius,true);
            pixels = Blur(intermediate,source.PixelWidth,source.PixelHeight,blurRadius,false);
        }
        return new(source.PixelWidth, source.PixelHeight, pixels);
    }

    private static byte[] Blur(byte[] input, int width, int height, int radius, bool horizontal)
    {
        var output = new byte[input.Length];
        int length = horizontal ? width : height, lines = horizontal ? height : width, count = radius*2+1;
        for (int line = 0; line < lines; line++)
        for (int channel = 0; channel < 4; channel++)
        {
            int Index(int position) => (horizontal ? line*width+Math.Clamp(position,0,width-1) : Math.Clamp(position,0,height-1)*width+line)*4+channel;
            int total = 0;
            for (int k = -radius; k <= radius; k++) total += input[Index(k)];
            for (int p = 0; p < length; p++)
            {
                output[Index(p)] = (byte)(total/count);
                total += input[Index(p+radius+1)]-input[Index(p-radius)];
            }
        }
        return output;
    }

    public static BitmapSource Draw(Backdrop source, int width, int height, double originX, double originY,
        double radius, double strength, double tint, double pointerX = 0.3, double pressed = 0)
    {
        var pixels = new byte[width * height * 4];
        double halfX = width * 0.5, halfY = height * 0.5;
        radius = Math.Min(radius, Math.Min(halfX, halfY));
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double px = x + 0.5 - halfX, py = y + 0.5 - halfY;
            double qx = Math.Abs(px) - (halfX - radius), qy = Math.Abs(py) - (halfY - radius);
            double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), length = Math.Sqrt(ox * ox + oy * oy);
            double distance = length + Math.Min(Math.Max(qx, qy), 0) - radius;
            double alpha = Math.Clamp(0.5 - distance, 0, 1);
            if (alpha == 0) continue;
            double nx = length > 0.0001 ? ox / length * Math.Sign(px) : qx > qy ? Math.Sign(px) : 0;
            double ny = length > 0.0001 ? oy / length * Math.Sign(py) : qy >= qx ? Math.Sign(py) : 0;
            double edge = Math.Clamp(1 + distance / 10, 0, 1);
            double bend = strength * edge * edge;
            double sampleX = originX + x - nx * bend, sampleY = originY + y - ny * bend;
            double light = Math.Max(0, -nx * 0.45 - ny * 0.8);
            double rim = Math.Pow(Math.Clamp(1 + distance / 1.7, 0, 1), 2);
            double contact = pressed * Math.Max(0, 1 - Math.Abs(x / (double)width - pointerX) * 2);
            for (int channel = 0; channel < 3; channel++)
            {
                double dispersion = (channel - 1) * edge * 0.32;
                double value = Sample(source, sampleX + nx * dispersion, sampleY + ny * dispersion, channel);
                double body = value * (1 - tint) + 249 * tint;
                body += rim * (light * 45 - (1 - light) * 11) + contact * 8;
                pixels[(y * width + x) * 4 + channel] = (byte)(Math.Clamp(body, 0, 255) * alpha);
            }
            pixels[(y * width + x) * 4 + 3] = (byte)(alpha * 255);
        }
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    private static double Sample(Backdrop source, double x, double y, int channel)
    {
        x = Math.Clamp(x, 0, Math.Max(0, source.Width - 1.001));
        y = Math.Clamp(y, 0, Math.Max(0, source.Height - 1.001));
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, source.Width - 1), y1 = Math.Min(y0 + 1, source.Height - 1);
        double tx = x - x0, ty = y - y0;
        double a = source.Pixels[(y0 * source.Width + x0) * 4 + channel], b = source.Pixels[(y0 * source.Width + x1) * 4 + channel];
        double c = source.Pixels[(y1 * source.Width + x0) * 4 + channel], d = source.Pixels[(y1 * source.Width + x1) * 4 + channel];
        return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty;
    }
}
