using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Owns opaque interrupt-route allocation, growth and zeroing.</summary>
public static unsafe partial class KernelInterruptBroker
{
    private static Route* Free()
    {
        for (UInt32 i = 0; i < _capacity; i++) if ((_routes + i)->Used == 0) return _routes + i;
        return null;
    }

    private static Boolean Allocate(UInt32 count, out KernelHeapAllocation allocation, out Route* routes)
    {
        allocation = default; routes = null;
        if (!KernelHeap.TryAllocate((UInt64)count * (UInt64)sizeof(Route), 64UL, true, out allocation)) return false;
        routes = (Route*)(nuint)allocation.Address;
        return true;
    }

    private static Boolean Grow()
    {
        UInt32 next = _capacity >= 0x40000000U ? UInt32.MaxValue : _capacity * 2U;
        if (next <= _capacity || !Allocate(next, out KernelHeapAllocation fresh, out Route* routes)) return false;
        for (UInt32 i = 0; i < _capacity; i++) routes[i] = _routes[i];
        KernelHeapAllocation old = _allocation; _allocation = fresh; _routes = routes; _capacity = next;
        return KernelHeap.TryRelease(old);
    }

    private static void Clear(Route* route)
    {
        Byte* bytes = (Byte*)route;
        for (UInt64 i = 0; i < (UInt64)sizeof(Route); i++) bytes[i] = 0;
    }
}
