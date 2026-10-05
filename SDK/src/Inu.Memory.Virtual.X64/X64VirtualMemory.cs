using Inu.Core;
using Inu.Primitives;

namespace Inu.Memory.Virtual.X64;

/// <summary>Provides checked x64 canonical-address and page-table index calculations.</summary>
/// <nova.when>Use when building, walking, or validating x64 four-level page tables.</nova.when>
/// <nova.depends>x64 48-bit canonical virtual-address form</nova.depends>
[SupportedArchitecture(SupportedArchitecture.X64)]
[BootStage(BootStage.ManagedBootstrap)]
public static class X64VirtualAddress
{
    /// <summary>Determines whether a 64-bit address is canonical for four-level x64 paging.</summary>
    /// <nova.when>Use before dereferencing or installing any x64 virtual mapping.</nova.when>
    /// <nova.depends>Bits 63:48 replicate bit 47</nova.depends>
    /// <returns><see langword="true"/> when the address is in the low or high canonical half.</returns>
    /// <example><code>bool canonical = X64VirtualAddress.IsCanonical(0xFFFF800000000000);</code></example>
    public static bool IsCanonical(ulong address)
    {
        ulong upper = address >> 48;
        bool high = ((address >> 47) & 1UL) != 0;
        return high ? upper == 0xFFFFUL : upper == 0UL;
    }

    /// <summary>Attempts to obtain the four 9-bit page-table indices for one canonical address.</summary>
    /// <nova.when>Use when walking PML4, PDPT, page-directory, and page-table levels.</nova.when>
    /// <nova.depends>Successful canonical-address validation</nova.depends>
    /// <returns><see langword="true"/> when the address is canonical and all indices were produced.</returns>
    /// <example><code>bool indexed = X64VirtualAddress.TryGetIndices(address, out ushort pml4, out ushort pdpt, out ushort pd, out ushort pt);</code></example>
    public static bool TryGetIndices(ulong address, out ushort pml4, out ushort pdpt, out ushort pd, out ushort pt)
    {
        pml4 = 0;
        pdpt = 0;
        pd = 0;
        pt = 0;
        if (!IsCanonical(address)) return false;
        pml4 = (ushort)((address >> 39) & 0x1FFUL);
        pdpt = (ushort)((address >> 30) & 0x1FFUL);
        pd = (ushort)((address >> 21) & 0x1FFUL);
        pt = (ushort)((address >> 12) & 0x1FFUL);
        return true;
    }
}

/// <summary>Encodes and decodes x64 long-mode page-table leaf entries without exposing raw bit arithmetic to SDK consumers.</summary>
/// <nova.when>Use inside x64 virtual-memory implementations and diagnostics.</nova.when>
/// <nova.depends>VirtualMemoryProtection and x64 long-mode page-table encoding</nova.depends>
[SupportedArchitecture(SupportedArchitecture.X64)]
[BootStage(BootStage.ManagedBootstrap)]
public static class X64PageTableCodec
{
    private const ulong Present = 1UL << 0;
    private const ulong Writable = 1UL << 1;
    private const ulong User = 1UL << 2;
    private const ulong WriteThrough = 1UL << 3;
    private const ulong CacheDisable = 1UL << 4;
    private const ulong LargePage = 1UL << 7;
    private const ulong Global = 1UL << 8;
    private const ulong NoExecute = 1UL << 63;
    private const ulong AddressMask4KiB = 0x000FFFFFFFFFF000UL;
    private const ulong AddressMask2MiB = 0x000FFFFFFFE00000UL;
    private const ulong AddressMask1GiB = 0x000FFFFFC0000000UL;

    /// <summary>Attempts to encode one x64 present leaf entry for the selected page size.</summary>
    /// <nova.when>Use after validating processor support for the selected page size and NX policy.</nova.when>
    /// <nova.depends>Aligned physical address and architecture-neutral protection</nova.depends>
    /// <returns><see langword="true"/> when the address and protection can be represented safely.</returns>
    /// <example><code>bool encoded = X64PageTableCodec.TryEncodeLeaf(new PhysicalAddress(0x200000), VirtualPageSize.Page2MiB, VirtualMemoryProtection.Read | VirtualMemoryProtection.Write, out ulong entry);</code></example>
    public static bool TryEncodeLeaf(PhysicalAddress physicalAddress, VirtualPageSize pageSize, VirtualMemoryProtection protection, out ulong entry)
    {
        entry = 0;
        ulong size = (ulong)pageSize;
        if (size != 4096UL && size != 2097152UL && size != 1073741824UL) return false;
        if ((physicalAddress.Value & (size - 1UL)) != 0) return false;
        if ((protection & VirtualMemoryProtection.Read) == 0) return false;

        ulong addressMask = pageSize switch
        {
            VirtualPageSize.Page4KiB => AddressMask4KiB,
            VirtualPageSize.Page2MiB => AddressMask2MiB,
            VirtualPageSize.Page1GiB => AddressMask1GiB,
            _ => 0UL
        };
        if ((physicalAddress.Value & ~addressMask) != 0) return false;

        ulong flags = Present;
        if ((protection & VirtualMemoryProtection.Write) != 0) flags |= Writable;
        if ((protection & VirtualMemoryProtection.User) != 0) flags |= User;
        if ((protection & VirtualMemoryProtection.Global) != 0) flags |= Global;
        if ((protection & VirtualMemoryProtection.Device) != 0) flags |= CacheDisable;
        if ((protection & VirtualMemoryProtection.WriteThrough) != 0) flags |= WriteThrough;
        if ((protection & VirtualMemoryProtection.Execute) == 0) flags |= NoExecute;
        if (pageSize != VirtualPageSize.Page4KiB) flags |= LargePage;
        entry = (physicalAddress.Value & addressMask) | flags;
        return true;
    }

