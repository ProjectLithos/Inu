using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Memory;

public static unsafe partial class KernelPhysicalMemory
{
    private static Boolean InsertFreeExtent(UInt64 startFrame, UInt64 pageCount)
    {
        if (pageCount == 0UL || startFrame > 0xFFFFFFFFFFFFFFFFUL - pageCount) return false;
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            Int32 insert = 0;
            while (insert < _extentCount && starts[insert] < startFrame) insert++;
            if (_extentCount >= MaximumExtents) return false;
            for (Int32 move = _extentCount; move > insert; move--)
            {
                starts[move] = starts[move - 1];
                pages[move] = pages[move - 1];
            }
            starts[insert] = startFrame;
            pages[insert] = pageCount;
            _extentCount++;
            return CoalesceAround(insert);
        }
    }

    private static Boolean CoalesceAround(Int32 index)
    {
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            if (index > 0)
            {
                UInt64 previousEnd = starts[index - 1] + pages[index - 1];
                if (previousEnd > starts[index]) return false;
                if (previousEnd == starts[index])
                {
                    pages[index - 1] += pages[index];
                    RemoveExtent(index);
                    index--;
                }
            }
            if (index + 1 < _extentCount)
            {
                UInt64 end = starts[index] + pages[index];
                if (end > starts[index + 1]) return false;
                if (end == starts[index + 1])
                {
                    pages[index] += pages[index + 1];
                    RemoveExtent(index + 1);
                }
            }
        }
        return true;
    }

    private static Boolean ReplaceAllocatedExtent(Int32 index, UInt64 originalStart, UInt64 prefixPages, UInt64 suffixStart, UInt64 suffixPages)
    {
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            if (prefixPages == 0UL && suffixPages == 0UL) return RemoveExtent(index);
            if (prefixPages == 0UL)
            {
                starts[index] = suffixStart;
                pages[index] = suffixPages;
                return true;
            }
            starts[index] = originalStart;
            pages[index] = prefixPages;
            if (suffixPages == 0UL) return true;
            for (Int32 move = _extentCount; move > index + 1; move--)
            {
                starts[move] = starts[move - 1];
                pages[move] = pages[move - 1];
            }
            starts[index + 1] = suffixStart;
            pages[index + 1] = suffixPages;
            _extentCount++;
            return true;
        }
    }

    private static Boolean RemoveExtent(Int32 index)
    {
        fixed (UInt64* starts = _state.ExtentStarts)
        fixed (UInt64* pages = _state.ExtentPages)
        {
            for (Int32 move = index; move + 1 < _extentCount; move++)
            {
                starts[move] = starts[move + 1];
                pages[move] = pages[move + 1];
            }
            _extentCount--;
            if (_extentCount >= 0)
            {
                starts[_extentCount] = 0UL;
                pages[_extentCount] = 0UL;
            }
        }
        return true;
    }
}
