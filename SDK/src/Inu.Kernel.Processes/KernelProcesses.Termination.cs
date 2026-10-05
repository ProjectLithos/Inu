using System;
using Inu.Kernel.Gui;
using Inu.Kernel.Security;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    internal static Boolean TryTerminateImplementation(UInt64 processId,Int64 exitCode)
    {
        if(!_initialized||!KernelProcessRecordStore.TryBeginTermination(processId,out KernelProcessRecordHandle record,out KernelProcessState state,out _))return false;
        if(state==KernelProcessState.Terminated)return true;
        if(state==KernelProcessState.Faulted){KernelProcessRecordStore.SetStateAndExit(record,KernelProcessState.Terminated,exitCode);return true;}
        KernelGui.ReleaseProcess(processId);Boolean unregistered=KernelSecurity.UnregisterProcess(processId);Boolean released=KernelProcessAddressSpaceServices.ReleaseOwned(record);
        KernelProcessRecordStore.Deactivate(record,KernelProcessState.Terminated,exitCode);return unregistered&&released;
    }
}
