using System;
using Inu.Kernel.Gui;
using KernelFaultInjection = Inu.Kernel.Contracts.KernelFaultInjection;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    public static Boolean TryStart(UInt64 processId,UInt64 argument)
    {
        if(!TryGetExecutionProcessor(out UInt32 cpu))return false;
        fixed(UInt64* running=_execution.RunningProcessIds)if(running[cpu]!=0UL)return false;
        if(!KernelProcessRecordStore.TryMarkRunning(processId,out KernelProcessRecordHandle record))return false;
        fixed(UInt64* running=_execution.RunningProcessIds)
        {
            if(running[cpu]!=0UL){KernelProcessRecordStore.ReturnToReady(record);return false;}
            running[cpu]=processId;
        }
        ClearPendingExit(cpu);
        if(!TryClaimForegroundProcess(record)){ReturnRunningProcessToReady(cpu,record);return false;}
#if DEBUG
        TraceUserProcessStart(record);
#endif
        if(!KernelSystemCalls.EnsureCurrentProcessorConfigured()){TraceUserRecord("[USR] syscall CPU setup FAILED\r\n");ReturnRunningProcessToReady(cpu,record);return false;}
        UInt64 kernelRoot=KernelVirtualMemory.GetRootPhysicalAddress();
#if DEBUG
        TraceUserPreflight(record,kernelRoot);
#endif
        if(!ValidateProcessRootBeforeSwitch(record)){TraceUserRecord("[USR] process CR3 preflight FAILED; refusing transition\r\n");ReturnRunningProcessToReady(cpu,record);return false;}
        UInt64 userRoot=KernelProcessRecordStore.GetRoot(record),entry=KernelProcessRecordStore.GetEntry(record),stackTop=KernelProcessRecordStore.GetStackTop(record);
        if(!Native.WritePageTableRoot(userRoot)){TraceUserRecord("[USR] user CR3 switch FAILED\r\n");ReturnRunningProcessToReady(cpu,record);return false;}
#if DEBUG
        if(Native.BeginSerialRecord()){TraceUserText("[USR] user CR3 active readback=");TraceUserHex(Native.ReadPageTableRoot());TraceUserText("\r\n");Native.EndSerialRecord();}
#endif
        if(!KernelSecurity.SetCurrentProcess(processId)){TraceUserRecord("[USR] syscall policy activation FAILED\r\n");Native.WritePageTableRoot(kernelRoot);ClearRunningProcess(cpu);return FinishFaultedProcess(record,-2L,false);}
#if DEBUG
        if(Native.BeginSerialRecord()){TraceUserText("[USR] syscall policy active pid=");TraceUserHex(processId);TraceUserText("\r\n");Native.EndSerialRecord();}
#endif
        if(KernelFaultInjection.ShouldCrashUserProcess("process",out UInt64 injectedCrashCode))
        {
            if(Native.BeginSerialRecord()){TraceUserText("[USR] injected controlled process crash code=");TraceUserHex(injectedCrashCode);TraceUserText("\r\n");Native.EndSerialRecord();}
            Boolean injectedRootRestored=Native.WritePageTableRoot(kernelRoot);KernelSecurity.SetCurrentProcess(0UL);ClearRunningProcess(cpu);ClearPendingExit(cpu);ReleaseForegroundProcessClaim(processId);
            Int64 injectedExit=injectedCrashCode==0UL?-1L:unchecked((Int64)injectedCrashCode);return FinishFaultedProcess(record,injectedExit,injectedRootRestored);
        }
#if DEBUG
        if(Native.BeginSerialRecord()){TraceUserText("[USR] EnterUserMode -> IRETQ entry=");TraceUserHex(entry);TraceUserText(" rsp=");TraceUserHex(stackTop);TraceUserText("\r\n");Native.EndSerialRecord();}
#endif
        Int32 transition=Native.EnterUserMode(entry,stackTop,argument);
#if DEBUG
        if(transition==1)TraceUserRecord("[USR] controlled user exit returned to kernel\r\n");
        else if(transition==2)TraceUserRecord("[USR] contained CPL3 exception returned to kernel\r\n");
        else TraceUserRecord("[USR] EnterUserMode failed before controlled return\r\n");
#else
        if(transition!=1)TraceUserRecord(transition==2?"[USR] contained CPL3 exception returned to kernel\r\n":"[USR] EnterUserMode failed before controlled return\r\n");
#endif
        Boolean rootRestored=Native.WritePageTableRoot(kernelRoot);KernelSecurity.SetCurrentProcess(0UL);ClearRunningProcess(cpu);ReleaseForegroundProcessClaim(processId);
        ReadPendingExit(cpu,out UInt64 pendingProcess,out Int64 pendingCode,out UInt64 pendingKill);ClearPendingExit(cpu);
        if(transition==1&&pendingProcess==processId)
        {
            KernelProcessRecordStore.SetExitCode(record,pendingCode);
            if(pendingKill==processId)
            {
                KernelGui.ReleaseProcess(processId);KernelSecurity.UnregisterProcess(processId);Boolean released=KernelProcessAddressSpaceServices.ReleaseOwned(record);KernelProcessRecordStore.Deactivate(record,KernelProcessState.Terminated,pendingCode);return rootRestored&&released;
            }
            KernelProcessRecordStore.Complete(record,pendingCode);return rootRestored;
        }
        return FinishFaultedProcess(record,transition==2?-1L:-2L,rootRestored);
    }
}
