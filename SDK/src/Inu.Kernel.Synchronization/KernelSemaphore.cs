using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Bounded counting semaphore with atomic acquisition/release and timeout support.</summary>
public unsafe struct KernelSemaphore
{
    private UInt64 _count;
    private UInt64 _maximum;

    public Boolean Initialize(UInt32 initialCount, UInt32 maximumCount)
    { if(maximumCount==0U||initialCount>maximumCount)return false; _count=initialCount; _maximum=maximumCount; return true; }

    public UInt32 GetCount() { fixed(UInt64* p=&_count) return KernelAtomic.TryLoad(p,out UInt64 value)?(UInt32)value:0U; }

    public Boolean TryWait()
    {
        fixed(UInt64* p=&_count)
        {
            for(UInt32 attempt=0U;attempt<64U;attempt++)
            {
                if(!KernelAtomic.TryLoad(p,out UInt64 current)||current==0UL)return false;
                if(KernelAtomic.TryCompareExchange(p,current,current-1UL,out UInt64 observed)&&observed==current)return true;
                KernelAtomic.SpinWaitHint();
            }
        }
        return false;
    }

    public Boolean Wait(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    { UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds); UInt32 i=0; do{if(TryWait())return true;KernelSynchronizationWait.Pause(++i);}while(!KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds));return false; }

    public Boolean Release(UInt32 count=1U)
    {
        if(count==0U)return false;
        fixed(UInt64* p=&_count)
        {
            for(UInt32 attempt=0U;attempt<128U;attempt++)
            {
                if(!KernelAtomic.TryLoad(p,out UInt64 current)||current+count>_maximum)return false;
                if(KernelAtomic.TryCompareExchange(p,current,current+count,out UInt64 observed)&&observed==current)return true;
            }
        }
        return false;
    }
}
