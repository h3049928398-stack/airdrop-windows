using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LiquidGlassWinAirDrop;

// Windows closes the job when the UI process dies, stopping its radio/BLE
// workers even when managed shutdown handlers cannot run.
internal sealed class WorkerJob : IDisposable
{
    private readonly SafeFileHandle _handle;
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Limits
    {
        public BasicLimits Basic;
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref Limits limits,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
    public WorkerJob()
    {
        _handle=CreateJobObjectW(IntPtr.Zero,null);
        var limits=new Limits { Basic=new BasicLimits { Flags=0x2000 } };
        if(_handle.IsInvalid || !SetInformationJobObject(_handle,9,ref limits,(uint)Marshal.SizeOf<Limits>()))
        { var error=Marshal.GetLastWin32Error(); _handle.Dispose(); throw new Win32Exception(error); }
    }
    public void Add(Process process)
    {
        if(!AssignProcessToJobObject(_handle,process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose() => _handle.Dispose();
}
