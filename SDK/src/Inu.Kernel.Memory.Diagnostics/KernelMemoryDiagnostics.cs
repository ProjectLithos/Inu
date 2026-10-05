using System;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Memory.Diagnostics;

/// <summary>Physical allocator statistics normalized for SDK diagnostics.</summary>
public readonly struct KernelPhysicalAllocatorDiagnosticStatistics
{
    internal KernelPhysicalAllocatorDiagnosticStatistics(KernelPhysicalMemoryStatistics source)
    { TotalPages=source.ManagedPages; FreePages=source.FreePages; ReservedPages=source.ReservedPages; AllocatedPages=source.AllocatedPages; LargestFreeExtentPages=source.LargestFreeExtentPages; FreeExtentCount=source.FreeExtentCount; LiveAllocationCount=source.LiveAllocationCount; }
    public UInt64 TotalPages { get; }
    public UInt64 FreePages { get; }
    public UInt64 ReservedPages { get; }
    public UInt64 AllocatedPages { get; }
    public UInt64 LargestFreeExtentPages { get; }
    public Int32 FreeExtentCount { get; }
    public Int32 LiveAllocationCount { get; }
}

/// <summary>Single SDK-side facade for kernel memory diagnostics.</summary>
public static class KernelMemoryDiagnostics
{
    public static Boolean TryGetPhysicalAllocatorStatistics(out KernelPhysicalAllocatorDiagnosticStatistics statistics)
    {
        statistics=default; if(!KernelPhysicalMemory.IsInitialized())return false;
        statistics=new KernelPhysicalAllocatorDiagnosticStatistics(KernelPhysicalMemory.GetStatistics()); return true;
    }
    public static Boolean TryGetHeapDiagnostics(UInt64 leakCheckpoint, out KernelHeapDiagnosticSnapshot diagnostics)
    { diagnostics=default; if(!KernelHeap.IsInitialized())return false; diagnostics=KernelHeap.GetDiagnosticSnapshot(leakCheckpoint); return true; }
    public static Boolean TryInspectPageTable(UInt64 virtualAddress, out KernelPageTableInspection inspection) => KernelVirtualMemory.TryInspectPageTable(virtualAddress,out inspection);
    public static Boolean TryCreateLeakCheckpoint(out UInt64 checkpoint) => KernelHeap.TryCreateLeakCheckpoint(out checkpoint);
    public static UInt64 GetLeakCandidateCount(UInt64 checkpoint) => KernelHeap.GetLeakCandidateCount(checkpoint);
    public static Boolean TryGetLeakCandidate(UInt64 checkpoint, UInt64 index, out KernelHeapAllocationInfo info) => KernelHeap.TryGetLeakCandidate(checkpoint,index,out info);
    public static Boolean TryAllocateGuarded(UInt64 byteCount, UInt64 alignment, Boolean zeroFill, String tag, out KernelGuardedHeapAllocation allocation) => KernelHeap.TryAllocateGuarded(byteCount,alignment,zeroFill,tag,out allocation);
    public static Boolean TryReleaseGuarded(KernelGuardedHeapAllocation allocation) => KernelHeap.TryReleaseGuarded(allocation);
    public static Boolean TrySetAllocationTag(KernelHeapAllocation allocation, String tag) => KernelHeap.TrySetAllocationTag(allocation,tag);
    public static Boolean TryGetAllocationTagHash(UInt64 token,out UInt64 tagHash) => KernelHeap.TryGetAllocationTagHash(token,out tagHash);
    public static Boolean TryValidateGuards(out UInt64 failures) => KernelHeap.TryValidateGuards(out failures);
}
