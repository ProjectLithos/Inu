using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    private static Boolean TryGetOrCreateChild(UInt64* table, Int32 index, KernelVirtualMemoryProtection protection, out UInt64* child)
    {
        child = (UInt64*)0;
        UInt64 entry = table[index];
        if (IsPresent(entry))
        {
            if (IsLarge(entry)) return SetFailure(KernelVirtualMemoryStatus.AlreadyMapped);
            if (HasProtection(protection, KernelVirtualMemoryProtection.Write) && (entry & Writable) == 0UL)
                return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
            if (HasProtection(protection, KernelVirtualMemoryProtection.User) && (entry & User) == 0UL)
                return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
            if (HasProtection(protection, KernelVirtualMemoryProtection.Execute) && (entry & NoExecute) != 0UL)
                return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
            UInt64 childPhysicalAddress = entry & AddressMask4KiB;
            if (!IsTableWritableBeforeDirectMap(childPhysicalAddress))
                return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
            child = TablePointer(childPhysicalAddress);
            return true;
        }

        if (!TryAllocatePageTable(out UInt64 createdPhysicalAddress))
            return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
        UInt64* created = TablePointer(createdPhysicalAddress);
        for (Int32 i = 0; i < 512; i++) created[i] = 0UL;
        table[index] = (createdPhysicalAddress & AddressMask4KiB) | Present | Writable | User;
        _createdPageTables++;
        child = created;
        return true;
    }

    private static Boolean TryInstallLeaf(UInt64* table, Int32 index, UInt64 virtualAddress, UInt64 physicalAddress, KernelVirtualPageSize pageSize, KernelVirtualMemoryProtection protection)
    {
        if (IsPresent(table[index])) return SetFailure(KernelVirtualMemoryStatus.AlreadyMapped);
        if (!TryEncodeLeaf(physicalAddress, pageSize, protection, out UInt64 entry))
            return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        table[index] = entry;
        IncrementMapped(pageSize);
        if (!Native.InvalidatePage(virtualAddress)) return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    private static Boolean TryFindLeaf(UInt64 virtualAddress, out UInt64* table, out Int32 index, out KernelVirtualPageSize pageSize)
    {
        table = (UInt64*)0;
        index = 0;
        pageSize = KernelVirtualPageSize.Page4KiB;
        if (!IsCanonical(virtualAddress)) return SetFailure(KernelVirtualMemoryStatus.NonCanonicalAddress);
        UInt64* pml4 = TablePointer(_rootPhysicalAddress);
        UInt64 pml4Entry = pml4[(Int32)((virtualAddress >> 39) & 0x1FFUL)];
        if (!IsPresent(pml4Entry) || IsLarge(pml4Entry)) return SetFailure(KernelVirtualMemoryStatus.NotMapped);
        UInt64* pdpt = TablePointer(pml4Entry & AddressMask4KiB);
        Int32 pdptIndex = (Int32)((virtualAddress >> 30) & 0x1FFUL);
        UInt64 pdptEntry = pdpt[pdptIndex];
        if (!IsPresent(pdptEntry)) return SetFailure(KernelVirtualMemoryStatus.NotMapped);
        if (IsLarge(pdptEntry))
        {
            table = pdpt;
            index = pdptIndex;
            pageSize = KernelVirtualPageSize.Page1GiB;
            return true;
        }
        UInt64* pd = TablePointer(pdptEntry & AddressMask4KiB);
        Int32 pdIndex = (Int32)((virtualAddress >> 21) & 0x1FFUL);
        UInt64 pdEntry = pd[pdIndex];
        if (!IsPresent(pdEntry)) return SetFailure(KernelVirtualMemoryStatus.NotMapped);
        if (IsLarge(pdEntry))
        {
            table = pd;
            index = pdIndex;
            pageSize = KernelVirtualPageSize.Page2MiB;
            return true;
        }
        UInt64* pt = TablePointer(pdEntry & AddressMask4KiB);
        Int32 ptIndex = (Int32)((virtualAddress >> 12) & 0x1FFUL);
        if (!IsPresent(pt[ptIndex])) return SetFailure(KernelVirtualMemoryStatus.NotMapped);
        table = pt;
        index = ptIndex;
        pageSize = KernelVirtualPageSize.Page4KiB;
        return true;
    }

    private static Boolean ProtectTableHierarchy(UInt64 physicalAddress, Int32 level)
    {
        if (WasProtected(physicalAddress)) return true;
        if (_protectedTableCount >= MaximumProtectedTables) return SetFailure(KernelVirtualMemoryStatus.PageTableCapacityExhausted);
        if (!KernelPhysicalMemory.TryExcludePage(physicalAddress)) return SetFailure(KernelVirtualMemoryStatus.PhysicalAllocationFailed);
        fixed (UInt64* protectedTables = _state.ProtectedTables)
        {
            protectedTables[_protectedTableCount] = physicalAddress;
            _protectedTableCount++;
        }
        if (level <= 1) return true;
        UInt64* table = TablePointer(physicalAddress);
        for (Int32 index = 0; index < 512; index++)
        {
            UInt64 entry = table[index];
            if (!IsPresent(entry)) continue;
            if ((level == 3 || level == 2) && IsLarge(entry)) continue;
            UInt64 child = entry & AddressMask4KiB;
            if (child == 0UL) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
            if (!ProtectTableHierarchy(child, level - 1)) return false;
        }
        return true;
    }

    private static Boolean WasProtected(UInt64 physicalAddress)
    {
        fixed (UInt64* protectedTables = _state.ProtectedTables)
        {
            for (Int32 index = 0; index < _protectedTableCount; index++)
                if (protectedTables[index] == physicalAddress) return true;
        }
        return false;
    }
}
