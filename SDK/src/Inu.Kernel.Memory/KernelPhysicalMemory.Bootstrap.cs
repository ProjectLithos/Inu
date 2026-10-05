using System;

namespace Inu.Kernel.Memory;

public static unsafe partial class KernelPhysicalMemory
{

    /// <summary>Gets the physical base of the UEFI-reserved bootstrap page-table workspace.</summary>
    public static UInt64 GetBootstrapPageTableWorkspaceAddress() => _initialized ? _bootstrapWorkspaceAddress : 0UL;

    /// <summary>Gets the total UEFI-reserved bootstrap page-table workspace size in 4 KiB pages.</summary>
    public static UInt64 GetBootstrapPageTableWorkspacePages() => _initialized ? _bootstrapWorkspacePages : 0UL;

    /// <summary>Gets the number of bootstrap page-table workspace pages consumed so far.</summary>
    public static UInt64 GetBootstrapPageTableWorkspaceUsedPages() => _initialized ? _bootstrapWorkspaceUsedPages : 0UL;

    /// <summary>Claims one page from the UEFI-reserved page-table workspace before the direct map exists.</summary>
    /// <param name="physicalAddress">Receives the physical address of the writable, already-reachable page.</param>
    /// <returns><see langword="true"/> when a reserved workspace page remains available.</returns>
    public static Boolean TryTakeBootstrapPageTable(out UInt64 physicalAddress)
    {
        physicalAddress = 0UL;
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        if (_bootstrapWorkspaceUsedPages >= _bootstrapWorkspacePages) return SetFailure(KernelPhysicalMemoryStatus.OutOfMemory);
        UInt64 offset = _bootstrapWorkspaceUsedPages * PageSize;
        if (_bootstrapWorkspaceAddress > 0xFFFFFFFFFFFFFFFFUL - offset) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        physicalAddress = _bootstrapWorkspaceAddress + offset;
        _bootstrapWorkspaceUsedPages++;
        _lastStatus = KernelPhysicalMemoryStatus.Success;
        return true;
    }
    /// <summary>Gets the number of currently free physical extents known to the early allocator.</summary>
    public static Int32 GetFreeExtentCount() => _initialized ? _extentCount : 0;

    /// <summary>Reads one current free physical extent without allocating it.</summary>
    /// <param name="index">Zero-based free-extent index.</param>
    /// <param name="startAddress">Receives the first physical byte address.</param>
    /// <param name="pageCount">Receives the number of contiguous 4 KiB pages.</param>
    /// <returns><see langword="true"/> when the requested extent exists.</returns>
    public static Boolean TryGetFreeExtent(Int32 index, out UInt64 startAddress, out UInt64 pageCount)
    {
        startAddress = 0UL;
        pageCount = 0UL;
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        if (index < 0 || index >= _extentCount) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            startAddress = starts[index] * PageSize;
            pageCount = pages[index];
        }
        _lastStatus = KernelPhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Allocates an exact free physical range selected by an early bootstrap consumer.</summary>
    /// <param name="physicalAddress">4 KiB-aligned physical start address.</param>
    /// <param name="pageCount">Number of contiguous pages to claim.</param>
    /// <param name="allocation">Receives the live allocation token and range.</param>
    /// <returns><see langword="true"/> when the exact range was free and could be split from its extent.</returns>
    public static Boolean TryAllocateAt(UInt64 physicalAddress, UInt64 pageCount, out KernelPhysicalAllocation allocation)
    {
        allocation = default;
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        if (pageCount == 0UL || (physicalAddress & 0xFFFUL) != 0UL) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        UInt64 startFrame = physicalAddress / PageSize;
        if (startFrame > 0xFFFFFFFFFFFFFFFFUL - pageCount) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        Int32 record = FindFreeAllocationRecord();
        if (record < 0) return SetFailure(KernelPhysicalMemoryStatus.AllocationCapacityExhausted);
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        fixed (UInt64* tokens = _state.AllocationTokens)
        fixed (UInt64* allocationStarts = _state.AllocationStarts)
        fixed (UInt64* allocationPages = _state.AllocationPages)
        fixed (Byte* active = _state.AllocationActive)
        {
            for (Int32 index = 0; index < _extentCount; index++)
            {
                UInt64 extentStart = starts[index];
                UInt64 extentPages = pages[index];
                if (startFrame < extentStart) continue;
                UInt64 prefix = startFrame - extentStart;
                if (prefix > extentPages || pageCount > extentPages - prefix) continue;
                UInt64 suffix = extentPages - prefix - pageCount;
                if (prefix != 0UL && suffix != 0UL && _extentCount >= MaximumExtents)
                    return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                if (!ReplaceAllocatedExtent(index, extentStart, prefix, startFrame + pageCount, suffix))
                    return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                UInt64 token = NextToken();
                tokens[record] = token;
                allocationStarts[record] = startFrame;
                allocationPages[record] = pageCount;
                active[record] = 1;
                _liveAllocationCount++;
                _freePages -= pageCount;
                _allocatedPages += pageCount;
                allocation = new KernelPhysicalAllocation(token, physicalAddress, pageCount);
                _lastStatus = KernelPhysicalMemoryStatus.Success;
                return true;
            }
        }
        return SetFailure(KernelPhysicalMemoryStatus.OutOfMemory);
    }
}
