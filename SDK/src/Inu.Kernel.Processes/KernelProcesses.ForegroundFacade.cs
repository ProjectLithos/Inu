using System;
namespace Inu.Kernel.Processes;
public static unsafe partial class KernelProcesses
{
    public static UInt64 GetForegroundCommandProcessId()=>KernelProcessForegroundServices.GetForegroundProcessId();
    public static Boolean BeginForegroundCommandExecution()=>KernelProcessForegroundServices.Begin();
    public static Boolean RequestForegroundCommandCancellation()=>KernelProcessForegroundServices.RequestCancellation();
    public static Boolean ClearForegroundCommandCancellation()=>KernelProcessForegroundServices.ClearCancellation();
    public static Boolean IsForegroundCommandCancellationRequested()=>KernelProcessForegroundServices.IsCancellationRequested();
    private static Boolean TryClaimForegroundProcess(KernelProcessRecordHandle record)=>KernelProcessForegroundServices.TryClaim(record);
    private static void ReleaseForegroundProcessClaim(UInt64 processId)=>KernelProcessForegroundServices.Release(processId);
}
