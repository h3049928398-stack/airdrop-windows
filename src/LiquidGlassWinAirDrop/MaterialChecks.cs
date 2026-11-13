using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiquidGlassWinAirDrop;

public static class MaterialChecks
{
    public static void Run(List<string> passed, string reportDirectory)
    {
        const int w = 160, h = 80;
        var pixels = new byte[w*h*4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int offset = (y*w+x)*4;
            pixels[offset] = (byte)(x%14 < 7 ? 55 : 215);
            pixels[offset+1] = (byte)(y%14 < 7 ? 100 : 225);
            pixels[offset+2] = (byte)(x%14 < 7 ? 225 : 90);
            pixels[offset+3] = 255;
        }
        var bitmap = BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);
        var source = LiquidMaterial.Read(bitmap);
        var flat = LiquidMaterial.Draw(source,120,60,20,10,28,0,0);
        var refracted = LiquidMaterial.Draw(source,120,60,20,10,28,7,0);
        byte[] Bytes(BitmapSource image)
        {
            var result = new byte[image.PixelWidth*image.PixelHeight*4];
            image.CopyPixels(result,image.PixelWidth*4,0); return result;
        }
        var a = Bytes(flat); var b = Bytes(refracted);
        if (b[3] != 0 || b[(30*120+60)*4+3] != 255) throw new Exception("invalid rounded lens alpha");
        passed.Add("lens corners are transparent and center remains opaque");
        int changed = 0;
        for (int i = 0; i < a.Length; i+=4) if (a[i] != b[i] || a[i+1] != b[i+1] || a[i+2] != b[i+2]) changed++;
        if (changed < 100) throw new Exception("lens does not displace real backdrop pixels");
        passed.Add("refraction changes sampled backdrop pixels rather than adding only transparency");
        for (int channel = 0; channel < 3; channel++)
            if (a[(30*120+60)*4+channel] != b[(30*120+60)*4+channel]) throw new Exception("quiet lens center unexpectedly warped");
        passed.Add("undistorted center preserves the original sample");
        var softened = LiquidMaterial.Read(bitmap,4);
        if (softened.Pixels[40*160*4+41*4] == pixels[40*160*4+41*4]) throw new Exception("blur did not soften backdrop");
        passed.Add("navigation backdrop softens high-frequency content before refraction");

        // Diagnostic fixture, not a production screen or a transfer simulation.
        var diagnostic = new byte[120*128*4];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 120; x++)
        {
            int offset = (y*120+x)*4;
            diagnostic[offset] = diagnostic[offset+1] = diagnostic[offset+2] = diagnostic[offset+3] = 255;
        }
        for (int y = 0; y < 60; y++)
        {
            Array.Copy(a,y*120*4,diagnostic,y*120*4,120*4);
            Array.Copy(b,y*120*4,diagnostic,(y+68)*120*4,120*4);
        }
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(120,128,96,96,PixelFormats.Pbgra32,null,diagnostic,120*4)));
        Directory.CreateDirectory(reportDirectory);
        using (var stream = File.Create(Path.Combine(reportDirectory,"material-proof.png"))) png.Save(stream);
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 30; i++) LiquidMaterial.Draw(source,72,53, i*2,7,26.5,7.5,0.42);
        timer.Stop();
        File.WriteAllText(Path.Combine(reportDirectory,"material-check.json"),JsonSerializer.Serialize(new
        {
            ChangedBackdropPixels = changed, SelectorRenderMeanMilliseconds = timer.Elapsed.TotalMilliseconds/30,
            OpticalSource = "app-owned content only", DesktopCaptured = false,
            Fixture = "synthetic striped material proof; not production UI or AirDrop traffic"
        },PreferenceStore.JsonOptions));
    }
}
