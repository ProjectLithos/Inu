using System;

namespace Inu.Kernel.Scheduler;

/// <summary>Default priority-first scheduling policy. CPU ownership and migration decisions are delegated to the registered placement policy.</summary>
public static unsafe class KernelPrioritySchedulingPolicy
{
    public static Boolean Register()=>KernelSchedulingPolicyServices.Register(&SelectLocal,&SelectBalanced,&HasDispatchableWork);

    private static Boolean SelectLocal(UInt32 processorIndex,UInt64* output)
    {
        if(output==null)return false;*output=0UL;if(processorIndex>=KernelRunnableThreadServices.ProcessorCount)return false;
        if(TrySelectOwned(processorIndex,out KernelRunnableThreadView best)){*output=best.Id;return best.Id!=0UL;}
        if(!KernelRunnableThreadServices.TryGetIdle(processorIndex,out best))return false;*output=best.Id;return best.Id!=0UL;
    }

    private static Boolean HasDispatchableWork(UInt32 processorIndex)
    {
        if(processorIndex>=KernelRunnableThreadServices.ProcessorCount)return false;
        return TrySelectOwned(processorIndex,out _)||KernelThreadPlacementServices.HasStealableWork(processorIndex);
    }

    private static Boolean SelectBalanced(UInt32 processorIndex,UInt64* output)
    {
        if(output==null)return false;*output=0UL;if(processorIndex>=KernelRunnableThreadServices.ProcessorCount)return false;
        if(TrySelectOwned(processorIndex,out KernelRunnableThreadView best)){*output=best.Id;return best.Id!=0UL;}
        if(KernelThreadPlacementServices.TryStealTo(processorIndex,out UInt64 stolen)){*output=stolen;return stolen!=0UL;}
        if(KernelRunnableThreadServices.TryGetIdle(processorIndex,out KernelRunnableThreadView idle)){*output=idle.Id;return idle.Id!=0UL;}return false;
    }

    private static Boolean TrySelectOwned(UInt32 processorIndex,out KernelRunnableThreadView best)
    {
        best=default;Boolean found=false;
        for(UInt32 index=0U;index<KernelRunnableThreadServices.Capacity;index++)
        {
            if(!KernelRunnableThreadServices.TryGet(index,out KernelRunnableThreadView view)||view.State!=KernelThreadState.Ready||view.ProcessorIndex!=processorIndex||view.IsIdle)continue;
            if(!found||(UInt32)view.Priority>(UInt32)best.Priority){best=view;found=true;}
        }
        return found;
    }
}

public static unsafe partial class KernelScheduler
{
    /// <summary>Selects the highest-priority runnable thread currently owned by a processor using the registered policy.</summary>
    public static Boolean TrySelectNext(UInt32 processorIndex,out UInt64 threadId)=>KernelSchedulingPolicyServices.TrySelectLocal(processorIndex,out threadId);
}
