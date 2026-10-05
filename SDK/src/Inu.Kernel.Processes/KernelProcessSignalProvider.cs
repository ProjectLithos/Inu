using System;
namespace Inu.Kernel.Processes;
public static unsafe class KernelProcessSignalProvider
{
    public static Boolean Register()=>KernelProcessSignalServices.Register(&KernelProcesses.TrySetProcessControlImplementation,&KernelProcesses.HandleForegroundCancellationAtSyscallBoundaryImplementation,&KernelProcesses.HandleForegroundCancellationFromInterruptImplementation);
}
