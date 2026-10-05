using System;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.AddressSpace;

/// <summary>Reports the state of the freestanding kernel address-space policy.</summary>
public enum KernelAddressSpaceStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The virtual-memory manager must be initialized first.</summary>
    VirtualMemoryNotInitialized = 1,
    /// <summary>The address-space policy was already initialized.</summary>
    AlreadyInitialized = 2,
    /// <summary>The compiled standard layout failed an internal invariant.</summary>
    InvalidLayout = 3,
    /// <summary>The calculated PMM-backed direct physical map could not be established.</summary>
    DirectMapInitializationFailed = 4
}

/// <summary>Exposes the standard Inu x64 kernel virtual-address reservations to freestanding kernels.</summary>
public static class KernelAddressSpace
{
    /// <summary>First user virtual byte; low 64 KiB remain a guard.</summary>
    public const UInt64 UserBase = 0x0000000000010000UL;
    /// <summary>Exclusive end of the user half.</summary>
    public const UInt64 UserEndExclusive = 0x0000800000000000UL;
    /// <summary>Higher-half kernel image base.</summary>
    public const UInt64 KernelImageBase = 0xFFFF800000000000UL;
    /// <summary>Kernel image reservation length.</summary>
    public const UInt64 KernelImageLength = 0x0000000100000000UL;
    /// <summary>Future kernel heap base.</summary>
    public const UInt64 KernelHeapBase = 0xFFFF810000000000UL;
    /// <summary>Future kernel heap reservation length.</summary>
    public const UInt64 KernelHeapLength = 0x0000010000000000UL;
    /// <summary>Kernel stack arena base.</summary>
    public const UInt64 KernelStacksBase = 0xFFFF820000000000UL;
    /// <summary>Kernel stack arena length.</summary>
    public const UInt64 KernelStacksLength = 0x0000010000000000UL;
    /// <summary>Direct physical map base.</summary>
    public const UInt64 DirectMapBase = 0xFFFF900000000000UL;
    /// <summary>Direct physical map capacity.</summary>
    public const UInt64 DirectMapLength = 0x0000400000000000UL;
    /// <summary>MMIO window base.</summary>
    public const UInt64 MmioBase = 0xFFFFD00000000000UL;
    /// <summary>MMIO window length.</summary>
    public const UInt64 MmioLength = 0x0000100000000000UL;
    /// <summary>Page-table access window base.</summary>
    public const UInt64 PageTableWindowBase = 0xFFFFFF0000000000UL;
    /// <summary>Page-table access window length.</summary>
    public const UInt64 PageTableWindowLength = 0x0000008000000000UL;
    private static Boolean _initialized;
    private static KernelAddressSpaceStatus _status = KernelAddressSpaceStatus.VirtualMemoryNotInitialized;

    /// <summary>Gets whether the address-space policy has been initialized.</summary>
    public static Boolean IsInitialized() => _initialized;
    /// <summary>Gets the last initialization status.</summary>
    public static KernelAddressSpaceStatus GetLastStatus() => _status;
    /// <summary>Gets a freestanding-safe symbolic name for the last initialization status.</summary>
    /// <returns>A stable status name that can be written directly without enum formatting or <c>string.Format</c>.</returns>
    public static String GetLastStatusName()
    {
        if (_status == KernelAddressSpaceStatus.Success) return "Success";
        if (_status == KernelAddressSpaceStatus.VirtualMemoryNotInitialized) return "VirtualMemoryNotInitialized";
        if (_status == KernelAddressSpaceStatus.AlreadyInitialized) return "AlreadyInitialized";
        if (_status == KernelAddressSpaceStatus.InvalidLayout) return "InvalidLayout";
        if (_status == KernelAddressSpaceStatus.DirectMapInitializationFailed) return "DirectMapInitializationFailed";
        return "Unknown";
    }

