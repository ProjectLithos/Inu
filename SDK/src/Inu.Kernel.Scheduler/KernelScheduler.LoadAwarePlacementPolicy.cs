using System;
using Inu.Kernel.Time;

namespace Inu.Kernel.Scheduler;

/// <summary>Default affinity-aware, load-aware CPU placement policy with migration hysteresis and work stealing.</summary>
public static unsafe class KernelLoadAwarePlacementPolicy
{
    private const UInt64 MigrationMinimumResidencyNanoseconds=2000000000UL;
    private const UInt32 MigrationLoadAdvantage=2U;
    private const UInt32 NoProcessor=0xFFFFFFFFU;

    public static Boolean Register()=>KernelThreadPlacementServices.Register(&SelectInitial,&Rebalance,&StealTo,&HasStealableWork);

    private static Boolean SelectInitial(UInt64 affinityMask,UInt32* selected)
    {
        if(selected==null||affinityMask==0UL)return false;*selected=NoProcessor;
        UInt32 bestLoad=UInt32.MaxValue,bootstrap=KernelCpuRunQueueServices.BootstrapProcessorIndex;
        for(UInt32 cpu=0U;cpu<KernelCpuRunQueueServices.ProcessorCount&&cpu<64U;cpu++)
        {
            if(!KernelCpuRunQueueServices.IsEligible(affinityMask,cpu))continue;
            UInt32 load=KernelCpuRunQueueServices.GetLoad(cpu);
            Boolean prefer=*selected==NoProcessor||load<bestLoad||(load==bestLoad&&*selected==bootstrap&&cpu!=bootstrap);
            if(prefer){*selected=cpu;bestLoad=load;}
        }
        return *selected!=NoProcessor;
    }

    private static Boolean Rebalance(KernelRunnableThreadHandle handle,UInt32 currentCpu,UInt32* selected)
    {
        if(selected==null||!handle.IsValid)return false;*selected=currentCpu;
        if(!KernelRunnableThreadServices.TryGet(handle,out KernelRunnableThreadView thread))return false;
        if(!KernelCpuRunQueueServices.IsEligible(thread.AffinityMask,currentCpu))return SelectInitial(thread.AffinityMask,selected);
        UInt32 bestCpu=NoProcessor;if(!SelectInitial(thread.AffinityMask,&bestCpu))return false;
        if(bestCpu==currentCpu)return true;
        UInt64 now=KernelTime.GetMonotonicNanoseconds();if(!ResidencyExpired(thread,now))return true;
        UInt32 currentLoad=KernelCpuRunQueueServices.GetLoad(currentCpu),bestLoad=KernelCpuRunQueueServices.GetLoad(bestCpu);
        if(bestLoad+MigrationLoadAdvantage>currentLoad)return true;*selected=bestCpu;return true;
    }

    private static Boolean HasStealableWork(UInt32 destinationCpu)
    {
        if(destinationCpu>=KernelCpuRunQueueServices.ProcessorCount)return false;UInt64 now=KernelTime.GetMonotonicNanoseconds();UInt32 destinationLoad=KernelCpuRunQueueServices.GetLoad(destinationCpu);
        for(UInt32 index=0U;index<KernelRunnableThreadServices.Capacity;index++)
        {
            if(!KernelRunnableThreadServices.TryGet(index,out KernelRunnableThreadView thread)||!CanSteal(thread,destinationCpu,destinationLoad,now,out _))continue;return true;
        }
        return false;
    }

    private static Boolean StealTo(UInt32 destinationCpu,UInt64* threadId)
    {
        if(threadId==null||destinationCpu>=KernelCpuRunQueueServices.ProcessorCount)return false;*threadId=0UL;
        UInt64 now=KernelTime.GetMonotonicNanoseconds();UInt32 destinationLoad=KernelCpuRunQueueServices.GetLoad(destinationCpu);Boolean found=false;KernelRunnableThreadView best=default;UInt32 bestDonorLoad=0U;
        for(UInt32 index=0U;index<KernelRunnableThreadServices.Capacity;index++)
        {
            if(!KernelRunnableThreadServices.TryGet(index,out KernelRunnableThreadView thread)||!CanSteal(thread,destinationCpu,destinationLoad,now,out UInt32 donorLoad))continue;
            if(!found||(UInt32)thread.Priority>(UInt32)best.Priority||((UInt32)thread.Priority==(UInt32)best.Priority&&donorLoad>bestDonorLoad)){best=thread;bestDonorLoad=donorLoad;found=true;}
        }
        if(!found||!KernelCpuRunQueueServices.TryMove(best.Handle,destinationCpu,now))return false;*threadId=best.Id;return best.Id!=0UL;
    }

    private static Boolean CanSteal(KernelRunnableThreadView thread,UInt32 destinationCpu,UInt32 destinationLoad,UInt64 now,out UInt32 donorLoad)
    {
        donorLoad=0U;if(thread.State!=KernelThreadState.Ready||thread.IsIdle||thread.ProcessorIndex==destinationCpu)return false;
        if(destinationCpu>=64U||(thread.AffinityMask&(1UL<<(Int32)destinationCpu))==0UL)return false;if(!KernelCpuRunQueueServices.IsEligible(thread.AffinityMask,destinationCpu))return false;
        if(!ResidencyExpired(thread,now))return false;UInt32 donor=thread.ProcessorIndex;if(donor>=KernelCpuRunQueueServices.ProcessorCount)return false;donorLoad=KernelCpuRunQueueServices.GetLoad(donor);
        return donorLoad>=destinationLoad+MigrationLoadAdvantage;
    }

    private static Boolean ResidencyExpired(KernelRunnableThreadView thread,UInt64 now)
    { if(thread.LastMigrationNanoseconds==0UL)return true;UInt64 since=now>=thread.LastMigrationNanoseconds?now-thread.LastMigrationNanoseconds:0UL;return since>=MigrationMinimumResidencyNanoseconds; }
}
