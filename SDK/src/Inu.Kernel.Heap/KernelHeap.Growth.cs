using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Heap;

public static unsafe partial class KernelHeap
{
    private static Boolean TryAllocateExisting(UInt64 bytes, UInt64 alignment, Boolean zeroFill, out KernelHeapAllocation allocation)
    {
        allocation = default;
        State* state = GetState();
        UInt64* starts = state->Starts;
        UInt64* lengths = state->Lengths;
        UInt64* tokens = state->Tokens;
        Byte* states = state->States;
        {
            for (Int32 i = 0; i < MaximumBlocks; i++)
            {
                if (states[i] != 1) continue;
                UInt64 start = starts[i];
                UInt64 length = lengths[i];
                UInt64 mask = alignment - 1UL;
                if (start > 0xFFFFFFFFFFFFFFFFUL - mask) continue;
                UInt64 aligned = (start + mask) & ~mask;
                UInt64 prefix = aligned - start;
                if (prefix > length || bytes > length - prefix) continue;
                UInt64 suffix = length - prefix - bytes;
                Int32 firstSlot = prefix > 0UL ? FindUnused(i, -1) : -1;
                Int32 secondSlot = suffix > 0UL ? FindUnused(i, firstSlot) : -1;
                if ((prefix > 0UL && firstSlot < 0) || (suffix > 0UL && secondSlot < 0))
                {
                    _status = KernelHeapStatus.MetadataCapacityExhausted;
                    return false;
                }

                UInt64 token = NextToken();
                states[i] = 2;
                starts[i] = aligned;
                lengths[i] = bytes;
                tokens[i] = token;
                if (prefix > 0UL)
                {
                    states[firstSlot] = 1;
                    starts[firstSlot] = start;
                    lengths[firstSlot] = prefix;
                }
                if (suffix > 0UL)
                {
                    states[secondSlot] = 1;
                    starts[secondSlot] = aligned + bytes;
                    lengths[secondSlot] = suffix;
                }
                _allocated += bytes;
                if (_allocated > _peak) _peak = _allocated;
                _live++;
                if (zeroFill)
                {
                    Byte* pointer = (Byte*)(nuint)aligned;
                    for (UInt64 n = 0UL; n < bytes; n++) pointer[n] = 0;
                }
                allocation = new KernelHeapAllocation(token, aligned, bytes);
                OnAllocationCreated(token, aligned, bytes);
                _status = KernelHeapStatus.Success;
                SynchronizeDiagnosticHeader();
                return true;
            }
        }
        return false;
    }

    private static Boolean Grow(UInt64 pages)
    {
        if (pages == 0UL || pages > 0xFFFFFFFFFFFFFFFFUL / PageSize)
        {
            _status = KernelHeapStatus.InvalidParameter;
            return false;
        }
        UInt64 bytes = pages * PageSize;
        if (_committed > AllocatableHeapLength || bytes > AllocatableHeapLength - _committed)
        {
            _status = KernelHeapStatus.OutOfMemory;
            return false;
        }
        Int32 slot = FindUnused(-1, -1);
        if (slot < 0)
        {
            _status = KernelHeapStatus.MetadataCapacityExhausted;
            return false;
        }
        if (!KernelPhysicalMemory.TryAllocate(pages, 1UL, out KernelPhysicalAllocation physical))
        {
            _status = KernelHeapStatus.OutOfMemory;
            return false;
        }

        UInt64 mapped = 0UL;
        KernelVirtualMemoryProtection protection = KernelVirtualMemoryProtection.Read | KernelVirtualMemoryProtection.Write | KernelVirtualMemoryProtection.Global;
        for (UInt64 page = 0UL; page < pages; page++)
        {
            UInt64 virtualAddress = KernelAddressSpace.KernelHeapBase + _committed + page * PageSize;
            UInt64 physicalAddress = physical.StartAddress + page * PageSize;
            if (!KernelVirtualMemory.TryMap(virtualAddress, physicalAddress, KernelVirtualPageSize.Page4KiB, protection))
            {
                for (UInt64 undo = 0UL; undo < mapped; undo++)
                    KernelVirtualMemory.TryUnmap(KernelAddressSpace.KernelHeapBase + _committed + undo * PageSize);
                KernelPhysicalMemory.TryRelease(physical);
                _status = KernelHeapStatus.MappingFailed;
                return false;
            }
            mapped++;
        }

        State* state = GetState();
        UInt64* starts = state->Starts;
        UInt64* lengths = state->Lengths;
        Byte* states = state->States;
        {
            states[slot] = 1;
            starts[slot] = KernelAddressSpace.KernelHeapBase + _committed;
            lengths[slot] = bytes;
        }
        _committed += bytes;
        Coalesce();
        SynchronizeDiagnosticHeader();
        return true;
    }

    private static Int32 FindUnused(Int32 excluded, Int32 excluded2)
    {
        State* state = GetState();
        Byte* states = state->States;
        {
            for (Int32 i = 0; i < MaximumBlocks; i++)
                if (i != excluded && i != excluded2 && states[i] == 0) return i;
        }
        return -1;
    }
}