    /// <summary>Attempts to decode a present x64 leaf entry and exact translated byte address.</summary>
    /// <nova.when>Use when translating or inspecting an already-walked leaf entry.</nova.when>
    /// <nova.depends>Correct leaf level supplied by the caller</nova.depends>
    /// <returns><see langword="true"/> when the entry is present and consistent with the supplied page size.</returns>
    /// <example><code>bool decoded = X64PageTableCodec.TryDecodeLeaf(entry, virtualAddress, VirtualPageSize.Page4KiB, out VirtualTranslation translation);</code></example>
    public static bool TryDecodeLeaf(ulong entry, ulong virtualAddress, VirtualPageSize pageSize, out VirtualTranslation translation)
    {
        translation = default;
        if ((entry & Present) == 0 || !X64VirtualAddress.IsCanonical(virtualAddress)) return false;
        if (pageSize == VirtualPageSize.Page4KiB && (entry & LargePage) != 0) return false;
        if (pageSize != VirtualPageSize.Page4KiB && (entry & LargePage) == 0) return false;

        ulong size = (ulong)pageSize;
        ulong mask = pageSize switch
        {
            VirtualPageSize.Page4KiB => AddressMask4KiB,
            VirtualPageSize.Page2MiB => AddressMask2MiB,
            VirtualPageSize.Page1GiB => AddressMask1GiB,
            _ => 0UL
        };
        if (mask == 0) return false;

        ulong physicalBase = entry & mask;
        ulong offset = virtualAddress & (size - 1UL);
        VirtualMemoryProtection protection = VirtualMemoryProtection.Read;
        if ((entry & Writable) != 0) protection |= VirtualMemoryProtection.Write;
        if ((entry & User) != 0) protection |= VirtualMemoryProtection.User;
        if ((entry & Global) != 0) protection |= VirtualMemoryProtection.Global;
        if ((entry & CacheDisable) != 0) protection |= VirtualMemoryProtection.Device;
        if ((entry & WriteThrough) != 0) protection |= VirtualMemoryProtection.WriteThrough;
        if ((entry & NoExecute) == 0) protection |= VirtualMemoryProtection.Execute;
        translation = new VirtualTranslation(virtualAddress, new PhysicalAddress(physicalBase + offset), pageSize, protection);
        return true;
    }

    /// <summary>Attempts to encode an x64 non-leaf page-table pointer.</summary>
    /// <nova.when>Use when allocating an intermediate PML4, PDPT, page-directory, or page-table child.</nova.when>
    /// <nova.depends>4 KiB-aligned physical page-table storage</nova.depends>
    /// <returns><see langword="true"/> when the physical address fits the x64 page-table address field.</returns>
    /// <example><code>bool encoded = X64PageTableCodec.TryEncodeTablePointer(new PhysicalAddress(0x3000), true, true, out ulong entry);</code></example>
    public static bool TryEncodeTablePointer(PhysicalAddress physicalAddress, bool writable, bool userAccessible, out ulong entry)
    {
        entry = 0;
        if ((physicalAddress.Value & 0xFFFUL) != 0 || (physicalAddress.Value & ~AddressMask4KiB) != 0) return false;
        ulong flags = Present;
        if (writable) flags |= Writable;
        if (userAccessible) flags |= User;
        entry = (physicalAddress.Value & AddressMask4KiB) | flags;
        return true;
    }

    /// <summary>Gets the 4 KiB-aligned physical page-table address stored in a present non-leaf entry.</summary>
    /// <nova.when>Use while walking intermediate x64 page-table levels.</nova.when>
    /// <nova.depends>Caller has verified the entry is a present non-leaf pointer</nova.depends>
    /// <returns>The physical child-table address, or zero when the entry is not present.</returns>
    /// <example><code>PhysicalAddress child = X64PageTableCodec.GetTableAddress(entry);</code></example>
    public static PhysicalAddress GetTableAddress(ulong entry) => new((entry & Present) != 0 ? entry & AddressMask4KiB : 0UL);

    /// <summary>Determines whether an x64 entry is present.</summary>
    /// <nova.when>Use before interpreting an entry as a table pointer or leaf.</nova.when>
    /// <nova.depends>x64 present bit</nova.depends>
    /// <returns><see langword="true"/> when the present bit is set.</returns>
    /// <example><code>bool present = X64PageTableCodec.IsPresent(entry);</code></example>
    public static bool IsPresent(ulong entry) => (entry & Present) != 0;

    /// <summary>Determines whether an x64 entry marks a large leaf at PDPT or page-directory level.</summary>
    /// <nova.when>Use while walking x64 page tables before descending to a child table.</nova.when>
    /// <nova.depends>x64 page-size bit</nova.depends>
    /// <returns><see langword="true"/> when the large-page bit is set.</returns>
    /// <example><code>bool large = X64PageTableCodec.IsLargePage(entry);</code></example>
    public static bool IsLargePage(ulong entry) => (entry & LargePage) != 0;
}
