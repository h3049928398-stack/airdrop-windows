using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidGlassWinAirDrop;

// Current-user-only local IPC; never listens on a network port.
internal sealed class ControlPipe : IDisposable
{
    internal const string Name="LiquidGlassWinAirDrop.Control.v1";
    private readonly CancellationTokenSource _stop=new();
    public ControlPipe(Func<JsonElement,Task<object>> handle,string name=Name) { _=RunAsync(handle,name); }
    private async Task RunAsync(Func<JsonElement,Task<object>> handle,string name)
    {
        while(!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var limit=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); limit.CancelAfter(TimeSpan.FromSeconds(4));
                var bytes=new byte[4096]; int count=0;
                while(count<bytes.Length)
                {
                    int read=await pipe.ReadAsync(bytes.AsMemory(count,1),limit.Token);
                    if(read==0)break;
                    if(bytes[count++]=='\n')break;
                }
                object response;
                try
                {
                    if(count==0 || bytes[count-1]!='\n')throw new InvalidDataException("Invalid control request");
                    using var document=JsonDocument.Parse(bytes.AsMemory(0,count));
                    response=await handle(document.RootElement.Clone()).WaitAsync(limit.Token);
                }
                catch(Exception error) { response=new {ok=false,error=error.Message}; }
                var result=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)+"\n");
                await pipe.WriteAsync(result,limit.Token); await pipe.FlushAsync(limit.Token);
            }
            catch(OperationCanceledException) { }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { if(!_stop.IsCancellationRequested)await Task.Delay(1000).ConfigureAwait(false); }
        }
    }
    public void Dispose() { _stop.Cancel(); }
}
