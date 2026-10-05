using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Manual-reset or auto-reset kernel event.</summary>
public unsafe struct KernelEvent
{
    private UInt64 _signaled;
    private UInt64 _manualReset;

    public Boolean Initialize(Boolean manualReset, Boolean initiallySignaled=false)
    { _manualReset=manualReset?1UL:0UL; _signaled=initiallySignaled?1UL:0UL; return true; }

    public Boolean IsSet() { fixed(UInt64* p=&_signaled) return KernelAtomic.TryLoad(p,out UInt64 value)&&value!=0UL; }
    public Boolean Set() { fixed(UInt64* p=&_signaled) return KernelAtomic.TryStore(p,1UL,KernelMemoryOrder.Release); }
    public Boolean Reset() { fixed(UInt64* p=&_signaled) return KernelAtomic.TryStore(p,0UL,KernelMemoryOrder.Release); }

    public Boolean TryWait()
    {
        fixed(UInt64* p=&_signaled)
        {
            if(_manualReset!=0UL)return KernelAtomic.TryLoad(p,out UInt64 value)&&value!=0UL;
            return KernelAtomic.TryCompareExchange(p,1UL,0UL,out UInt64 previous)&&previous==1UL;
        }
    }

    public Boolean Wait(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    { UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds); UInt32 i=0; do{if(TryWait())return true;KernelSynchronizationWait.Pause(++i);}while(!KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds));return false; }
}
