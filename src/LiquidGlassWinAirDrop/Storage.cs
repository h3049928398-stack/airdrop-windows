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

public sealed record Preferences
{
    public string DeviceName { get; init; } = Environment.MachineName;
    public string ReceiveDirectory { get; init; } = "";
    public bool MotionEnabled { get; init; } = true;
    public bool HighContrast { get; init; }
    public bool AlwaysOnTop { get; init; } = true;
    public bool StartWithWindows { get; init; }
}

public sealed class PreferenceStore(string dataRoot)
{
    public string DataRoot { get; } = Path.GetFullPath(dataRoot);
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true };

    public Preferences Load()
    {
        var path = Path.Combine(DataRoot, "preferences.json");
        if (!File.Exists(path)) return Defaults();
        try
        {
            var result = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path));
            return Validate(result ?? Defaults());
        }
        catch (JsonException)
        {
            // Preserve the damaged file for inspection; do not silently overwrite it.
            throw new InvalidDataException("偏好设置文件无法读取，请检查 data/preferences.json。");
        }
    }

    private Preferences Defaults() => new() { ReceiveDirectory = Path.Combine(DataRoot, "Received") };

    public static Preferences Validate(Preferences value)
    {
        var name = value.DeviceName.Trim();
        if (name.Length is < 1 or > 63 || name.Any(char.IsControl))
            throw new ArgumentException("设备名称需要 1–63 个可见字符。");
        if (!Path.IsPathFullyQualified(value.ReceiveDirectory))
            throw new ArgumentException("请选择一个完整的接收目录。");
        return value with { DeviceName = name, ReceiveDirectory = Path.GetFullPath(value.ReceiveDirectory) };
    }

    public void Save(Preferences value)
    {
        value = Validate(value);
        Directory.CreateDirectory(DataRoot);
        var path = Path.Combine(DataRoot, "preferences.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed record FileEntry(string Name, string FullPath, long Length, DateTime ModifiedUtc)
{
    public string Detail => "接收目录 · " + ModifiedUtc.ToLocalTime().ToString("MM月dd日 HH:mm");
    public string SizeLabel => Length >= 1048576 ? $"{Length / 1048576d:0.0} MB" : Length >= 1024 ? $"{Length / 1024d:0.0} KB" : $"{Length} B";
}

public sealed class FolderCatalog : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    public event Action? Changed;

    public void Watch(string directory)
    {
        Dispose();
        Directory.CreateDirectory(directory);
        _debounce = new Timer(_ => Changed?.Invoke());
        _watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };
        FileSystemEventHandler change = (_, _) => Schedule();
        _watcher.Created += change;
        _watcher.Deleted += change;
        _watcher.Changed += change;
        _watcher.Renamed += (_, _) => Schedule();
        _watcher.Error += (_, _) => Schedule();
        _watcher.EnableRaisingEvents = true;
    }

    private void Schedule() { try { _debounce?.Change(500, Timeout.Infinite); } catch (ObjectDisposedException) { } }

    public static IReadOnlyList<FileEntry> Read(string directory)
    {
        if (!Directory.Exists(directory)) return Array.Empty<FileEntry>();
        var result = new List<FileEntry>();
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (Path.GetFileName(path).StartsWith('.') || path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                result.Add(new(info.Name, info.FullName, info.Length, info.LastWriteTimeUtc));
            }
            catch (IOException) { } // A file may disappear between enumeration and stat.
        }
        return result.OrderByDescending(x => x.ModifiedUtc).ToArray();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _watcher = null;
        _debounce = null;
    }
}

// Original storage primitive for the future protocol worker. A completed file is
// atomically published only after stream/size/hash checks; no archive paths run.
public sealed class ReceiveStore(string directory)
{
    public static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || name != name.Trim() ||
            name.EndsWith('.') || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
            throw new InvalidDataException("文件名无效。");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9'))
            throw new InvalidDataException("文件名是系统保留名称。");
        return name.Normalize(NormalizationForm.FormC);
    }

    public async Task<string> SaveAsync(string name, Stream input, long expectedBytes, string? sha256, CancellationToken cancellationToken)
    {
        name = ValidateName(name);
        if (expectedBytes < 0) throw new ArgumentOutOfRangeException(nameof(expectedBytes));
        Directory.CreateDirectory(directory);
        if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("接收目录不能是符号链接。");
        var temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".partial");
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (total > expectedBytes - read) throw new InvalidDataException("文件超过声明的长度。");
                    total += read;
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != expectedBytes) throw new InvalidDataException("文件传输不完整。");
                await output.FlushAsync(cancellationToken);
            }
            var actual = Convert.ToHexString(hash.GetHashAndReset());
            if (sha256 is not null && !actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("文件校验失败。");
            cancellationToken.ThrowIfCancellationRequested();
            for (int suffix = 0; suffix < 10000; suffix++)
            {
                var destination = Path.Combine(directory, suffix == 0 ? name :
                    Path.GetFileNameWithoutExtension(name) + $" ({suffix})" + Path.GetExtension(name));
                try { File.Move(temporary, destination, false); return destination; }
                catch (IOException) when (File.Exists(destination)) { }
            }
            throw new IOException("同名文件过多，请选择另一个文件名。");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
