using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace LiquidGlassWinAirDrop;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            if (e.Args.Length == 2 && e.Args[0] == "--make-icon")
            {
                IconFactory.WriteIcon(e.Args[1]);
                Shutdown();
                return;
            }
            if (e.Args.Length > 0 && e.Args[0] == "--self-test")
            {
                if (e.Args.Length != 2) throw new ArgumentException("--self-test needs a report path");
                Shutdown(await SelfTest.RunAsync(e.Args[1]));
                return;
            }
            if (e.Args.Length > 0 && e.Args[0] == "--diagnose")
            {
                if (e.Args.Length != 2) throw new ArgumentException("--diagnose needs an output path");
                var snapshot = HardwareInspector.Capture();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(e.Args[1]))!);
                File.WriteAllText(e.Args[1], JsonSerializer.Serialize(snapshot, PreferenceStore.JsonOptions));
                Shutdown(snapshot.Error is null ? 0 : 1);
                return;
            }
            string? preview = null;
            if (e.Args.Length > 0)
            {
                if (e.Args.Length == 2 && e.Args[0] == "--preview") preview = Path.GetFullPath(e.Args[1]);
                else throw new ArgumentException("Unrecognized arguments");
            }
            if (preview is null)
            {
                _singleInstance = new Mutex(true, "Local\\LiquidGlassWinAirDrop.Desktop", out var created);
                if (!created) { Shutdown(); return; }
            }
            var store = new PreferenceStore(Path.Combine(AppContext.BaseDirectory, "data"));
            var preferences = store.Load();
            var window = new MainWindow(store, preferences, preview);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (Exception error)
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.log"), error.ToString()); } catch { }
            if (e.Args.Length == 0) System.Windows.MessageBox.Show(error.Message, "LiquidGlassWinAirDrop", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { _singleInstance?.Dispose(); base.OnExit(e); }
}
