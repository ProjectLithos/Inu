using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    public static Boolean TryMap(UInt64 virtualAddress, UInt64 physicalAddress, KernelVirtualPageSize pageSize, KernelVirtualMemoryProtection protection)
    {
        if (!_initialized) return SetFailure(KernelVirtualMemoryStatus.NotInitialized);
        UInt64 size = (UInt64)pageSize;
        if (!IsSupportedPageSize(size)) return SetFailure(KernelVirtualMemoryStatus.UnsupportedPageSize);
        if (pageSize == KernelVirtualPageSize.Page1GiB && !_page1GiBSupported) return SetFailure(KernelVirtualMemoryStatus.UnsupportedPageSize);
        if (!HasProtection(protection, KernelVirtualMemoryProtection.Execute) && !_executeDisableEnabled) return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
        if (!IsCanonical(virtualAddress)) return SetFailure(KernelVirtualMemoryStatus.NonCanonicalAddress);
        if ((virtualAddress & (size - 1UL)) != 0UL || (physicalAddress & (size - 1UL)) != 0UL)
            return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        if ((physicalAddress & ~LeafAddressMask(pageSize)) != 0UL) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        if (!HasProtection(protection, KernelVirtualMemoryProtection.Read)) return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);

        if (!IsTableWritableBeforeDirectMap(_rootPhysicalAddress)) return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        UInt64* pml4 = TablePointer(_rootPhysicalAddress);
        Int32 pml4Index = (Int32)((virtualAddress >> 39) & 0x1FFUL);
        Int32 pdptIndex = (Int32)((virtualAddress >> 30) & 0x1FFUL);
        Int32 pdIndex = (Int32)((virtualAddress >> 21) & 0x1FFUL);
        Int32 ptIndex = (Int32)((virtualAddress >> 12) & 0x1FFUL);

        if (!TryGetOrCreateChild(pml4, pml4Index, protection, out UInt64* pdpt)) return false;
        if (pageSize == KernelVirtualPageSize.Page1GiB)
            return TryInstallLeaf(pdpt, pdptIndex, virtualAddress, physicalAddress, pageSize, protection);
        if (IsLarge(pdpt[pdptIndex])) return SetFailure(KernelVirtualMemoryStatus.AlreadyMapped);
        if (!TryGetOrCreateChild(pdpt, pdptIndex, protection, out UInt64* pd)) return false;
        if (pageSize == KernelVirtualPageSize.Page2MiB)
            return TryInstallLeaf(pd, pdIndex, virtualAddress, physicalAddress, pageSize, protection);
        if (IsLarge(pd[pdIndex])) return SetFailure(KernelVirtualMemoryStatus.AlreadyMapped);
        if (!TryGetOrCreateChild(pd, pdIndex, protection, out UInt64* pt)) return false;
        return TryInstallLeaf(pt, ptIndex, virtualAddress, physicalAddress, pageSize, protection);
    }

    /// <summary>Removes the present leaf mapping covering one canonical virtual address.</summary>
    /// <returns><see langword="true"/> when a leaf was cleared and its translation invalidated.</returns>
    public static Boolean TryUnmap(UInt64 virtualAddress)
    {
        if (!_initialized) return SetFailure(KernelVirtualMemoryStatus.NotInitialized);
        if (!TryFindLeaf(virtualAddress, out UInt64* table, out Int32 index, out KernelVirtualPageSize pageSize)) return false;
        table[index] = 0UL;
        DecrementMapped(pageSize);
        if (!Native.InvalidatePage(virtualAddress)) return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    /// <summary>Replaces access and cache protection on the present leaf mapping covering one virtual address.</summary>
    /// <returns><see langword="true"/> when the leaf was rewritten without changing its physical target.</returns>
    public static Boolean TryProtect(UInt64 virtualAddress, KernelVirtualMemoryProtection protection)
    {
        if (!_initialized) return SetFailure(KernelVirtualMemoryStatus.NotInitialized);
        if (!HasProtection(protection, KernelVirtualMemoryProtection.Read)) return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
        if (!HasProtection(protection, KernelVirtualMemoryProtection.Execute) && !_executeDisableEnabled) return SetFailure(KernelVirtualMemoryStatus.UnsupportedProtection);
        if (!TryFindLeaf(virtualAddress, out UInt64* table, out Int32 index, out KernelVirtualPageSize pageSize)) return false;
        UInt64 current = table[index];
        UInt64 physicalAddress = current & LeafAddressMask(pageSize);
        if (!TryEncodeLeaf(physicalAddress, pageSize, protection, out UInt64 replacement))
            return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        table[index] = replacement;
        if (!Native.InvalidatePage(virtualAddress)) return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    /// <summary>Resolves one canonical virtual byte address through the active x64 page tables.</summary>
    /// <returns><see langword="true"/> when a present leaf covers the queried address.</returns>
    public static Boolean TryTranslate(UInt64 virtualAddress, out KernelVirtualTranslation translation)
    {
        translation = default;
        if (KernelFaultInjection.ShouldInject(KernelFaultKind.PageFault,"virtual-memory",out _)) return SetFailure(KernelVirtualMemoryStatus.NotMapped);
        if (!_initialized) return SetFailure(KernelVirtualMemoryStatus.NotInitialized);
        if (!TryFindLeaf(virtualAddress, out UInt64* table, out Int32 index, out KernelVirtualPageSize pageSize)) return false;
        UInt64 entry = table[index];
        UInt64 size = (UInt64)pageSize;
        UInt64 physicalBase = entry & LeafAddressMask(pageSize);
        UInt64 physicalAddress = physicalBase + (virtualAddress & (size - 1UL));
        translation = new KernelVirtualTranslation(virtualAddress, physicalAddress, pageSize, DecodeProtection(entry));
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    /// <summary>Gets current inherited page-table protection and manager-created mapping accounting.</summary>
    public static KernelVirtualMemoryStatistics GetStatistics() => new((UInt64)_protectedTableCount, _createdPageTables, _mapped4KiB, _mapped2MiB, _mapped1GiB);
}
