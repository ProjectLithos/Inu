using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Memory;

/// <summary>Reports the result of a freestanding physical-memory operation.</summary>
public enum KernelPhysicalMemoryStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The supplied boot map or allocation request was invalid.</summary>
    InvalidParameter = 1,
    /// <summary>The physical-memory manager has not been initialized.</summary>
    NotInitialized = 2,
    /// <summary>The physical-memory manager was already initialized.</summary>
    AlreadyInitialized = 3,
    /// <summary>The fixed early-boot extent table cannot represent another free range.</summary>
    ExtentCapacityExhausted = 4,
    /// <summary>The bounded live-allocation table is full.</summary>
    AllocationCapacityExhausted = 5,
    /// <summary>No free physical extent can satisfy the request.</summary>
    OutOfMemory = 6,
    /// <summary>The allocation token is unknown or has already been released.</summary>
    AllocationNotFound = 7
}

/// <summary>Identifies one live contiguous physical-frame allocation.</summary>
public readonly struct KernelPhysicalAllocation
{
    /// <summary>Creates an allocation descriptor whose values are validated by release operations.</summary>
    public KernelPhysicalAllocation(UInt64 token, UInt64 startAddress, UInt64 pageCount)
    {
        Token = token;
        StartAddress = startAddress;
        PageCount = pageCount;
    }

    /// <summary>Gets the opaque allocation token.</summary>
    public UInt64 Token { get; }
    /// <summary>Gets the physical address of the first allocated 4 KiB frame.</summary>
    public UInt64 StartAddress { get; }
    /// <summary>Gets the number of contiguous allocated frames.</summary>
    public UInt64 PageCount { get; }
}

/// <summary>Provides an immutable snapshot of early physical-memory accounting.</summary>
public readonly struct KernelPhysicalMemoryStatistics
{
    internal KernelPhysicalMemoryStatistics(UInt64 managedPages, UInt64 freePages, UInt64 allocatedPages, UInt64 largestFreeExtentPages, Int32 freeExtentCount, Int32 liveAllocationCount)
    {
        ManagedPages = managedPages;
        FreePages = freePages;
        AllocatedPages = allocatedPages;
        LargestFreeExtentPages = largestFreeExtentPages;
        FreeExtentCount = freeExtentCount;
        LiveAllocationCount = liveAllocationCount;
    }

    /// <summary>Gets the number of immediately allocatable pages discovered at boot.</summary>
    public UInt64 ManagedPages { get; }
    /// <summary>Gets the number of currently free managed pages.</summary>
    public UInt64 FreePages { get; }
    /// <summary>Gets the number of pages currently owned by live allocations.</summary>
    public UInt64 AllocatedPages { get; }
    /// <summary>Gets the number of managed pages permanently excluded from ordinary allocation.</summary>
    public UInt64 ReservedPages => ManagedPages - FreePages - AllocatedPages;
    /// <summary>Gets the largest currently free contiguous extent in pages.</summary>
    public UInt64 LargestFreeExtentPages { get; }
    /// <summary>Gets the number of current free extents.</summary>
    public Int32 FreeExtentCount { get; }
    /// <summary>Gets the number of live allocations.</summary>
    public Int32 LiveAllocationCount { get; }
}

/// <summary>
/// Provides the default no-heap physical-frame manager used by the editable freestanding kernel template.
/// </summary>
/// <remarks>
/// The early manager consumes only UEFI ConventionalMemory. BootServicesCode and BootServicesData remain
/// deferred even after ExitBootServices because firmware-provided bootstrap state, including the inherited
/// stack, can still occupy those pages. Loader, runtime, ACPI, MMIO, framebuffer and defective ranges also
/// remain unavailable. Metadata lives in fixed kernel storage, so initialization never depends on a heap.
/// </remarks>
public static unsafe partial class KernelPhysicalMemory
{
    private const UInt64 PageSize = 4096UL;
    private const Int32 MaximumExtents = 512;
    private const Int32 MaximumAllocations = 256;
    private const UInt32 UefiConventionalMemory = 7U;
    private const UInt64 UefiRuntimeAttribute = 0x8000000000000000UL;

    private unsafe struct State
    {
        internal fixed UInt64 ExtentStarts[MaximumExtents];
        internal fixed UInt64 ExtentPages[MaximumExtents];
        internal fixed UInt64 AllocationTokens[MaximumAllocations];
        internal fixed UInt64 AllocationStarts[MaximumAllocations];
        internal fixed UInt64 AllocationPages[MaximumAllocations];
        internal fixed Byte AllocationActive[MaximumAllocations];
    }

    #pragma warning disable CS0169 // Roslyn does not count fixed-buffer member access as use of the containing freestanding state field.
    private static State _state;
    #pragma warning restore CS0169
    private static Int32 _extentCount;
    private static Int32 _liveAllocationCount;
    private static UInt64 _managedPages;
    private static UInt64 _freePages;
    private static UInt64 _allocatedPages;
    private static UInt64 _nextToken;
    private static UInt64 _bootstrapWorkspaceAddress;
    private static UInt64 _bootstrapWorkspacePages;
    private static UInt64 _bootstrapWorkspaceUsedPages;
    private static Boolean _initialized;
    private static KernelPhysicalMemoryStatus _lastStatus;

    /// <summary>Gets whether the default early physical-memory manager is initialized.</summary>
}
