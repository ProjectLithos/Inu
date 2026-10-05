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
    /// <summary>Retires a blocked/terminated non-running thread, unregistering its GC stack before releasing its kernel allocations.</summary>
    public static Boolean TryRetireThread(UInt64 threadId)
    {
        if(!_initialized||threadId==0UL)return false;UInt32 slot=0U;KernelHeapAllocation stack=default,context=default;Boolean canRetire=false;
        EnterSchedulerLock();
        if(FindThread(threadId,out slot))
        {
            ThreadRecord* r=_threads+slot;Boolean stateOk=r->State==(UInt32)KernelThreadState.Blocked||r->State==(UInt32)KernelThreadState.Terminated;Boolean current=false;
            if(stateOk)for(UInt32 cpu=0U;cpu<_processorCount;cpu++)if((_cpus+cpu)->CurrentSlot==slot){current=true;break;}
            Boolean executionQuiesced=r->OneShot==0||r->RetireReady!=0;
            if(stateOk&&!current&&executionQuiesced&&r->StackAllocation.Token!=0UL&&r->ContextAllocation.Token!=0UL)
            {
                r->State=(UInt32)KernelThreadState.Terminated;r->ProcessorIndex=NoProcessor;stack=r->StackAllocation;context=r->ContextAllocation;canRetire=true;
            }
        }
        ExitSchedulerLock();
        if(!canRetire)return false;
        if(!NativeAotGarbageCollector.UnregisterThreadStack(threadId))return false;
        Boolean contextReleased=KernelHeap.TryRelease(context);Boolean stackReleased=KernelHeap.TryRelease(stack);
        EnterSchedulerLock();
        ThreadRecord* retired=_threads+slot;if(retired->Id==threadId)
        {
            retired->Id=0UL;retired->StackBase=0UL;retired->StackTop=0UL;retired->EntryPoint=0UL;retired->Argument=0UL;retired->AffinityMask=0UL;retired->ContextAddress=0UL;retired->StackAllocation=default;retired->ContextAllocation=default;retired->State=(UInt32)KernelThreadState.Unused;retired->Priority=0U;retired->ProcessorIndex=NoProcessor;retired->NextReady=0xFFFFFFFFU;retired->ExecutionClass=0;retired->OneShot=0;retired->RetireReady=0;if(_activeThreads!=0U)_activeThreads--;
        }
        ExitSchedulerLock();
        return contextReleased&&stackReleased;
    }
}