    /// <summary>Validates and activates the standard address-space policy for later kernel subsystems.</summary>
    /// <returns><see langword="true"/> when the VMM is ready and every standard region satisfies the compiled invariants.</returns>
    public static Boolean Initialize()
    {
        if (_initialized) { _status = KernelAddressSpaceStatus.AlreadyInitialized; return false; }
        if (!KernelVirtualMemory.IsInitialized()) { _status = KernelAddressSpaceStatus.VirtualMemoryNotInitialized; return false; }
        if (!ValidateRange(UserBase, UserEndExclusive - UserBase, false) ||
            !ValidateRange(KernelImageBase, KernelImageLength, true) || !ValidateRange(KernelHeapBase, KernelHeapLength, true) ||
            !ValidateRange(KernelStacksBase, KernelStacksLength, true) || !ValidateRange(DirectMapBase, DirectMapLength, true) ||
            !ValidateRange(MmioBase, MmioLength, true) || !ValidateRange(PageTableWindowBase, PageTableWindowLength, true) ||
            Overlaps(KernelImageBase, KernelImageLength, KernelHeapBase, KernelHeapLength) ||
            Overlaps(KernelHeapBase, KernelHeapLength, KernelStacksBase, KernelStacksLength) ||
            Overlaps(KernelStacksBase, KernelStacksLength, DirectMapBase, DirectMapLength) ||
            Overlaps(DirectMapBase, DirectMapLength, MmioBase, MmioLength) ||
            Overlaps(MmioBase, MmioLength, PageTableWindowBase, PageTableWindowLength))
        { _status = KernelAddressSpaceStatus.InvalidLayout; return false; }
        if (!KernelVirtualMemory.InitializeDirectMap(DirectMapBase, DirectMapLength))
        { _status = KernelAddressSpaceStatus.DirectMapInitializationFailed; return false; }
        _initialized = true; _status = KernelAddressSpaceStatus.Success; return true;
    }

    /// <summary>Attempts to convert a physical byte address into an actually-present standard direct-map mapping.</summary>
    /// <returns><see langword="true"/> only when the physical byte belongs to a mapped PMM/bootstrap extent, not merely when it fits the 64 TiB reservation.</returns>
    public static Boolean TryPhysicalToDirectMap(UInt64 physicalAddress, out UInt64 virtualAddress)
    { virtualAddress = 0UL; return _initialized && KernelVirtualMemory.TryGetDirectMapAddress(physicalAddress, out virtualAddress); }

    /// <summary>Attempts to convert one actually-present direct-map virtual address to its physical byte address.</summary>
    /// <returns><see langword="true"/> only when the address lies in a mapped direct-map extent rather than an unmapped hole in the reservation.</returns>
    public static Boolean TryDirectMapToPhysical(UInt64 virtualAddress, out UInt64 physicalAddress)
    {
        physicalAddress = 0UL;
        if (!_initialized || virtualAddress < DirectMapBase || virtualAddress >= DirectMapBase + DirectMapLength) return false;
        UInt64 candidate = virtualAddress - DirectMapBase;
        if (!KernelVirtualMemory.TryGetDirectMapAddress(candidate, out UInt64 resolved) || resolved != virtualAddress) return false;
        physicalAddress = candidate;
        return true;
    }

    private static Boolean ValidateRange(UInt64 address, UInt64 length, Boolean high)
    {
        if (length == 0 || (address & 0xFFFUL) != 0 || (length & 0xFFFUL) != 0 || address > 0xFFFFFFFFFFFFFFFFUL - length) return false;
        UInt64 last = address + length - 1UL;
        if (!IsCanonical(address) || !IsCanonical(last)) return false;
        return high ? address >= 0xFFFF800000000000UL : last < 0x0000800000000000UL;
    }
    private static Boolean IsCanonical(UInt64 address) { UInt64 upper = address >> 48; return ((address >> 47) & 1UL) != 0 ? upper == 0xFFFFUL : upper == 0UL; }
    private static Boolean Overlaps(UInt64 a, UInt64 al, UInt64 b, UInt64 bl) => a < b + bl && b < a + al;
}
