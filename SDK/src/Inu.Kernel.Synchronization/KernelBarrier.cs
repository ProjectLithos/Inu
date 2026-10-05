using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Reusable generation-counted barrier for a fixed participant count.</summary>
public unsafe struct KernelBarrier
{
    private UInt64 _participants;
    private UInt64 _arrived;
    private UInt64 _generation;

    public Boolean Initialize(UInt32 participants)
    { if(participants==0U)return false; _participants=participants; _arrived=0UL; _generation=0UL; return true; }

    public Boolean SignalAndWait(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    {
        UInt64 generation; fixed(UInt64* g=&_generation) if(!KernelAtomic.TryLoad(g,out generation))return false;
        UInt64 arrival; fixed(UInt64* a=&_arrived) if(!KernelAtomic.TryFetchAdd(a,1UL,out arrival))return false;
        if(arrival+1UL==_participants)
        {
            fixed(UInt64* a=&_arrived) if(!KernelAtomic.TryStore(a,0UL,KernelMemoryOrder.Release))return false;
            fixed(UInt64* g=&_generation) return KernelAtomic.TryIncrement(g,out _);
        }
        if(arrival+1UL>_participants)return false;
        UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds); UInt32 i=0;
        while(true)
        {
            fixed(UInt64* g=&_generation) if(!KernelAtomic.TryLoad(g,out UInt64 current))return false; else if(current!=generation)return true;
            if(KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds))
            {
                fixed(UInt64* g=&_generation) if(KernelAtomic.TryLoad(g,out UInt64 currentGeneration)&&currentGeneration!=generation)return true;
                fixed(UInt64* a=&_arrived)
                {
                    for(UInt32 undo=0U;undo<64U;undo++)
                    {
                        if(!KernelAtomic.TryLoad(a,out UInt64 currentArrived)||currentArrived==0UL)break;
                        if(KernelAtomic.TryCompareExchange(a,currentArrived,currentArrived-1UL,out UInt64 observed)&&observed==currentArrived)break;
                    }
                }
                fixed(UInt64* g=&_generation) if(KernelAtomic.TryLoad(g,out UInt64 finalGeneration)&&finalGeneration!=generation)return true;
                return false;
            }
            KernelSynchronizationWait.Pause(++i);
        }
    }
}
