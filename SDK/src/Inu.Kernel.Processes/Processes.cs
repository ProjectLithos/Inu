using System;

namespace Inu.Kernel.Processes;

/// <summary><inu.api>Coder-facing process-policy facade for inspecting, starting, terminating and controlling foreground processes.</inu.api></summary>
public static class Processes
{
    public static UInt32 GetActiveCount()=>KernelProcesses.GetActiveProcessCount();
    public static Boolean TryStart(UInt64 processId,UInt64 argument=0UL)=>KernelProcesses.TryStart(processId,argument);
    public static Boolean Terminate(UInt64 processId,Int64 exitCode=0L)=>KernelProcesses.TryTerminate(processId,exitCode);
    public static UInt64 GetForegroundProcessId()=>KernelProcesses.GetForegroundCommandProcessId();
    public static Boolean CancelForeground()=>KernelProcesses.RequestForegroundCommandCancellation();
    public static Boolean IsForegroundCancellationRequested()=>KernelProcesses.IsForegroundCommandCancellationRequested();
}
