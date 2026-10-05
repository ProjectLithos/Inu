using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Heap;

public static unsafe partial class KernelHeap
{
    public static Boolean TryAllocate(UInt64 byteCount, UInt64 alignment, Boolean zeroFill, out KernelHeapAllocation allocation)
    {
        allocation = default;
        if (KernelFaultInjection.ShouldInject(KernelFaultKind.AllocationFailure,"heap",out _))
        {
            _status = KernelHeapStatus.OutOfMemory;
            return false;
        }
        if (!_initialized)
        {
            _status = KernelHeapStatus.DependencyNotInitialized;
            return false;
        }
        if (byteCount == 0UL || alignment == 0UL || (alignment & (alignment - 1UL)) != 0UL || alignment > PageSize)
        {
            _status = KernelHeapStatus.InvalidParameter;
            return false;
        }
        if (byteCount > 0xFFFFFFFFFFFFFFFFUL - (alignment - 1UL))
        {
            _status = KernelHeapStatus.InvalidParameter;
            return false;
        }

        for (Int32 attempt = 0; attempt < 2; attempt++)
        {
            if (TryAllocateExisting(byteCount, alignment, zeroFill, out allocation)) return true;
            UInt64 required = byteCount + alignment - 1UL;
            if (required > 0xFFFFFFFFFFFFFFFFUL - (PageSize - 1UL))
            {
                _status = KernelHeapStatus.InvalidParameter;
                return false;
            }
            UInt64 pages = (required + PageSize - 1UL) / PageSize;
            if (pages < GrowthPages) pages = GrowthPages;
            if (!Grow(pages)) return false;
        }
        _status = KernelHeapStatus.OutOfMemory;
        return false;
    }

    /// <summary>Releases one exact live allocation and coalesces adjacent free blocks.</summary>
    /// <returns><see langword="true"/> when the allocation was live.</returns>
    public static Boolean TryRelease(KernelHeapAllocation allocation)
    {
        if (!_initialized)
        {
            _status = KernelHeapStatus.DependencyNotInitialized;
            return false;
        }
        State* state = GetState();
        UInt64* starts = state->Starts;
        UInt64* lengths = state->Lengths;
        UInt64* tokens = state->Tokens;
        Byte* states = state->States;
        {
            for (Int32 i = 0; i < MaximumBlocks; i++)
            {
                if (states[i] != 2 || tokens[i] != allocation.Token || starts[i] != allocation.Address || lengths[i] != allocation.ByteCount) continue;
                UInt64 releasedToken = tokens[i];
                states[i] = 1;
                tokens[i] = 0UL;
                _allocated -= lengths[i];
                _live--;
                OnAllocationReleased(releasedToken);
                Coalesce();
                _status = KernelHeapStatus.Success;
                SynchronizeDiagnosticHeader();
                return true;
            }
        }
        if (WasReleasedToken(allocation.Token))
        {
            _doubleFreeFailures++;
            _status = KernelHeapStatus.DoubleFreeDetected;
            return false;
        }
        _status = KernelHeapStatus.AllocationNotFound;
        return false;
    }

    /// <summary>Gets current heap accounting.</summary>
    /// <returns>An immutable statistics snapshot.</returns>
    public static KernelHeapStatistics GetStatistics()
    {
        if (!_diagnosticMetadataReady) return new KernelHeapStatistics(0UL, 0UL, 0UL, 0UL, 0, 0);
        UInt64 free = 0UL;
        Int32 blocks = 0;
        State* state = GetState();
        UInt64* lengths = state->Lengths;
        Byte* states = state->States;
        {
            for (Int32 i = 0; i < MaximumBlocks; i++)
            {
                if (states[i] != 1) continue;
                free += lengths[i];
                blocks++;
            }
        }
        return new KernelHeapStatistics(_committed, _allocated, free, _peak, _live, blocks);
    }
}
