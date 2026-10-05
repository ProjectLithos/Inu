using System;
using Inu.Kernel.Gui;
using Inu.Kernel.Security;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private struct ExecutionTable
    {
        internal fixed UInt64 RunningProcessIds[(Int32)MaximumExecutionProcessors];
        internal fixed UInt64 PendingCompletionProcessIds[(Int32)MaximumExecutionProcessors];
        internal fixed UInt64 PendingKillProcessIds[(Int32)MaximumExecutionProcessors];
        internal fixed Int64 PendingCompletionCodes[(Int32)MaximumExecutionProcessors];
    }
#pragma warning disable CS0169
    private static ExecutionTable _execution;
#pragma warning restore CS0169

    private static Boolean TryFindRunningProcessor(UInt64 processId,out UInt32 cpu)
    {
        cpu=0U;if(processId==0UL)return false;UInt32 count=KernelSmp.IsInitialized()?KernelSmp.GetProcessorCount():1U;if(count>MaximumExecutionProcessors)count=MaximumExecutionProcessors;fixed(UInt64* running=_execution.RunningProcessIds){for(UInt32 i=0U;i<count;i++)if(running[i]==processId){cpu=i;return true;}}return false;
    }

    private static Boolean TryGetExecutionProcessor(out UInt32 cpu)
    {
        cpu=0U;if(KernelSmp.IsInitialized()&&!KernelSmp.TryGetCurrentProcessorIndex(out cpu))return false;return cpu<MaximumExecutionProcessors;
    }

    private static Boolean SetPendingExitForCurrentProcessor(UInt64 processId,Int64 code,Boolean kill)
    {
        if(!TryGetExecutionProcessor(out UInt32 cpu))return false;fixed(UInt64* completion=_execution.PendingCompletionProcessIds,kills=_execution.PendingKillProcessIds)fixed(Int64* codes=_execution.PendingCompletionCodes){completion[cpu]=processId;codes[cpu]=code;kills[cpu]=kill?processId:0UL;}return true;
    }

    private static void ClearPendingExit(UInt32 cpu){fixed(UInt64* completion=_execution.PendingCompletionProcessIds,kills=_execution.PendingKillProcessIds)fixed(Int64* codes=_execution.PendingCompletionCodes){completion[cpu]=0UL;kills[cpu]=0UL;codes[cpu]=0L;}}
    private static void ReadPendingExit(UInt32 cpu,out UInt64 processId,out Int64 code,out UInt64 killProcessId){fixed(UInt64* completion=_execution.PendingCompletionProcessIds,kills=_execution.PendingKillProcessIds)fixed(Int64* codes=_execution.PendingCompletionCodes){processId=completion[cpu];code=codes[cpu];killProcessId=kills[cpu];}}
    private static void ClearRunningProcess(UInt32 cpu){fixed(UInt64* running=_execution.RunningProcessIds)running[cpu]=0UL;}

    private static void ReturnRunningProcessToReady(UInt32 cpu,KernelProcessRecordHandle record)
    {
        ClearRunningProcess(cpu);if(record.IsValid)ReleaseForegroundProcessClaim(KernelProcessRecordStore.GetId(record));if(record.IsValid)KernelProcessRecordStore.ReturnToReady(record);
    }

    private static Boolean FinishFaultedProcess(KernelProcessRecordHandle record,Int64 exitCode,Boolean rootRestored)
    {
        if(!record.IsValid)return false;UInt64 processId=KernelProcessRecordStore.GetId(record);ReleaseForegroundProcessClaim(processId);KernelGui.ReleaseProcess(processId);KernelSecurity.UnregisterProcess(processId);Boolean released=KernelProcessAddressSpaceServices.ReleaseOwned(record);KernelProcessRecordStore.Deactivate(record,KernelProcessState.Faulted,exitCode);if(!rootRestored||!released)TraceUserRecord("[USR] fault cleanup reported an error\r\n");return false;
    }

    private static Boolean ConfigureSyscallPolicy(UInt64 processId,Inu.ApplicationFormat.InuApplicationAbi abi)
    {
        if(!KernelSecurity.TrySetSyscallAbiPolicy(processId,1U,false)||!KernelSecurity.TrySetSyscallAbiPolicy(processId,2U,false)||!KernelSecurity.TrySetSyscallAbiPolicy(processId,3U,false))return false;return KernelSecurity.TrySetSyscallAbiPolicy(processId,(UInt32)abi,true);
    }
}
