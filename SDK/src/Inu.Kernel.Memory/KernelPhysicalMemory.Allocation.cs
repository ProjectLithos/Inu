using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Memory;

public static unsafe partial class KernelPhysicalMemory
{
    public static Boolean TryAllocate(UInt64 pageCount, UInt64 alignmentPages, out KernelPhysicalAllocation allocation)
    {
        allocation = default;
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        if (pageCount == 0UL || alignmentPages == 0UL || (alignmentPages & (alignmentPages - 1UL)) != 0UL)
            return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
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
                if (!TryAlignFrame(extentStart, alignmentPages, out UInt64 candidate)) continue;
                if (candidate < extentStart) continue;
                UInt64 prefix = candidate - extentStart;
                if (prefix > extentPages || pageCount > extentPages - prefix) continue;

                UInt64 suffix = extentPages - prefix - pageCount;
                if (prefix != 0UL && suffix != 0UL && _extentCount >= MaximumExtents)
                    return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                ReplaceAllocatedExtent(index, extentStart, prefix, candidate + pageCount, suffix);

                UInt64 token = NextToken();
                tokens[record] = token;
                allocationStarts[record] = candidate;
                allocationPages[record] = pageCount;
                active[record] = 1;
                _liveAllocationCount++;
                _freePages -= pageCount;
                _allocatedPages += pageCount;
                allocation = new KernelPhysicalAllocation(token, candidate * PageSize, pageCount);
                _lastStatus = KernelPhysicalMemoryStatus.Success;
                return true;
            }
        }
        return SetFailure(KernelPhysicalMemoryStatus.OutOfMemory);
    }

    /// <summary>Permanently excludes one 4 KiB physical page from ordinary early allocation when it is currently free.</summary>
    /// <param name="physicalAddress">The 4 KiB-aligned physical page address to protect.</param>
    /// <returns><see langword="true"/> when the page is excluded or was already unavailable to this manager.</returns>
    public static Boolean TryExcludePage(UInt64 physicalAddress)
    {
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        if ((physicalAddress & 0xFFFUL) != 0UL) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        UInt64 frame = physicalAddress / PageSize;
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            for (Int32 index = 0; index < _extentCount; index++)
            {
                UInt64 start = starts[index];
                UInt64 count = pages[index];
                if (frame < start || frame - start >= count) continue;
                UInt64 prefix = frame - start;
                UInt64 suffix = count - prefix - 1UL;
                if (prefix != 0UL && suffix != 0UL && _extentCount >= MaximumExtents)
                    return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                if (!ReplaceAllocatedExtent(index, start, prefix, frame + 1UL, suffix))
                    return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                _freePages--;
                _lastStatus = KernelPhysicalMemoryStatus.Success;
                return true;
            }
        }
        _lastStatus = KernelPhysicalMemoryStatus.Success;
        return true;
    }


    /// <summary>Releases a live physical allocation exactly once and coalesces adjacent free extents.</summary>
    /// <returns><see langword="true"/> when the token identifies an active allocation.</returns>
    public static Boolean TryRelease(KernelPhysicalAllocation allocation)
    {
        if (!_initialized) return SetFailure(KernelPhysicalMemoryStatus.NotInitialized);
        fixed (UInt64* tokens = _state.AllocationTokens)
        fixed (UInt64* starts = _state.AllocationStarts)
        fixed (UInt64* pages = _state.AllocationPages)
        fixed (Byte* active = _state.AllocationActive)
        {
            for (Int32 index = 0; index < MaximumAllocations; index++)
            {
                if (active[index] == 0 || tokens[index] != allocation.Token || starts[index] * PageSize != allocation.StartAddress || pages[index] != allocation.PageCount) continue;
                if (!InsertFreeExtent(starts[index], pages[index])) return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
                active[index] = 0;
                _liveAllocationCount--;
                _freePages += pages[index];
                _allocatedPages -= pages[index];
                _lastStatus = KernelPhysicalMemoryStatus.Success;
                return true;
            }
        }
        return SetFailure(KernelPhysicalMemoryStatus.AllocationNotFound);
    }

    /// <summary>Gets current physical-memory page accounting and free-extent diagnostics.</summary>
    public static KernelPhysicalMemoryStatistics GetStatistics()
    {
        if (!_initialized) return default;
        UInt64 largest = 0UL;
        fixed (UInt64* pages = _state.ExtentPages)
        {
            for (Int32 index = 0; index < _extentCount; index++) if (pages[index] > largest) largest = pages[index];
        }
        return new KernelPhysicalMemoryStatistics(_managedPages, _freePages, _allocatedPages, largest, _extentCount, _liveAllocationCount);
    }
}
