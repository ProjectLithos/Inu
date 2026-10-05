using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;
using Inu.Runtime.NativeAot;

namespace Inu.Kernel.Scheduler;

public static unsafe partial class KernelScheduler
{
    /// <summary>Gets one CPU-local scheduler snapshot and stable scheduler identifier.</summary>
    public static Boolean TryGetLocalScheduler(UInt32 processorIndex,out KernelCpuLocalSchedulerInfo info)
    { info=default; if(!_initialized||_cpus==null||processorIndex>=_processorCount)return false; CpuScheduleState* state=_cpus+processorIndex; UInt64 current=0UL; if(state->CurrentSlot!=0xFFFFFFFFU&&state->CurrentSlot<MaximumThreads)current=(_threads+state->CurrentSlot)->Id; info=new KernelCpuLocalSchedulerInfo(processorIndex,(UInt64)state,current,state->SwitchCount,state->PreemptionCount,state->LastDispatchNanoseconds); return true; }

    /// <summary>Gets the CPU-local scheduler for the calling processor.</summary>
    public static Boolean TryGetCurrentLocalScheduler(out KernelCpuLocalSchedulerInfo info)
    { info=default; return KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)&&TryGetLocalScheduler(cpu,out info); }

    /// <summary>Marks the calling CPU as entering the scheduler-visible idle/interrupt-wait state.</summary>
    public static Boolean NotifyCurrentProcessorIdleEnter()
    { if(!_initialized||!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)||cpu>=_processorCount)return false;CpuScheduleState* state=_cpus+cpu;AccountUntil(state,KernelTime.GetMonotonicNanoseconds());state->AccountingState=1;state->ExecutionClass=(Byte)KernelCpuExecutionClass.Idle;return true; }

    /// <summary>Marks the calling CPU as leaving the scheduler-visible idle/interrupt-wait state.</summary>
    public static Boolean NotifyCurrentProcessorIdleExit()
    { if(!_initialized||!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)||cpu>=_processorCount)return false;CpuScheduleState* state=_cpus+cpu;AccountUntil(state,KernelTime.GetMonotonicNanoseconds());state->AccountingState=0;state->ExecutionClass=(Byte)ExecutionClassForCurrentThread(state);return true; }

    /// <summary>Gets cumulative CPU busy/idle accounting without disturbing another observer's sampling interval.</summary>
    public static Boolean TryGetCpuUsageSnapshot(UInt32 processorIndex,out KernelCpuUsageSnapshot snapshot)
    {
        snapshot=default;if(!_initialized||_cpus==null||processorIndex>=_processorCount)return false;
        CpuScheduleState* state=_cpus+processorIndex;UInt64 busy=state->BusyNanoseconds,idle=state->IdleNanoseconds,now=KernelTime.GetMonotonicNanoseconds();
        UInt64 kernel=state->KernelNanoseconds,userland=state->UserlandNanoseconds,gui=state->GuiNanoseconds,drivers=state->DriverNanoseconds,interrupts=state->InterruptNanoseconds,networking=state->NetworkingNanoseconds,storage=state->StorageNanoseconds,realtime=state->RealtimeNanoseconds,background=state->BackgroundNanoseconds;
        UInt64 delta=now>=state->LastAccountingNanoseconds?now-state->LastAccountingNanoseconds:0UL;if(state->AccountingState==0){busy+=delta;AddExecutionDelta((KernelCpuExecutionClass)state->ExecutionClass,delta,ref kernel,ref userland,ref gui,ref drivers,ref interrupts,ref networking,ref storage,ref realtime,ref background);}else idle+=delta;
        Boolean online=false;if(KernelSmp.TryGetProcessor(processorIndex,out KernelProcessorState cpu))online=cpu.StartupState==KernelProcessorStartupState.BootstrapProcessor||cpu.StartupState==KernelProcessorStartupState.OnlineParked;
        UInt64 currentThread=0UL;if(state->CurrentSlot!=0xFFFFFFFFU&&state->CurrentSlot<MaximumThreads)currentThread=(_threads+state->CurrentSlot)->Id;
        snapshot=new KernelCpuUsageSnapshot(processorIndex,online,busy,idle,(KernelCpuExecutionClass)state->ExecutionClass,currentThread,kernel,userland,gui,drivers,interrupts,networking,storage,realtime,background);return true;
    }

    private static Boolean HasDispatchableWork(UInt32 processorIndex)=>KernelSchedulingPolicyServices.HasDispatchableWork(processorIndex);


    /// <summary>Performs the scheduler decision associated with a cooperative yield.</summary>
    public static Boolean Yield(UInt32 processorIndex, out UInt64 nextThreadId) => Schedule(processorIndex,false,out nextThreadId);
    /// <summary>Performs the scheduler decision associated with a Local APIC timer tick.</summary>
    public static Boolean OnTimerTick(UInt32 processorIndex, out UInt64 nextThreadId) => Schedule(processorIndex,true,out nextThreadId);

    private static Boolean Schedule(UInt32 cpu, Boolean preempt, out UInt64 nextThreadId)
    {
        nextThreadId=0UL; if (!_initialized || cpu>=_processorCount) return false; CpuScheduleState* cs=_cpus+cpu;
        EnterSchedulerLock();
        if(_lifecycle!=StateRunning)
        {
            if(cs->CurrentSlot!=0xFFFFFFFFU&&cs->CurrentSlot<MaximumThreads){ThreadRecord* current=_threads+cs->CurrentSlot;nextThreadId=current->Id;ExitSchedulerLock();return current->Id!=0UL;}
            ExitSchedulerLock();return false;
        }
        AccountUntil(cs,KernelTime.GetMonotonicNanoseconds()); cs->AccountingState=0;
        if(cs->CurrentSlot!=0xFFFFFFFFU){ThreadRecord* current=_threads+cs->CurrentSlot;if(current->State==(UInt32)KernelThreadState.Running){current->State=(UInt32)KernelThreadState.Ready;current->ProcessorIndex=cpu;}}
        if (!KernelSchedulingPolicyServices.TrySelectBalanced(cpu,out nextThreadId)) { ExitSchedulerLock(); return false; }
        if (!FindThread(nextThreadId,out UInt32 slot)) { ExitSchedulerLock(); return false; }
        ThreadRecord* next=_threads+slot; UInt32 previousSlot=cs->CurrentSlot; ThreadRecord* previous=previousSlot==0xFFFFFFFFU ? null : _threads+previousSlot;
        next->State=(UInt32)KernelThreadState.Running; next->ProcessorIndex=cpu; cs->CurrentSlot=slot; cs->SwitchCount++; if(preempt) cs->PreemptionCount++; cs->LastDispatchNanoseconds=KernelTime.GetMonotonicNanoseconds();cs->ExecutionClass=next->ExecutionClass;
        UInt64 previousContext=previous==null?0UL:previous->ContextAddress;UInt64 nextContext=next->ContextAddress;Boolean switchRequired=previous!=null&&previous!=next&&previousContext!=0UL&&nextContext!=0UL;
        ExitSchedulerLock();
        if(switchRequired)return Native.SwitchThreadContext(previousContext,nextContext);
        return true;
    }
    /// <summary>Gets the number of ready threads eligible to run on one processor.</summary>
    public static UInt32 GetReadyThreadCount(UInt32 processorIndex)
    { if(!_initialized||processorIndex>=_processorCount)return 0U;UInt32 count=0U;for(UInt32 slot=1U;slot<MaximumThreads;slot++){ThreadRecord* r=_threads+slot;if(r->State==(UInt32)KernelThreadState.Ready&&r->ProcessorIndex==processorIndex&&slot!=(_cpus+processorIndex)->IdleSlot)count++;}return count; }

    /// <summary>Gets the number of live non-idle threads owned by one CPU-local scheduler, including the BSP bootstrap thread.</summary>
    public static UInt32 GetAssignedThreadCount(UInt32 processorIndex)
    { if(!_initialized||processorIndex>=_processorCount)return 0U;UInt32 count=0U;UInt32 idleSlot=(_cpus+processorIndex)->IdleSlot;for(UInt32 slot=0U;slot<MaximumThreads;slot++){ThreadRecord* r=_threads+slot;if(r->Id==0UL||r->State==(UInt32)KernelThreadState.Unused||r->State==(UInt32)KernelThreadState.Terminated||slot==idleSlot)continue;if(r->ProcessorIndex==processorIndex)count++;}return count; }

    /// <summary>Gets the number of live scheduler-owned idle threads for one processor (zero for the BSP, one for an initialized AP).</summary>
    public static UInt32 GetIdleThreadCount(UInt32 processorIndex)
    { if(!_initialized||processorIndex>=_processorCount)return 0U;UInt32 slot=(_cpus+processorIndex)->IdleSlot;if(slot==0xFFFFFFFFU||slot>=MaximumThreads)return 0U;ThreadRecord* r=_threads+slot;return r->Id!=0UL&&r->State!=(UInt32)KernelThreadState.Unused&&r->State!=(UInt32)KernelThreadState.Terminated?1U:0U; }

    /// <summary>Entry used by x64 APs after SIPI. Each AP joins its CPU-local scheduler and executes its idle thread until role-affined work is runnable.</summary>
}
