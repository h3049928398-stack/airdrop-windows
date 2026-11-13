using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidGlassWinAirDrop;

public static class SelfTest
{
    public static async Task<int> RunAsync(string reportPath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "LiquidGlassWinAirDrop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var passed = new List<string>();
        string? error = null;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed.Add(label); }
        try
        {
            var settings = new PreferenceStore(directory);
            var preferences = settings.Load() with { DeviceName = "玻璃 · Windows", MotionEnabled = false };
            settings.Save(preferences);
            Check(settings.Load() == preferences, "preferences persist Unicode names, folder and reduced motion");
            File.WriteAllText(Path.Combine(directory, "preferences.json"), "{");
            try { settings.Load(); throw new Exception("invalid JSON was accepted"); }
            catch (InvalidDataException) { passed.Add("damaged settings are reported and preserved"); }
            Check(File.ReadAllText(Path.Combine(directory, "preferences.json")) == "{", "damaged preferences not overwritten");
            var received = Path.Combine(directory, "received");
            var store = new ReceiveStore(received);
            var content = Encoding.UTF8.GetBytes("LiquidGlassWinAirDrop 原创存储验证");
            var digest = Convert.ToHexString(SHA256.HashData(content));
            var first = await store.SaveAsync("玻璃.txt", new MemoryStream(content), content.Length, digest, CancellationToken.None);
            Check(File.ReadAllBytes(first).SequenceEqual(content), "complete stream publishes exact bytes with SHA-256");
            var second = await store.SaveAsync("玻璃.txt", new MemoryStream(content), content.Length, digest, CancellationToken.None);
            Check(first != second && File.Exists(first), "duplicate names preserve existing files");
            foreach (var name in new[] { "../escape.txt", "..\\escape.txt", "C:escape", "CON.txt", "LPT1.txt", "trailing.", "aux", "name:stream", " name.txt" })
            {
                try { ReceiveStore.ValidateName(name); throw new Exception("unsafe name accepted: " + name); }
                catch (InvalidDataException) { }
            }
            passed.Add("path traversal, alternate streams, reserved names and ambiguous names rejected");
            async Task Reject(Func<Task<string>> action, string label)
            {
                var count = Directory.GetFiles(received).Length;
                try { await action(); throw new Exception("unexpected success: " + label); }
                catch (InvalidDataException) { }
                Check(count == Directory.GetFiles(received).Length, label);
            }
            await Reject(() => store.SaveAsync("short.txt", new MemoryStream(content), content.Length + 1, null, CancellationToken.None), "truncated stream leaves no final or partial file");
            await Reject(() => store.SaveAsync("long.txt", new MemoryStream(content), 2, null, CancellationToken.None), "oversized stream leaves no final or partial file");
            await Reject(() => store.SaveAsync("bad.txt", new MemoryStream(content), content.Length, new string('0', 64), CancellationToken.None), "hash mismatch leaves no final or partial file");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try { await store.SaveAsync("cancel.txt", new MemoryStream(content), content.Length, null, cancellation.Token); throw new Exception("cancel ignored"); }
            catch (OperationCanceledException) { }
            Check(Directory.GetFiles(received).Length == 2, "cancelled stream is cleaned without publishing");
            using var catalog = new FolderCatalog();
            var change = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            catalog.Changed += () => change.TrySetResult();
            catalog.Watch(received);
            File.WriteAllText(Path.Combine(received, "watch.txt"), "original local test");
            await change.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(FolderCatalog.Read(received).Count == 3, "real file watcher refreshes the folder catalog");
            var model = new MainViewModel();
            model.SetFiles(FolderCatalog.Read(received), "玻璃");
            Check(model.Files.Count == 2, "history search filters actual files");
            Check(!new NativeAirDropReceiver().IsAvailable, "unfinished AirDrop engine never reports ready");
        }
        catch (Exception failure) { error = failure.ToString(); }
        finally
        {
            // Only the freshly created task-specific directory is removed.
            Directory.Delete(directory, true);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { success = error is null, passed, error }, PreferenceStore.JsonOptions));
        }
        return error is null ? 0 : 1;
    }
}
