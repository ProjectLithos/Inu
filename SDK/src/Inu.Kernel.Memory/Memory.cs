using System;

namespace Inu.Kernel.Memory;

/// <summary><inu.api>Coder-facing physical-memory policy facade. Bootstrap owns initialization; kernel policy can inspect and allocate/release pages without depending on KernelPhysicalMemory internals.</inu.api></summary>
public static class Memory
{
    public static Boolean IsInitialized()=>KernelPhysicalMemory.IsInitialized();
    public static KernelPhysicalMemoryStatistics GetStatistics()=>KernelPhysicalMemory.GetStatistics();
    public static Boolean TryAllocatePages(UInt64 pageCount,out KernelPhysicalAllocation allocation)=>KernelPhysicalMemory.TryAllocate(pageCount,1UL,out allocation);
    public static Boolean TryAllocatePages(UInt64 pageCount,UInt64 alignmentPages,out KernelPhysicalAllocation allocation)=>KernelPhysicalMemory.TryAllocate(pageCount,alignmentPages,out allocation);
    public static Boolean Release(KernelPhysicalAllocation allocation)=>KernelPhysicalMemory.TryRelease(allocation);
}
