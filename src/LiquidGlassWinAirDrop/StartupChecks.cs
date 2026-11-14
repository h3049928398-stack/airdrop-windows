using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace LiquidGlassWinAirDrop;

internal static class StartupChecks
{
    public static int Run(string report)
    {
        var path=@"Software\LiquidGlassWinAirDrop\Tests\"+Guid.NewGuid().ToString("N");
        var passed=new List<string>(); string? error=null;
        void Check(bool value,string name) { if(!value)throw new IOException(name); passed.Add(name); }
        try
        {
            var startup=new WindowsStartup(@"C:\测试 空格\AirDrop.exe",path);
            using(var key=Registry.CurrentUser.CreateSubKey(path))key.SetValue("OtherApp","preserve");
            Check(!startup.IsEnabled(),"absent startup entry is disabled");
            bool saved=false;
            startup.Save(true,()=>saved=true);
            using(var key=Registry.CurrentUser.OpenSubKey(path))
                Check(saved && startup.IsEnabled() && (string?)key?.GetValue(WindowsStartup.ValueName)==@"""C:\测试 空格\AirDrop.exe"" --startup", "enable persists a quoted executable and startup argument");
            try { startup.Save(false,()=>throw new IOException("simulated settings failure")); }
            catch(IOException failure) when(failure.Message=="simulated settings failure") { }
            Check(startup.IsEnabled(),"failed settings persistence restores the original enabled entry");
            startup.Save(false,()=>{});
            using(var key=Registry.CurrentUser.OpenSubKey(path))
                Check(!startup.IsEnabled() && (string?)key?.GetValue("OtherApp")=="preserve", "disable removes only the owned entry");
            try { startup.Save(true,()=>throw new IOException("simulated settings failure")); }
            catch(IOException failure) when(failure.Message=="simulated settings failure") { }
            Check(!startup.IsEnabled(),"failed enable leaves no startup registration");
        }
        catch(Exception failure) { error=failure.ToString(); }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path,false); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        File.WriteAllText(report,JsonSerializer.Serialize(new {Success=error is null,Passed=passed,Error=error,
            Scope="isolated HKCU test key; real Windows Run key never modified; no reboot/logon performed"},PreferenceStore.JsonOptions));
        return error is null?0:1;
    }
}
