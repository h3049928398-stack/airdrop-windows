using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LiquidGlassWinAirDrop;

public partial class MainWindow
{
    private ControlPipe? _controlPipe;
    private DispatcherTimer? _controlTimer;
    private DateTimeOffset _controlLease;
    private DateTimeOffset? _receiveUntil;
    private readonly Dictionary<string,ReceiveRequest> _requests=[];
    private string _transferPhase="idle", _lastFile="";
    private long _transferDone,_transferTotal;
    private bool _controlStarting;

    private void StartControl()
    {
        _controlPipe=new ControlPipe(request=>Dispatcher.InvokeAsync(()=>HandleControl(request)).Task);
        _controlTimer=new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Background,(_,_)=>
        {
            if(_receiveUntil is {} end && DateTimeOffset.UtcNow>=end)
            { _receiveUntil=null; _=ControlReceiveAsync(false); }
        },Dispatcher);
    }
    private object Snapshot() => new
    {
        ok=true, version=1, isRunning=_receiver.IsRunning, isStarting=_receiver.IsStarting||_controlStarting,
        message=_model.ReceiverStatus, deviceName=_saved.DeviceName, receiveDirectory=_saved.ReceiveDirectory,
        remainingSeconds=_receiveUntil is {} end?Math.Max(0,(int)(end-DateTimeOffset.UtcNow).TotalSeconds):(int?)null,
        requests=_requests.Values.Take(8).Select(r=>new {id=r.Id,sender=r.Sender,files=r.Files.Take(6)}).ToArray(),
        transfer=new {phase=_transferPhase,bytes=_transferDone,total=_transferTotal,lastFile=_lastFile}
    };
    private object HandleControl(JsonElement request)
    {
        if(request.ValueKind!=JsonValueKind.Object || !request.TryGetProperty("command",out var command))throw new ArgumentException("Missing command");
        if(request.TryGetProperty("client",out var client) && client.GetString()=="wisland")_controlLease=DateTimeOffset.UtcNow.AddSeconds(8);
        switch(command.GetString())
        {
            case "status": break;
            case "attach": Hide(); break;
            case "start":
                _receiveUntil=DateTimeOffset.UtcNow.AddMinutes(10);
                if(!_receiver.IsRunning && !_controlStarting && !_receiver.IsStarting) { _controlStarting=true; _=ControlReceiveAsync(true); }
                break;
            case "stop": _receiveUntil=null; _=ControlReceiveAsync(false); break;
            case "respond":
                string id=request.GetProperty("id").GetString()??"";
                bool accept=request.GetProperty("accept").GetBoolean();
                if(!_requests.Remove(id))throw new InvalidOperationException("接收请求已结束，请重新发送。");
                _=AnswerControlAsync(id,accept);
                break;
            default: throw new ArgumentException("Unknown command");
        }
        return Snapshot();
    }
    private async Task AnswerControlAsync(string id,bool accept)
    {
        try { await _receiver.RespondAsync(id,accept); }
        catch(Exception error) { _model.ReceiverStatus="接收确认失败："+error.Message; }
    }
    private async Task ControlReceiveAsync(bool start)
    {
        try
        {
            if(start) { _transferPhase="idle"; _transferDone=_transferTotal=0; await _receiver.StartAsync(_saved); }
            else { _model.ReceiverStatus="正在停止接收…"; await _receiver.StopAsync(); _requests.Clear(); _transferPhase="idle"; _model.ReceiverStatus="接收已关闭"; }
        }
        catch(Exception error) { _receiveUntil=null; _model.ReceiverStatus=error.Message; }
        finally { _controlStarting=false; ScanButton.Content=_receiver.IsRunning?"停止接收":"开启接收"; }
    }
    private void ControlProtocol(string line)
    {
        if(_closed)return;
        Dispatcher.BeginInvoke(()=>
        {
            if(_closed)return;
            using var json=JsonDocument.Parse(line); var item=json.RootElement;
            switch(item.GetProperty("event").GetString())
            {
                case "request_closed": _requests.Remove(item.GetProperty("id").GetString()??""); break;
                case "transfer_started":
                    _transferPhase="receiving"; _transferDone=0;
                    _transferTotal=item.TryGetProperty("total",out var total)&&total.ValueKind==JsonValueKind.Number?total.GetInt64():0; break;
                case "transfer_progress": _transferDone=item.GetProperty("bytes").GetInt64(); break;
                case "files_received": _transferPhase="received"; _lastFile=string.Join("、",item.GetProperty("files").EnumerateArray().Select(x=>x.GetString()).Take(6)); break;
                case "transfer_failed": _transferPhase="failed"; break;
            }
        });
    }
}
