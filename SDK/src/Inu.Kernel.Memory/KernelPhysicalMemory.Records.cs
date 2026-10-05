using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Memory;

public static unsafe partial class KernelPhysicalMemory
{
    private static Int32 FindFreeAllocationRecord()
    {
        fixed (Byte* active = _state.AllocationActive)
        {
            for (Int32 index = 0; index < MaximumAllocations; index++) if (active[index] == 0) return index;
        }
        return -1;
    }

    private static Boolean TryAlignFrame(UInt64 frame, UInt64 alignmentPages, out UInt64 aligned)
    {
        aligned = 0UL;
        UInt64 mask = alignmentPages - 1UL;
        if (frame > 0xFFFFFFFFFFFFFFFFUL - mask) return false;
        aligned = (frame + mask) & ~mask;
        return true;
    }

    private static UInt64 NextToken()
    {
        _nextToken++;
        if (_nextToken == 0UL) _nextToken++;
        return _nextToken;
    }

    private static Boolean SetFailure(KernelPhysicalMemoryStatus status)
    {
        _lastStatus = status;
        return false;
    }

    private static Boolean ResetState()
    {
        _extentCount = 0;
        _liveAllocationCount = 0;
        _managedPages = 0UL;
        _freePages = 0UL;
        _allocatedPages = 0UL;
        _nextToken = 0UL;
        _bootstrapWorkspaceAddress = 0UL;
        _bootstrapWorkspacePages = 0UL;
        _bootstrapWorkspaceUsedPages = 0UL;
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            for (Int32 index = 0; index < MaximumExtents; index++)
            {
                starts[index] = 0UL;
                pages[index] = 0UL;
            }
        }
        fixed (UInt64* tokens = _state.AllocationTokens)
        fixed (UInt64* starts = _state.AllocationStarts)
        fixed (UInt64* pages = _state.AllocationPages)
        fixed (Byte* active = _state.AllocationActive)
        {
            for (Int32 index = 0; index < MaximumAllocations; index++)
            {
                tokens[index] = 0UL;
                starts[index] = 0UL;
                pages[index] = 0UL;
                active[index] = 0;
            }
        }
        return true;
    }
}
