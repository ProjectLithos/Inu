using System;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Time;

namespace Inu.Kernel.Synchronization;

internal static class KernelSynchronizationWait
{
    internal static Boolean HasTimedOut(UInt64 start, UInt64 timeoutNanoseconds)
    {
        if(timeoutNanoseconds==KernelSynchronizationTimeout.Infinite)return false;
        if(timeoutNanoseconds==0UL)return true;
        if(!KernelTime.IsInitialized)return true;
        UInt64 now=KernelTime.GetMonotonicNanoseconds();
        return now-start>=timeoutNanoseconds;
    }

    internal static UInt64 StartTimestamp(UInt64 timeoutNanoseconds)
    { return timeoutNanoseconds==KernelSynchronizationTimeout.Infinite||!KernelTime.IsInitialized ? 0UL : KernelTime.GetMonotonicNanoseconds(); }

    internal static Boolean Pause(UInt32 iteration)
    {
        if((iteration&0xFFU)==0U && KernelScheduler.IsInitialized() && KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu))
        {
            KernelScheduler.Yield(cpu,out _);
        }
        return KernelAtomic.SpinWaitHint();
    }
}
