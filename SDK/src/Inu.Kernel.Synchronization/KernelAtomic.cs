using System;
using Inu.Arch.X64;

namespace Inu.Kernel.Synchronization;

/// <summary>Freestanding 64-bit atomic operations backed by architecture locked instructions.</summary>
public static unsafe class KernelAtomic
{
    public static Boolean IsSupported() => X64ArchitectureBoundary.EnsureInstalled();

    public static Boolean TryLoad(UInt64* location, out UInt64 value, KernelMemoryOrder order=KernelMemoryOrder.Acquire)
    {
        value=0UL; if(location==null||!IsSupported())return false;
        if(!X64ArchitectureBoundary.AtomicLoad64(location,out value))return false;
        return ApplyPostLoadOrder(order);
    }

    public static Boolean TryStore(UInt64* location, UInt64 value, KernelMemoryOrder order=KernelMemoryOrder.Release)
    {
        if(location==null||!IsSupported())return false;
        if(order==KernelMemoryOrder.SequentiallyConsistent||order==KernelMemoryOrder.Release||order==KernelMemoryOrder.AcquireRelease)
            if(!X64ArchitectureBoundary.MemoryBarrier())return false;
        return X64ArchitectureBoundary.AtomicStore64(location,value);
    }

    public static Boolean TryCompareExchange(UInt64* location, UInt64 expected, UInt64 replacement, out UInt64 previous, KernelMemoryOrder order=KernelMemoryOrder.SequentiallyConsistent)
    {
        previous=0UL; if(location==null||!IsSupported())return false;
        Boolean ok=X64ArchitectureBoundary.AtomicCompareExchange64(location,expected,replacement,out previous);
        if(!ok && previous==expected)return false;
        if(order!=KernelMemoryOrder.Relaxed) X64ArchitectureBoundary.MemoryBarrier();
        return ok;
    }

    public static Boolean TryExchange(UInt64* location, UInt64 value, out UInt64 previous)
    { previous=0UL; return location!=null&&IsSupported()&&X64ArchitectureBoundary.AtomicExchange64(location,value,out previous); }

    public static Boolean TryFetchAdd(UInt64* location, UInt64 delta, out UInt64 previous)
    { previous=0UL; return location!=null&&IsSupported()&&X64ArchitectureBoundary.AtomicFetchAdd64(location,delta,out previous); }

    public static Boolean TryIncrement(UInt64* location, out UInt64 value)
    { value=0UL; if(!TryFetchAdd(location,1UL,out UInt64 previous))return false; value=previous+1UL; return true; }

    public static Boolean TryDecrement(UInt64* location, out UInt64 value)
    { value=0UL; if(!TryFetchAdd(location,UInt64.MaxValue,out UInt64 previous))return false; value=previous-1UL; return true; }

    public static Boolean MemoryBarrier() => IsSupported()&&X64ArchitectureBoundary.MemoryBarrier();
    public static Boolean SpinWaitHint() => IsSupported()&&X64ArchitectureBoundary.SpinWaitHint();

    private static Boolean ApplyPostLoadOrder(KernelMemoryOrder order)
    { return order==KernelMemoryOrder.Relaxed || X64ArchitectureBoundary.MemoryBarrier(); }
}
