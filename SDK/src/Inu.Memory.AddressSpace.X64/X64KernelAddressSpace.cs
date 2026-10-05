using Inu.Core;
using Inu.Memory.Virtual.X64;

namespace Inu.Memory.AddressSpace.X64;

/// <summary>Defines and validates the standard Inu four-level x64 kernel address-space policy.</summary>
/// <nova.when>Use as the default x64 layout or as a reference when creating a custom layout.</nova.when>
/// <nova.depends>Four-level x64 canonical addressing and 4 KiB pages.</nova.depends>
[SupportedArchitecture(SupportedArchitecture.X64)]
[BootStage(BootStage.ManagedBootstrap)]
public static class X64KernelAddressSpace
{
    /// <summary>Gets the first user virtual byte; the first 64 KiB are intentionally left unmapped as a null/low-address guard.</summary>
    public const ulong UserBase = 0x0000000000010000UL;
    /// <summary>Gets the exclusive end of the low canonical user half.</summary>
    public const ulong UserEndExclusive = 0x0000800000000000UL;
    /// <summary>Gets the target base of the higher-half kernel image window.</summary>
    public const ulong KernelImageBase = 0xFFFF800000000000UL;
    /// <summary>Gets the reserved kernel-image window length (4 GiB).</summary>
    public const ulong KernelImageLength = 0x0000000100000000UL;
    /// <summary>Gets the base of the future kernel heap reservation.</summary>
    public const ulong KernelHeapBase = 0xFFFF810000000000UL;
    /// <summary>Gets the kernel heap reservation length (1 TiB).</summary>
    public const ulong KernelHeapLength = 0x0000010000000000UL;
    /// <summary>Gets the base of the kernel stack/guard-page arena.</summary>
    public const ulong KernelStacksBase = 0xFFFF820000000000UL;
    /// <summary>Gets the kernel stack arena length (1 TiB).</summary>
    public const ulong KernelStacksLength = 0x0000010000000000UL;
    /// <summary>Gets the base of the direct physical-memory map.</summary>
    public const ulong DirectMapBase = 0xFFFF900000000000UL;
    /// <summary>Gets the direct-map capacity (64 TiB of physical address space).</summary>
    public const ulong DirectMapLength = 0x0000400000000000UL;
    /// <summary>Gets the base of the kernel MMIO window.</summary>
    public const ulong MmioBase = 0xFFFFD00000000000UL;
    /// <summary>Gets the MMIO window length (16 TiB).</summary>
    public const ulong MmioLength = 0x0000100000000000UL;
    /// <summary>Gets the base of the dedicated page-table access window.</summary>
    public const ulong PageTableWindowBase = 0xFFFFFF0000000000UL;
    /// <summary>Gets the page-table access window length (512 GiB).</summary>
    public const ulong PageTableWindowLength = 0x0000008000000000UL;

    /// <summary>Creates the standard Inu x64 kernel layout.</summary>
    /// <nova.when>Use during x64 kernel bootstrap before heap, stack, MMIO, or direct-map consumers are enabled.</nova.when>
    /// <nova.depends>Canonical four-level x64 address ranges.</nova.depends>
    /// <returns><see langword="true"/> when all standard regions were created and validated.</returns>
    /// <example><code>bool ok = X64KernelAddressSpace.TryCreateStandard(out KernelAddressSpaceLayout layout);</code></example>
    public static bool TryCreateStandard(out KernelAddressSpaceLayout layout)
    {
        layout = default;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.User, UserBase, UserEndExclusive - UserBase, out var user)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.KernelImage, KernelImageBase, KernelImageLength, out var image)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.KernelHeap, KernelHeapBase, KernelHeapLength, out var heap)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.KernelStacks, KernelStacksBase, KernelStacksLength, out var stacks)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.DirectPhysicalMap, DirectMapBase, DirectMapLength, out var direct)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.Mmio, MmioBase, MmioLength, out var mmio)) return false;
        if (!KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.PageTableWindow, PageTableWindowBase, PageTableWindowLength, out var tables)) return false;
        if (!KernelAddressSpaceLayout.TryCreate(user, image, heap, stacks, direct, mmio, tables, out layout)) return false;
        return Validate(layout);
    }

    /// <summary>Validates canonicality, high/low-half placement, separation, and page alignment for an x64 kernel layout.</summary>
    /// <nova.when>Use before accepting a custom x64 address-space policy.</nova.when>
    /// <nova.depends>A structurally valid <see cref="KernelAddressSpaceLayout"/>.</nova.depends>
    /// <returns><see langword="true"/> when every mapped endpoint is canonical and the user/kernel split is respected.</returns>
    /// <example><code>bool valid = X64KernelAddressSpace.Validate(layout);</code></example>
    public static bool Validate(KernelAddressSpaceLayout layout)
    {
        if (layout.User.BaseAddress < UserBase || layout.User.EndExclusive > UserEndExclusive) return false;
        foreach (var region in new[] { layout.KernelImage, layout.KernelHeap, layout.KernelStacks, layout.DirectPhysicalMap, layout.Mmio, layout.PageTableWindow })
        {
            if (region.BaseAddress < 0xFFFF800000000000UL || !X64VirtualAddress.IsCanonical(region.BaseAddress) || !X64VirtualAddress.IsCanonical(region.EndExclusive - 1UL)) return false;
        }
        return true;
    }

    /// <summary>Attempts to translate a direct-map virtual address back to its physical byte address.</summary>
    /// <nova.when>Use for diagnostics or physical-page ownership calculations within the direct-map window.</nova.when>
    /// <nova.depends>The standard direct-map base and capacity.</nova.depends>
    /// <returns><see langword="true"/> when the virtual address belongs to the direct-map window.</returns>
    /// <example><code>bool ok = X64KernelAddressSpace.TryDirectMapToPhysical(X64KernelAddressSpace.DirectMapBase + 0x2000, out ulong physical);</code></example>
    public static bool TryDirectMapToPhysical(ulong virtualAddress, out ulong physicalAddress)
    { physicalAddress = 0; if (virtualAddress < DirectMapBase || virtualAddress >= DirectMapBase + DirectMapLength) return false; physicalAddress = virtualAddress - DirectMapBase; return true; }

    /// <summary>Attempts to convert a physical byte address into the standard direct-map virtual address.</summary>
    /// <nova.when>Use when a kernel subsystem needs stable virtual access to a physical byte inside the direct-map capacity.</nova.when>
    /// <nova.depends>Physical address below <see cref="DirectMapLength"/>.</nova.depends>
    /// <returns><see langword="true"/> when the physical address fits the configured direct-map capacity.</returns>
    /// <example><code>bool ok = X64KernelAddressSpace.TryPhysicalToDirectMap(0x2000, out ulong virtualAddress);</code></example>
    public static bool TryPhysicalToDirectMap(ulong physicalAddress, out ulong virtualAddress)
    { virtualAddress = 0; if (physicalAddress >= DirectMapLength) return false; virtualAddress = DirectMapBase + physicalAddress; return true; }
}
