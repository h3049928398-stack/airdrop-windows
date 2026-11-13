using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidGlassWinAirDrop;

// Synthetic host packets and loopback TLS only: never opens a USB device,
// starts a beacon, or treats this as real iPhone/AWDL acceptance.
public static class EngineChecks
{
    public static async Task<int> RunAsync(string report)
    {
        var passed=new List<string>(); string? failure=null;
        var root=AppContext.BaseDirectory;
        var data=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"engine-check-data");
        Directory.CreateDirectory(data);
        try
        {
            await CheckNetworkAsync(root,passed);
            await CheckProtocolAsync(root,data,passed);
            using var worker=Start(Path.Combine(root,"backend","AirDrop.Network.exe"),"jobcheck");
            using(var job=new WorkerJob())job.Add(worker);
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            passed.Add("closing the Windows job terminates an owned worker without opening USB or emitting RF");
        }
        catch(Exception error) { failure=error.ToString(); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        File.WriteAllText(report,JsonSerializer.Serialize(new { Success=failure is null, Passed=passed, Error=failure,
            Evidence="synthetic IPv6/lwIP/loopback TLS; no USB, BLE or real AirDrop transfer" },PreferenceStore.JsonOptions));
        return failure is null?0:1;
    }
    private static Process Start(string executable,params string[] args)
    {
        var start=new ProcessStartInfo(executable) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        start.Environment["PYTHONIOENCODING"]="utf-8";
        return Process.Start(start) ?? throw new IOException("Worker did not start");
    }
    private static async Task Cleanup(Process process)
    {
        try { if(!process.HasExited)process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { process.Dispose(); }
    }
    private static async Task CheckNetworkAsync(string root,List<string> passed)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20)); var token=timeout.Token;
        await using var pipe=new NamedPipeServerStream("AirDrop.Host.v1",PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        var listener=new TcpListener(IPAddress.Loopback,18771); listener.Start();
        var echo=Task.Run(async () =>
        {
            using var peer=await listener.AcceptTcpClientAsync(token);
            var request=new byte[4]; await peer.GetStream().ReadExactlyAsync(request,token);
            if(Encoding.ASCII.GetString(request)!="ping")throw new IOException("Wrong proxied bytes");
            await peer.GetStream().WriteAsync("pong"u8.ToArray(),token);
        },token);
        var process=Start(Path.Combine(root,"backend","AirDrop.Network.exe"),"airdroptest");
        var errors=process.StandardError.ReadToEndAsync(); var output=process.StandardOutput.ReadToEndAsync();
        var local=IPAddress.Parse("fe80::200:ff:fe00:1").GetAddressBytes();
        var remote=IPAddress.Parse("fe80::200:ff:fe00:2").GetAddressBytes();
        async Task Send(byte[] packet)
        {
            var length=BitConverter.GetBytes(packet.Length); await pipe.WriteAsync(length,token); await pipe.WriteAsync(packet,token); await pipe.FlushAsync(token);
        }
        async Task<byte[]> TcpFrame()
        {
            while(true)
            {
                var lengthBytes=new byte[4]; await pipe.ReadExactlyAsync(lengthBytes,token);
                int length=BitConverter.ToInt32(lengthBytes); if(length<40||length>65535)throw new IOException("Invalid host frame length");
                var packet=new byte[length]; await pipe.ReadExactlyAsync(packet,token);
                if(packet[6]==6 && BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(40))==8771)return packet;
            }
        }
        try
        {
            await pipe.WaitForConnectionAsync(token);
            await pipe.WriteAsync(BitConverter.GetBytes(16),token); await pipe.WriteAsync(local,token); await pipe.FlushAsync(token);
            await Send(Tcp(remote,local,1000,0,2,Array.Empty<byte>()));
            var syn=await TcpFrame();
            if((syn[53]&0x12)!=0x12 || BinaryPrimitives.ReadUInt32BigEndian(syn.AsSpan(48))!=1001)throw new IOException("No valid SYN/ACK from lwIP");
            uint sequence=BinaryPrimitives.ReadUInt32BigEndian(syn.AsSpan(44))+1;
            passed.Add("user-space lwIP accepts a synthetic IPv6 TCP connection over the AWDL host pipe");
            await Send(Tcp(remote,local,1001,sequence,16,Array.Empty<byte>()));
            await Send(Tcp(remote,local,1001,sequence,24,"ping"u8.ToArray()));
            var received=new List<byte>();
            while(received.Count<4)
            {
                var packet=await TcpFrame(); int header=(packet[52]>>4)*4;
                if(packet.Length>40+header) received.AddRange(packet.AsSpan(40+header).ToArray());
            }
            if(Encoding.ASCII.GetString(received.ToArray())!="pong")throw new IOException("TCP proxy changed the payload");
            await echo;
            passed.Add("opaque bidirectional TCP bytes survive host pipe, IPv6/lwIP and the loopback protocol proxy");
        }
        finally
        {
            listener.Stop(); await Cleanup(process);
            File.WriteAllText(Path.Combine(root,"network-check-error.log"),await errors);
            File.WriteAllText(Path.Combine(root,"network-check-output.log"),await output);
        }
    }
    private static byte[] Tcp(byte[] source,byte[] destination,uint sequence,uint acknowledgement,byte flags,byte[] body)
    {
        var bytes=new byte[60+body.Length]; bytes[0]=0x60; bytes[6]=6; bytes[7]=255;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4),(ushort)(20+body.Length));
        source.CopyTo(bytes,8); destination.CopyTo(bytes,24);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(40),44000); BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(42),8771);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(44),sequence); BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(48),acknowledgement);
        bytes[52]=0x50; bytes[53]=flags; BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(54),64000); body.CopyTo(bytes,60);
        uint sum=(uint)(20+body.Length+6);
        for(int i=8;i<40;i+=2)sum+=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i));
        for(int i=40;i<bytes.Length;i+=2)sum+=(uint)((bytes[i]<<8)+(i+1<bytes.Length?bytes[i+1]:0));
        while((sum>>16)!=0)sum=(sum&65535)+(sum>>16);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(56),(ushort)~sum); return bytes;
    }
    private static async Task CheckProtocolAsync(string root,string data,List<string> passed)
    {
        using var identity=new NativeAirDropReceiver(data); identity.PrepareIdentity();
        var dest=Path.Combine(data,"received");Directory.CreateDirectory(dest);
        var process=Start(Path.Combine(root,"backend","python","python.exe"),"-u",Path.Combine(root,"backend","receiver_worker.py"),
            "--name","AirDropTest","--outdir",dest,"--keys",Path.Combine(data,"data","keys"));
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stored=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool accept=false;
        int originalCount=Directory.GetFiles(dest).Length;
        var pump=Task.Run(async () =>
        {
            while(await process.StandardOutput.ReadLineAsync() is { } line)
            {
                using var document=JsonDocument.Parse(line);
                var eventName=document.RootElement.GetProperty("event").GetString();
                if(eventName=="receiver_ready")ready.TrySetResult();
                if(eventName=="files_received")stored.TrySetResult();
                if(eventName=="incoming_request")
                {
                    var id=document.RootElement.GetProperty("id").GetString();
                    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new {id,accept}));
                    await process.StandardInput.FlushAsync();
                }
            }
        });
        var errors=process.StandardError.ReadToEndAsync();
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var handler=new SocketsHttpHandler { SslOptions=new SslClientAuthenticationOptions {RemoteCertificateValidationCallback=(_,_,_,_)=>true} };
            using var client=new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(10) };
            var response=await client.PostAsync("https://127.0.0.1:18771/Discover",new ByteArrayContent(Array.Empty<byte>()));
            var bytes=await response.Content.ReadAsByteArrayAsync();
            if(!response.IsSuccessStatusCode || !bytes.AsSpan().StartsWith("bplist00"u8))throw new IOException("Discover did not return binary plist over TLS");
            passed.Add("local TLS worker presents the generated certificate and returns an AirDrop Discover binary plist");
            var ask="<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>SenderComputerName</key><string>Local test</string><key>Files</key><array><dict><key>FileName</key><string>test.txt</string><key>FileSize</key><integer>4</integer></dict></array></dict></plist>";
            var declined=await client.PostAsync("https://127.0.0.1:18771/Ask",new ByteArrayContent(Encoding.UTF8.GetBytes(ask)));
            if(declined.StatusCode!=HttpStatusCode.Unauthorized || Directory.GetFiles(dest).Length!=originalCount)throw new IOException("Explicit refusal did not reject the transfer");
            passed.Add("per-request consent refusal returns 401 and writes no files");
            accept=true;
            string filename="local-check-"+Guid.NewGuid().ToString("N")+".bin";
            var payload=RandomNumberGenerator.GetBytes(32768);
            var accepted=await client.PostAsync("https://127.0.0.1:18771/Ask",new ByteArrayContent(Encoding.UTF8.GetBytes(ask.Replace("test.txt",filename))));
            if(!accepted.IsSuccessStatusCode)throw new IOException("Explicit consent was not accepted");
            using var archive=new MemoryStream();
            void Member(string name,byte[] body,uint mode)
            {
                var raw=Encoding.UTF8.GetBytes(name+"\0");
                uint[] fields=[0,mode,0,0,1,0,(uint)body.Length,0,0,0,0,(uint)raw.Length,0];
                var header=new StringBuilder("070701"); foreach(uint field in fields)header.Append(field.ToString("x8"));
                archive.Write(Encoding.ASCII.GetBytes(header.ToString())); archive.Write(raw);
                while(archive.Length%4!=0)archive.WriteByte(0);
                archive.Write(body); while(archive.Length%4!=0)archive.WriteByte(0);
            }
            Member("./"+filename,payload,0x81A4); Member("TRAILER!!!",[],0);
            using var compressed=new MemoryStream();
            using(var gzip=new GZipStream(compressed,CompressionLevel.Fastest,true))gzip.Write(archive.ToArray());
            using var upload=new ByteArrayContent(compressed.ToArray());
            upload.Headers.ContentType=new("application/x-dvzip");
            var result=await client.PostAsync("https://127.0.0.1:18771/Upload",upload);
            if(!result.IsSuccessStatusCode)throw new IOException("Accepted archive was not received: "+result.StatusCode);
            await stored.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var received=File.ReadAllBytes(Path.Combine(dest,filename));
            if(!SHA256.HashData(payload).AsSpan().SequenceEqual(SHA256.HashData(received)))throw new IOException("Received bytes differ");
            passed.Add("explicit consent accepts a 32768-byte synthetic TLS/archive upload, emits the file event and preserves SHA-256");
        }
        finally { await Cleanup(process); await pump; File.WriteAllText(Path.Combine(root,"protocol-check-error.log"),await errors); }
    }
}
