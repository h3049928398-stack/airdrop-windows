using System;
using System.IO;
using Microsoft.Win32;

namespace LiquidGlassWinAirDrop;

internal sealed class WindowsStartup(string executable, string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    internal const string ValueName = "LiquidGlassWinAirDrop";
    internal string Command => "\"" + Path.GetFullPath(executable) + "\" --startup";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    // Restore the original entry if preference persistence fails.
    public void Save(bool enabled, Action persistPreferences)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
        var previous = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var kind = previous is null ? RegistryValueKind.String : key.GetValueKind(ValueName);
        try
        {
            if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
            else key.DeleteValue(ValueName, false);
            persistPreferences();
        }
        catch
        {
            if (previous is null) key.DeleteValue(ValueName, false);
            else key.SetValue(ValueName, previous, kind);
            throw;
        }
    }
}
