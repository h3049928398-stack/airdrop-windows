using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidGlassWinAirDrop;

public sealed record ReceiveRequest(string Id, string Sender, string[] Files);

public sealed class NativeAirDropReceiver : IAirDropReceiver, IDisposable
{
    private readonly string _root, _backend, _data;
    private readonly List<Process> _processes = [];
    private Process? _protocol;
    private readonly SemaphoreSlim _operation = new(1,1);
    private readonly object _logLock = new();
    private CancellationTokenSource? _session;
    private bool _stopping;
    private WorkerJob? _job;
    public bool IsRunning { get; private set; }
    public bool IsStarting { get; private set; }
    public event Action<string>? StateChanged;
    public event Action<ReceiveRequest>? RequestReceived;
    public event Action? FilesReceived;
    public NativeAirDropReceiver(string? root = null)
    {
        _root = root ?? AppContext.BaseDirectory;
        _backend = Path.Combine(_root,"backend"); _data = Path.Combine(_root,"data");
    }
    private readonly string[] _required = ["AirDrop.Radio.exe","AirDrop.Awdl.exe","AirDrop.Network.exe","receiver_worker.py","python/python.exe","protocol/receiver.py"];
    public bool IsAvailable => _required.All(name => File.Exists(Path.Combine(_backend,name)));
    public string UnavailableReason => IsAvailable ? "接收组件已构建；专用驱动与真实手机传输仍需验证。" : "缺少原生接收组件，请构建并打包后端。";
    public string Describe(HardwareSnapshot snapshot)
    {
        if(!IsAvailable) return UnavailableReason;
        if(snapshot.UsbRadio is null) return "未检测到专用 USB 网卡。";
        if(snapshot.UsbRadio.ProblemCode != 0) return "USB 网卡状态异常。";
        if(!snapshot.UsbRadio.Service.Equals("WinUSB",StringComparison.OrdinalIgnoreCase))
            return "USB 网卡仍为普通 Wi-Fi 驱动，需要单独绑定 WinUSB。";
        return "组件与专用驱动已准备，可启动接收；无线兼容性待真机验证。";
    }
    private void State(string message) { Log("state",message); StateChanged?.Invoke(message); }
    private void Log(string worker,string line)
    {
        lock(_logLock)
        {
            Directory.CreateDirectory(Path.Combine(_data,"logs"));
            var path=Path.Combine(_data,"logs","engine.log");
            if(File.Exists(path) && new FileInfo(path).Length>5*1024*1024) File.Move(path,path+".1",true);
            File.AppendAllText(path,DateTimeOffset.Now.ToString("O")+" ["+worker+"] "+line+Environment.NewLine,Encoding.UTF8);
        }
    }
    private (Process Process, Task Ready) Launch(string name,string executable,IEnumerable<string> arguments,Func<string,bool> ready)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory=_backend,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        foreach(var arg in arguments) start.ArgumentList.Add(arg);
        start.Environment["PYTHONIOENCODING"]="utf-8";
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new Process { StartInfo=start,EnableRaisingEvents=true };
        process.OutputDataReceived += (_,e) =>
        {
            if(e.Data is not { } line) return;
            try { Log(name,line); if(ready(line)) signal.TrySetResult(); if(name=="protocol") ProtocolEvent(line); }
            catch(Exception error) { StateChanged?.Invoke("接收日志处理失败："+error.Message); }
        };
        process.ErrorDataReceived += (_,e) => { if(e.Data is { } line) { try { Log(name,line); } catch(IOException) { } } };
        process.Exited += (_,_) =>
        {
            signal.TrySetException(new IOException(name+" exited before ready"));
            if(name=="beacon")
            {
                if(!_stopping)StateChanged?.Invoke("无线接收仍在运行；蓝牙辅助发现不可用，请检查手机分享列表。");
                return;
            }
            if(!_stopping && _session?.IsCancellationRequested == false)
            {
                IsRunning=false;
                StateChanged?.Invoke("接收组件已退出，请检查日志后重试。");
                _session?.Cancel();
                _ = StopAsync();
            }
        };
        if(!process.Start()) throw new IOException("无法启动 "+name);
        try { (_job ??= new WorkerJob()).Add(process); }
        catch { try { process.Kill(true); } finally { process.Dispose(); } throw; }
        _processes.Add(process);
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return(process,signal.Task);
    }
    private void ProtocolEvent(string line)
    {
        if(!line.StartsWith('{'))return;
        using var document=JsonDocument.Parse(line);
        var root=document.RootElement;
        switch(root.GetProperty("event").GetString())
        {
            case "incoming_request":
                var files=root.GetProperty("files").EnumerateArray().Select(x=>x.GetString()??"").ToArray();
                RequestReceived?.Invoke(new(root.GetProperty("id").GetString()!,root.GetProperty("sender").GetString()!,files));
                break;
            case "transfer_started": StateChanged?.Invoke("正在接收文件…"); break;
            case "files_received": StateChanged?.Invoke("文件已保存到接收目录。"); FilesReceived?.Invoke(); break;
            case "transfer_failed": StateChanged?.Invoke("传输未完成："+root.GetProperty("reason").GetString()); break;
        }
    }
    public async Task RespondAsync(string id,bool accept)
    {
        var process=_protocol;
        if(process is null || process.HasExited)return;
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id,accept }));
        await process.StandardInput.FlushAsync();
    }
    internal string PrepareIdentity()
    {
        var keys=Path.Combine(_data,"keys"); Directory.CreateDirectory(keys);
        var certificate=Path.Combine(keys,"certificate.self-signed.pem");
        var privateKey=Path.Combine(keys,"key.self-signed.pem");
        if(!File.Exists(certificate)||!File.Exists(privateKey))
        {
            using var rsa=RSA.Create(2048);
            var request=new CertificateRequest("CN=AirDrop",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1));
            File.WriteAllText(certificate,cert.ExportCertificatePem());
            File.WriteAllText(privateKey,rsa.ExportPkcs8PrivateKeyPem());
        }
        var service=Path.Combine(keys,"service-id.txt");
        if(!File.Exists(service))File.WriteAllText(service,Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant());
        var id=File.ReadAllText(service).Trim();
        if(id.Length!=12 || !id.All(Uri.IsHexDigit))throw new InvalidDataException("无效的本地服务标识。");
        return id;
    }
    public async Task StartAsync(Preferences preferences)
    {
        await _operation.WaitAsync();
        try
        {
            if(IsRunning || IsStarting)return;
            var snapshot=await Task.Run(HardwareInspector.Capture);
            if(!IsAvailable || snapshot.UsbRadio?.Service.Equals("WinUSB",StringComparison.OrdinalIgnoreCase)!=true)
                throw new InvalidOperationException(Describe(snapshot));
            IsStarting=true; _stopping=false; _session?.Dispose(); _session=new CancellationTokenSource();
            var token=_session.Token; var id=PrepareIdentity();
            Directory.CreateDirectory(preferences.ReceiveDirectory);
            var receiver=Launch("protocol",Path.Combine(_backend,"python","python.exe"),
                ["-u",Path.Combine(_backend,"receiver_worker.py"),"--name",preferences.DeviceName,"--outdir",preferences.ReceiveDirectory,"--keys",Path.Combine(_data,"keys")],line=>line.Contains("receiver_ready"));
            _protocol=receiver.Process;
            await receiver.Ready.WaitAsync(TimeSpan.FromSeconds(15),token);
            State("正在初始化专用 USB 网卡…");
            var radio=Launch("radio",Path.Combine(_backend,"AirDrop.Radio.exe"),["--run"],line=>line.Contains("radio_ready"));
            await radio.Ready.WaitAsync(TimeSpan.FromSeconds(180),token);
            var awdl=Launch("awdl",Path.Combine(_backend,"AirDrop.Awdl.exe"),["-c","149","-s","1"],line=>line.Contains("host pipe ready"));
            var network=Launch("network",Path.Combine(_backend,"AirDrop.Network.exe"),[id],line=>line.Contains("network_ready"));
            await Task.WhenAll(awdl.Ready,network.Ready).WaitAsync(TimeSpan.FromSeconds(20),token);
            var beacon=Path.Combine(_backend,"beacon.ps1");
            if(File.Exists(beacon)) Launch("beacon",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),["-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",beacon],_=>true);
            IsRunning=true; State("接收服务已启动，等待 iPhone；首次兼容性待验证。");
        }
        catch { _session?.Cancel(); await StopCoreAsync(); throw; }
        finally { IsStarting=false; _operation.Release(); }
    }
    private static void Signal(string name)
    {
        try { using var stop=EventWaitHandle.OpenExisting(name); stop.Set(); } catch(WaitHandleCannotBeOpenedException) { }
    }
    public async Task StopAsync()
    {
        _session?.Cancel();
        await _operation.WaitAsync();
        try { await StopCoreAsync(); } finally { _operation.Release(); }
    }
    private async Task StopCoreAsync()
    {
        _stopping=true;
        if(_processes.Count>0) { Signal("Local\\AirDrop.Network.Stop.v1"); Signal("Local\\AirDrop.Radio.Stop.v1"); }
        foreach(var process in _processes.AsEnumerable().Reverse())
        {
            try
            {
                if(!process.HasExited)
                {
                    if(Path.GetFileName(process.StartInfo.FileName).Equals("AirDrop.Radio.exe",StringComparison.OrdinalIgnoreCase))
                    { try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); } catch(TimeoutException) { process.Kill(true); } }
                    else process.Kill(true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch(InvalidOperationException) { }
            catch(TimeoutException) { _job?.Dispose(); _job=null; }
            finally { process.Dispose(); }
        }
        _processes.Clear(); _protocol=null; IsRunning=IsStarting=false;
        _job?.Dispose(); _job=null;
    }
    public void Dispose()
    {
        _stopping=true; _session?.Cancel();
        if(_processes.Count>0) { Signal("Local\\AirDrop.Radio.Stop.v1"); Signal("Local\\AirDrop.Network.Stop.v1"); }
        foreach(var p in _processes) { try { if(!p.HasExited)p.Kill(true); } catch(InvalidOperationException) { } p.Dispose(); }
        _processes.Clear(); _session?.Dispose();
        _job?.Dispose(); _job=null;
    }
}
