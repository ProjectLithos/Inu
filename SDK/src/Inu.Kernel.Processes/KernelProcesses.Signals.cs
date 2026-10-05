using System;
using System.Runtime;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Smp;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    internal static Boolean TrySetProcessControlImplementation(UInt64 processId,KernelProcessControl control)
    {
        if(!_initialized||processId==0UL||control!=KernelProcessControl.Kill||!KernelProcessRecordStore.TryGetAny(processId,out KernelProcessRecordHandle record))return false;
        KernelProcessState state=KernelProcessRecordStore.GetState(record);Int64 exitCode=KernelProcessRecordStore.GetExitCode(record);
        if(state==KernelProcessState.Terminated)return true;
        if(state==KernelProcessState.Running)
        {
            Boolean stored=KernelProcessRecordStore.RequestKill(record);UInt32 runningCpu=0U;Boolean foundCpu=stored&&TryFindRunningProcessor(processId,out runningCpu);
            if(foundCpu&&KernelSmp.IsInitialized())KernelSmp.TrySendIpi(runningCpu,KernelIpiPurpose.Reschedule);
            return stored;
        }
        if(state!=KernelProcessState.Completed&&state!=KernelProcessState.Ready&&state!=KernelProcessState.Faulted)return false;return KernelProcessLifecycleServices.TryTerminate(processId,exitCode);
    }

    internal static Boolean HandleForegroundCancellationAtSyscallBoundaryImplementation(KernelSystemCallFrame* frame)=>frame!=null&&ArmRequestedProcessKillExit();
    internal static Boolean HandleForegroundCancellationFromInterruptImplementation()=>ArmRequestedProcessKillExit();

    private static Boolean ArmRequestedProcessKillExit()
    {
        if(!TryGetCurrentProcessId(out UInt64 id)||id==0UL||GetRunningProcessId()!=id)return false;
        Boolean requested=IsProcessKillRequested(id);
        if(!requested)
        {
            requested=KernelProcessForegroundServices.IsCancellationRequestedFor(id);
        }
        if(!requested)return false;if(!SetPendingExitForCurrentProcessor(id,ForegroundCancellationExitCode,true))return false;return Native.RequestUserModeExit(ForegroundCancellationExitCode);
    }

    private static Boolean IsProcessKillRequested(UInt64 processId)
    {
        return processId!=0UL&&KernelProcessRecordStore.TryGetAny(processId,out KernelProcessRecordHandle record)&&KernelProcessRecordStore.IsKillRequested(record);
    }
}
