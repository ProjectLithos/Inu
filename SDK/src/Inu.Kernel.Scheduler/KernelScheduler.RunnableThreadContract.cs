using System;

namespace Inu.Kernel.Scheduler;

/// <summary>Opaque identity used by scheduling policies to refer to a runnable thread without seeing the scheduler record layout.</summary>
public readonly struct KernelRunnableThreadHandle
{
    internal KernelRunnableThreadHandle(UInt32 token) { Token=token; }
    internal UInt32 Token { get; }
    public Boolean IsValid => Token!=0U;
}

/// <summary>Read-only scheduling view of one live thread.</summary>
public readonly struct KernelRunnableThreadView
{
    internal KernelRunnableThreadView(KernelRunnableThreadHandle handle,UInt64 id,KernelThreadState state,KernelThreadPriority priority,UInt32 processorIndex,UInt64 affinityMask,KernelCpuExecutionClass executionClass,UInt64 lastMigrationNanoseconds,Boolean idle)
    { Handle=handle;Id=id;State=state;Priority=priority;ProcessorIndex=processorIndex;AffinityMask=affinityMask;ExecutionClass=executionClass;LastMigrationNanoseconds=lastMigrationNanoseconds;IsIdle=idle; }
    public KernelRunnableThreadHandle Handle { get; }
    public UInt64 Id { get; }
    public KernelThreadState State { get; }
    public KernelThreadPriority Priority { get; }
    public UInt32 ProcessorIndex { get; }
    public UInt64 AffinityMask { get; }
    public KernelCpuExecutionClass ExecutionClass { get; }
    public UInt64 LastMigrationNanoseconds { get; }
    public Boolean IsIdle { get; }
}

/// <summary>Scheduler-owned runnable-set contract consumed by replaceable scheduling policies.</summary>
public static class KernelRunnableThreadServices
{
    public static UInt32 Capacity => KernelScheduler.GetRunnableThreadCapacityForPolicy();
    public static UInt32 ProcessorCount => KernelScheduler.GetProcessorCountForPolicy();
    public static Boolean TryGet(UInt32 index,out KernelRunnableThreadView view)=>KernelScheduler.TryGetRunnableThreadForPolicy(index,out view);
    public static Boolean TryGetIdle(UInt32 processorIndex,out KernelRunnableThreadView view)=>KernelScheduler.TryGetIdleThreadForPolicy(processorIndex,out view);
    public static UInt32 GetProcessorLoad(UInt32 processorIndex)=>KernelCpuRunQueueServices.GetLoad(processorIndex);
    public static Boolean TryGet(KernelRunnableThreadHandle handle,out KernelRunnableThreadView view)=>KernelScheduler.TryGetRunnableThreadForPolicy(handle.IsValid?handle.Token-1U:UInt32.MaxValue,out view);
}

public static unsafe partial class KernelScheduler
{
    internal static UInt32 GetRunnableThreadCapacityForPolicy()=>MaximumThreads;
    internal static UInt32 GetProcessorCountForPolicy()=>_processorCount;

    internal static Boolean TryGetRunnableThreadForPolicy(UInt32 index,out KernelRunnableThreadView view)
    {
        view=default;if(_threads==null||index>=MaximumThreads)return false;ThreadRecord* r=_threads+index;
        if(r->Id==0UL||r->State==(UInt32)KernelThreadState.Unused||r->State==(UInt32)KernelThreadState.Terminated)return false;
        Boolean idle=r->ExecutionClass==(Byte)KernelCpuExecutionClass.Idle;
        view=new KernelRunnableThreadView(new KernelRunnableThreadHandle(index+1U),r->Id,(KernelThreadState)r->State,(KernelThreadPriority)r->Priority,r->ProcessorIndex,r->AffinityMask,(KernelCpuExecutionClass)r->ExecutionClass,r->LastMigrationNanoseconds,idle);return true;
    }

    internal static Boolean TryGetIdleThreadForPolicy(UInt32 processorIndex,out KernelRunnableThreadView view)
    {
        view=default;if(_cpus==null||processorIndex>=_processorCount)return false;UInt32 slot=(_cpus+processorIndex)->IdleSlot;
        return slot<MaximumThreads&&TryGetRunnableThreadForPolicy(slot,out view)&&view.State==KernelThreadState.Ready&&view.ProcessorIndex==processorIndex;
    }


}
