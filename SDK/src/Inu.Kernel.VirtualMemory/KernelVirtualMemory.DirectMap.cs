using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    private const Int32 MaximumDirectMapExtents = 512;

    private unsafe struct DirectMapState
    {
        internal fixed UInt64 Starts[MaximumDirectMapExtents];
        internal fixed UInt64 Pages[MaximumDirectMapExtents];
    }

#pragma warning disable CS0169 // Fixed-buffer access is not counted as use of the containing freestanding state field.
    private static DirectMapState _directMapState;
#pragma warning restore CS0169
    private static Int32 _directMapExtentCount;
    private static UInt64 _directMapBase;
    private static UInt64 _directMapPhysicalLimit;
    private static UInt64 _bootstrapTablePages;
    private static Boolean _directMapReady;

    /// <summary>Gets whether the permanent direct map for PMM-managed RAM has been established.</summary>
    public static Boolean IsDirectMapInitialized() => _directMapReady;

    /// <summary>Gets the exclusive highest physical byte covered by the calculated PMM direct-map plan.</summary>
    public static UInt64 GetDirectMapPhysicalLimit() => _directMapPhysicalLimit;

    /// <summary>Gets the number of currently reachable PMM pages consumed to bootstrap new page-table levels.</summary>
    public static UInt64 GetBootstrapPageTableCount() => _bootstrapTablePages;

    /// <summary>Resolves one physical byte only when that byte is actually present in Inu's permanent direct-map plan.</summary>
    /// <remarks>The 64-TiB direct-map reservation is sparse: PMM extents and the bootstrap page-table workspace are mapped, not every physical byte below the reservation limit.</remarks>
    public static Boolean TryGetDirectMapAddress(UInt64 physicalAddress, out UInt64 virtualAddress)
    {
        virtualAddress = 0UL;
        if (!_directMapReady || !IsPhysicalAddressDirectMapped(physicalAddress)) return false;
        virtualAddress = _directMapBase + physicalAddress;
        return true;
    }

    /// <summary>Resolves a page-table page to a kernel-accessible virtual address without inventing sparse direct-map mappings.</summary>
    /// <remarks>Inherited firmware page tables protected during VM attach remain reachable through the preserved identity map; PMM-owned tables use the permanent direct map.</remarks>
    public static Boolean TryGetPageTableAccessAddress(UInt64 physicalAddress, out UInt64 virtualAddress)
    {
        virtualAddress = 0UL;
        if (!_initialized || (physicalAddress & 0xFFFUL) != 0UL) return false;
        if (WasProtected(physicalAddress))
        {
            virtualAddress = physicalAddress;
            return true;
        }
        if (TryGetDirectMapAddress(physicalAddress, out virtualAddress)) return true;
        if (!_directMapReady && IsIdentityWritable(physicalAddress))
        {
            virtualAddress = physicalAddress;
            return true;
        }
        return false;
    }

    /// <summary>Builds the direct map from the PMM free extents derived from the final normalized UEFI memory map.</summary>
    /// <param name="virtualBase">Virtual base reserved for the physical direct map.</param>
    /// <param name="virtualLength">Maximum physical span representable by that reservation.</param>
    /// <returns><see langword="true"/> when every currently allocatable PMM range is directly mapped.</returns>
    public static Boolean InitializeDirectMap(UInt64 virtualBase, UInt64 virtualLength)
    {
        if (!_initialized) return SetFailure(KernelVirtualMemoryStatus.NotInitialized);
        if (_directMapReady) return SetFailure(KernelVirtualMemoryStatus.AlreadyInitialized);
        if (!IsCanonical(virtualBase) || virtualBase < 0xFFFF800000000000UL || virtualLength == 0UL || (virtualBase & 0xFFFUL) != 0UL || (virtualLength & 0xFFFUL) != 0UL)
            return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);

        if (_directMapExtentCount <= 0) return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
        if (!AdoptPrivateRoot()) return false;

        UInt64 physicalLimit = 0UL;
        fixed (UInt64* starts = _directMapState.Starts)
        fixed (UInt64* pages = _directMapState.Pages)
        {
            for (Int32 index = 0; index < _directMapExtentCount; index++)
            {
                UInt64 start = starts[index];
                UInt64 count = pages[index];
                UInt64 bytes = count * 4096UL;
                UInt64 end = start + bytes;
                if (end > virtualLength) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
                if (end > physicalLimit) physicalLimit = end;
            }

            _directMapBase = virtualBase;
            for (Int32 index = 0; index < _directMapExtentCount; index++)
                if (!MapDirectExtent(virtualBase, starts[index], pages[index])) return false;

            UInt64 workspaceAddress = KernelPhysicalMemory.GetBootstrapPageTableWorkspaceAddress();
            UInt64 workspacePages = KernelPhysicalMemory.GetBootstrapPageTableWorkspacePages();
            if (workspaceAddress == 0UL || workspacePages == 0UL || !MapDirectExtent(virtualBase, workspaceAddress, workspacePages))
                return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
            UInt64 workspaceEnd = workspaceAddress + workspacePages * 4096UL;
            if (workspaceEnd > virtualLength) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
            if (workspaceEnd > physicalLimit) physicalLimit = workspaceEnd;
        }

        _directMapPhysicalLimit = physicalLimit;
        _directMapReady = true;
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    private static Boolean CaptureDirectMapPlan()
    {
        Int32 extentCount = KernelPhysicalMemory.GetFreeExtentCount();
        if (extentCount <= 0 || extentCount > MaximumDirectMapExtents)
            return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
        fixed (UInt64* starts = _directMapState.Starts)
        fixed (UInt64* pages = _directMapState.Pages)
        {
            for (Int32 index = 0; index < extentCount; index++)
            {
                if (!KernelPhysicalMemory.TryGetFreeExtent(index, out UInt64 start, out UInt64 count))
                    return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
                if (count == 0UL || count > 0xFFFFFFFFFFFFFFFFUL / 4096UL || start > 0xFFFFFFFFFFFFFFFFUL - count * 4096UL)
                    return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
                starts[index] = start;
                pages[index] = count;
            }
        }
        _directMapExtentCount = extentCount;
        return true;
    }

    private static Boolean IsPhysicalAddressDirectMapped(UInt64 physicalAddress)
    {
        fixed (UInt64* starts = _directMapState.Starts)
        fixed (UInt64* pages = _directMapState.Pages)
        {
            for (Int32 index = 0; index < _directMapExtentCount; index++)
            {
                UInt64 start = starts[index];
                UInt64 bytes = pages[index] * 4096UL;
                if (physicalAddress >= start && physicalAddress - start < bytes) return true;
            }
        }
        UInt64 workspaceAddress = KernelPhysicalMemory.GetBootstrapPageTableWorkspaceAddress();
        UInt64 workspacePages = KernelPhysicalMemory.GetBootstrapPageTableWorkspacePages();
        if (workspaceAddress != 0UL && workspacePages != 0UL)
        {
            UInt64 bytes = workspacePages * 4096UL;
            if (physicalAddress >= workspaceAddress && physicalAddress - workspaceAddress < bytes) return true;
        }
        return false;
    }

    private static Boolean MapDirectExtent(UInt64 virtualBase, UInt64 physicalStart, UInt64 pageCount)
    {
        KernelVirtualMemoryProtection protection = KernelVirtualMemoryProtection.Read | KernelVirtualMemoryProtection.Write | KernelVirtualMemoryProtection.Global;
        if (!_executeDisableEnabled) protection = protection | KernelVirtualMemoryProtection.Execute;
        UInt64 physical = physicalStart;
        UInt64 remaining = pageCount * 4096UL;
        while (remaining != 0UL)
        {
            KernelVirtualPageSize pageSize = KernelVirtualPageSize.Page4KiB;
            UInt64 bytes = 4096UL;
            UInt64 virtualAddress = virtualBase + physical;
            if (_page1GiBSupported && remaining >= 1073741824UL && (physical & 0x3FFFFFFFUL) == 0UL && (virtualAddress & 0x3FFFFFFFUL) == 0UL)
            {
                pageSize = KernelVirtualPageSize.Page1GiB;
                bytes = 1073741824UL;
            }
            else if (remaining >= 2097152UL && (physical & 0x1FFFFFUL) == 0UL && (virtualAddress & 0x1FFFFFUL) == 0UL)
            {
                pageSize = KernelVirtualPageSize.Page2MiB;
                bytes = 2097152UL;
            }
            if (!TryMap(virtualAddress, physical, pageSize, protection)) return false;
            physical += bytes;
            remaining -= bytes;
        }
        return true;
    }

    private static Boolean AdoptPrivateRoot()
    {
        UInt64 inheritedRoot = _rootPhysicalAddress;
        UInt64* inherited = (UInt64*)(nuint)inheritedRoot;
        if (!TryAllocatePageTable(out UInt64 ownedPhysicalAddress))
            return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
        UInt64* owned = (UInt64*)(nuint)ownedPhysicalAddress;
        for (Int32 index = 0; index < 512; index++) owned[index] = inherited[index];
        if (!Native.WritePageTableRoot(ownedPhysicalAddress))
            return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        _rootPhysicalAddress = ownedPhysicalAddress;
        _createdPageTables++;
        return true;
    }

    private static Boolean TryAllocatePageTable(out UInt64 physicalAddress)
    {
        physicalAddress = 0UL;
        if (_directMapReady)
        {
            if (!KernelPhysicalMemory.TryAllocate(1UL, 1UL, out KernelPhysicalAllocation allocation)) return false;
            physicalAddress = allocation.StartAddress;
            return true;
        }
        if (!KernelPhysicalMemory.TryTakeBootstrapPageTable(out physicalAddress)) return false;
        _bootstrapTablePages++;
        return true;
    }

    private static Boolean IsIdentityWritable(UInt64 physicalAddress)
    {
        UInt64* pml4 = (UInt64*)(nuint)_rootPhysicalAddress;
        UInt64 pml4Entry = pml4[(Int32)((physicalAddress >> 39) & 0x1FFUL)];
        if (!IsPresent(pml4Entry) || IsLarge(pml4Entry) || (pml4Entry & Writable) == 0UL) return false;
        UInt64* pdpt = (UInt64*)(nuint)(pml4Entry & AddressMask4KiB);
        UInt64 pdptEntry = pdpt[(Int32)((physicalAddress >> 30) & 0x1FFUL)];
        if (!IsPresent(pdptEntry) || (pdptEntry & Writable) == 0UL) return false;
        if (IsLarge(pdptEntry)) return ((pdptEntry & AddressMask1GiB) + (physicalAddress & 0x3FFFFFFFUL)) == physicalAddress;
        UInt64* pd = (UInt64*)(nuint)(pdptEntry & AddressMask4KiB);
        UInt64 pdEntry = pd[(Int32)((physicalAddress >> 21) & 0x1FFUL)];
        if (!IsPresent(pdEntry) || (pdEntry & Writable) == 0UL) return false;
        if (IsLarge(pdEntry)) return ((pdEntry & AddressMask2MiB) + (physicalAddress & 0x1FFFFFUL)) == physicalAddress;
        UInt64* pt = (UInt64*)(nuint)(pdEntry & AddressMask4KiB);
        UInt64 ptEntry = pt[(Int32)((physicalAddress >> 12) & 0x1FFUL)];
        return IsPresent(ptEntry) && (ptEntry & Writable) != 0UL &&
            ((ptEntry & AddressMask4KiB) + (physicalAddress & 0xFFFUL)) == physicalAddress;
    }

    private static Boolean IsTableWritableBeforeDirectMap(UInt64 physicalAddress)
        => _directMapReady || IsIdentityWritable(physicalAddress);

    private static Boolean ResetDirectMapState()
    {
        _directMapExtentCount = 0;
        _directMapBase = 0UL;
        _directMapPhysicalLimit = 0UL;
        _bootstrapTablePages = 0UL;
        _directMapReady = false;
        fixed (UInt64* starts = _directMapState.Starts)
        fixed (UInt64* pages = _directMapState.Pages)
        {
            for (Int32 index = 0; index < MaximumDirectMapExtents; index++)
            {
                starts[index] = 0UL;
                pages[index] = 0UL;
            }
        }
        return true;
    }
}
