using Inu.Memory.Heap;

namespace Inu.Kernel.Sample;

internal static class HeapExamples
{
    internal static bool ValidateContracts()
    {
        BumpEarlyAllocator early = new(0x100000UL, 0x10000UL);
        if (!early.TryAllocate(128UL, 16UL, out ulong earlyAddress) || earlyAddress != 0x100000UL) return false;
        FirstFitKernelHeap heap = new(0xFFFF810000000000UL, 0x100000UL, 64);
        if (!heap.TryAllocate(KernelHeapAllocationRequest.Default(256UL), out KernelHeapAllocation allocation)) return false;
        if (allocation.Address < 0xFFFF810000000000UL) return false;
        if (!heap.TryRelease(allocation)) return false;
        return heap.GetStatistics().AllocatedBytes == 0UL;
    }
}
