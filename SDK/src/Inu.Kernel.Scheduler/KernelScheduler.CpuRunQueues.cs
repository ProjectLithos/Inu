using System;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Scheduler;

/// <summary>Read-only view of one scheduler-owned per-CPU runnable set.</summary>
public readonly struct KernelCpuRunQueueView
{
    internal KernelCpuRunQueueView(UInt32 processorIndex,UInt32 runnableLoad,UInt64 currentThreadId,UInt64 idleThreadId)
    { ProcessorIndex=processorIndex;RunnableLoad=runnableLoad;CurrentThreadId=currentThreadId;IdleThreadId=idleThreadId; }
    public UInt32 ProcessorIndex { get; }
    public UInt32 RunnableLoad { get; }
    public UInt64 CurrentThreadId { get; }
    public UInt64 IdleThreadId { get; }
}

/// <summary>Scheduler-owned contract exposing CPU runnable-set state without exposing CpuScheduleState or ThreadRecord.</summary>
public static class KernelCpuRunQueueServices
{
    public static UInt32 ProcessorCount=>KernelScheduler.GetRunQueueProcessorCount();
    public static UInt32 BootstrapProcessorIndex=>KernelSmp.GetBootstrapProcessorIndex();
    public static Boolean IsEligible(UInt64 affinityMask,UInt32 processorIndex)=>KernelScheduler.IsRunQueueProcessorEligible(affinityMask,processorIndex);
    public static UInt32 GetLoad(UInt32 processorIndex)=>KernelScheduler.GetRunQueueLoad(processorIndex);
    public static Boolean TryGet(UInt32 processorIndex,out KernelCpuRunQueueView view)=>KernelScheduler.TryGetRunQueueView(processorIndex,out view);
    public static Boolean TryMove(KernelRunnableThreadHandle handle,UInt32 processorIndex,UInt64 migrationNanoseconds)=>KernelScheduler.TryMoveRunnableThread(handle,processorIndex,migrationNanoseconds);
}

public static unsafe partial class KernelScheduler
{
    internal static UInt32 GetRunQueueProcessorCount()=>_processorCount;

    internal static Boolean IsRunQueueProcessorEligible(UInt64 affinityMask,UInt32 cpu)
    {
        if(cpu>=_processorCount||cpu>=64U||(affinityMask&(1UL<<(Int32)cpu))==0UL)return false;
        if(!KernelSmp.TryGetProcessor(cpu,out KernelProcessorState processor))return false;
        if(processor.StartupState!=KernelProcessorStartupState.BootstrapProcessor&&processor.StartupState!=KernelProcessorStartupState.OnlineParked)return false;
        if(_lifecycle==StateRunning&&processor.StartupState==KernelProcessorStartupState.OnlineParked&&!KernelSmp.IsProcessorSchedulerReady(cpu))return false;
        return true;
    }

    internal static UInt32 GetRunQueueLoad(UInt32 cpu)
    {
        if(cpu>=_processorCount||_cpus==null||_threads==null)return UInt32.MaxValue;
        UInt32 load=0U;UInt32 idleSlot=(_cpus+cpu)->IdleSlot;
        for(UInt32 slot=0U;slot<MaximumThreads;slot++)
        {
            ThreadRecord* r=_threads+slot;
            if(r->Id==0UL||r->State==(UInt32)KernelThreadState.Unused||r->State==(UInt32)KernelThreadState.Terminated||r->State==(UInt32)KernelThreadState.Blocked||slot==idleSlot)continue;
            if(r->ProcessorIndex==cpu)load++;
        }
        return load;
    }

    internal static Boolean TryMoveRunnableThread(KernelRunnableThreadHandle handle,UInt32 processorIndex,UInt64 migrationNanoseconds)
    {
        if(!handle.IsValid||processorIndex>=_processorCount||_threads==null)return false;UInt32 slot=handle.Token-1U;if(slot>=MaximumThreads)return false;ThreadRecord* r=_threads+slot;
        if(r->Id==0UL||r->State!=(UInt32)KernelThreadState.Ready||r->ExecutionClass==(Byte)KernelCpuExecutionClass.Idle)return false;
        r->ProcessorIndex=processorIndex;r->LastMigrationNanoseconds=migrationNanoseconds;return true;
    }

    internal static Boolean TryGetRunQueueView(UInt32 cpu,out KernelCpuRunQueueView view)
    {
        view=default;if(cpu>=_processorCount||_cpus==null||_threads==null)return false;
        CpuScheduleState* state=_cpus+cpu;UInt64 current=0UL,idle=0UL;
        if(state->CurrentSlot<MaximumThreads)current=(_threads+state->CurrentSlot)->Id;
        if(state->IdleSlot<MaximumThreads)idle=(_threads+state->IdleSlot)->Id;
        view=new KernelCpuRunQueueView(cpu,GetRunQueueLoad(cpu),current,idle);return true;
    }
}
