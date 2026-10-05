using System;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Synchronization;

/// <summary>Adaptive non-recursive kernel mutex with ownership checking and timeout support.</summary>
public unsafe struct KernelMutex
{
    private UInt64 _state;
    private UInt64 _owner;

    public Boolean IsHeld() { fixed(UInt64* p=&_state) return KernelAtomic.TryLoad(p,out UInt64 value)&&value!=0UL; }

    public Boolean TryLock()
    {
        UInt64 owner=GetOwnerToken(); if(owner==0UL)return false;
        fixed(UInt64* state=&_state)
        {
            if(!KernelAtomic.TryCompareExchange(state,0UL,1UL,out UInt64 previous)||previous!=0UL)return false;
        }
        fixed(UInt64* p=&_owner)
        {
            if(KernelAtomic.TryStore(p,owner,KernelMemoryOrder.Release))return true;
        }
        fixed(UInt64* state=&_state) KernelAtomic.TryStore(state,0UL,KernelMemoryOrder.Release);
        return false;
    }

    public Boolean Lock(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    {
        UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds); UInt32 iteration=0U;
        do { if(TryLock())return true; KernelSynchronizationWait.Pause(++iteration); }
        while(!KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds));
        return false;
    }

    public Boolean Unlock()
    {
        UInt64 owner=GetOwnerToken(); if(owner==0UL)return false;
        fixed(UInt64* ownerAddress=&_owner)
        {
            if(!KernelAtomic.TryLoad(ownerAddress,out UInt64 current)||current!=owner)return false;
            if(!KernelAtomic.TryStore(ownerAddress,0UL,KernelMemoryOrder.Release))return false;
        }
        fixed(UInt64* state=&_state) return KernelAtomic.TryExchange(state,0UL,out UInt64 previous)&&previous==1UL;
    }

    private static UInt64 GetOwnerToken()
    {
        if(KernelScheduler.IsInitialized()&&KernelScheduler.TryGetCurrentThreadId(out UInt64 thread)&&thread!=0UL)return (thread<<1)|1UL;
        if(KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu))return ((UInt64)cpu+1UL)<<1;
        return 0UL;
    }
}
