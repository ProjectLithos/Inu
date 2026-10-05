using System;
using Inu.Kernel.SystemCalls;
namespace Inu.Kernel.Processes;
public static unsafe partial class KernelProcesses
{
    public static Boolean TrySetProcessControl(UInt64 processId,KernelProcessControl control)=>KernelProcessSignalServices.TrySetControl(processId,control);
    private static Boolean HandleForegroundCancellationAtSyscallBoundary(KernelSystemCallFrame* frame)=>KernelProcessSignalServices.HandleSyscallCancellation(frame);
    private static Boolean HandleForegroundCancellationFromInterrupt()=>KernelProcessSignalServices.HandleInterruptCancellation();
}
