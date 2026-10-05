using Inu.Core;

namespace Inu.Memory.AddressSpace;

/// <summary>Identifies the purpose of a reserved virtual-address region.</summary>
public enum KernelAddressSpaceRegionKind
{
    /// <summary>User-mode virtual address space.</summary>
    User = 0,
    /// <summary>Kernel image and static kernel mappings.</summary>
    KernelImage = 1,
    /// <summary>Future kernel heap reservation.</summary>
    KernelHeap = 2,
    /// <summary>Kernel stacks and their guard-page neighbourhood.</summary>
    KernelStacks = 3,
    /// <summary>Direct physical-memory mapping window.</summary>
    DirectPhysicalMap = 4,
    /// <summary>Memory-mapped device I/O window.</summary>
    Mmio = 5,
    /// <summary>Dedicated page-table access window.</summary>
    PageTableWindow = 6
}

/// <summary>Describes one half-open virtual-address region in a kernel layout.</summary>
/// <nova.when>Use when defining, validating, or querying a kernel virtual-address policy.</nova.when>
/// <nova.depends>Canonical architecture address rules and page alignment.</nova.depends>
public readonly struct KernelAddressSpaceRegion
{
    private KernelAddressSpaceRegion(KernelAddressSpaceRegionKind kind, ulong baseAddress, ulong length)
    {
        Kind = kind; BaseAddress = baseAddress; Length = length;
    }
    /// <summary>Gets the role assigned to this region.</summary>
    public KernelAddressSpaceRegionKind Kind { get; }
    /// <summary>Gets the first virtual byte in the region.</summary>
    public ulong BaseAddress { get; }
    /// <summary>Gets the region length in bytes.</summary>
    public ulong Length { get; }
    /// <summary>Gets the exclusive end address, or zero when arithmetic would overflow.</summary>
    public ulong EndExclusive => BaseAddress <= ulong.MaxValue - Length ? BaseAddress + Length : 0UL;

    /// <summary>Creates a non-empty, page-aligned region without arithmetic overflow.</summary>
    /// <nova.when>Use before placing a region into a kernel address-space layout.</nova.when>
    /// <nova.depends>4 KiB base and length alignment.</nova.depends>
    /// <returns><see langword="true"/> when the region is valid.</returns>
    /// <example><code>bool ok = KernelAddressSpaceRegion.TryCreate(KernelAddressSpaceRegionKind.KernelHeap, 0xFFFF810000000000UL, 0x10000000000UL, out var region);</code></example>
    public static bool TryCreate(KernelAddressSpaceRegionKind kind, ulong baseAddress, ulong length, out KernelAddressSpaceRegion region)
    {
        region = default;
        if (length == 0 || (baseAddress & 0xFFFUL) != 0 || (length & 0xFFFUL) != 0) return false;
        if (baseAddress > ulong.MaxValue - length) return false;
        region = new KernelAddressSpaceRegion(kind, baseAddress, length);
        return true;
    }

    /// <summary>Determines whether one virtual byte lies inside this half-open region.</summary>
    /// <nova.when>Use when routing an address to the region responsible for it.</nova.when>
    /// <nova.depends>A validated region.</nova.depends>
    /// <returns><see langword="true"/> when the address is greater than or equal to the base and below the exclusive end.</returns>
    /// <example><code>bool inside = region.Contains(region.BaseAddress);</code></example>
    public bool Contains(ulong address) => address >= BaseAddress && address < EndExclusive;

    /// <summary>Determines whether two half-open regions overlap.</summary>
    /// <nova.when>Use while validating a custom kernel layout.</nova.when>
    /// <nova.depends>Two validated regions.</nova.depends>
    /// <returns><see langword="true"/> when at least one byte belongs to both regions.</returns>
    /// <example><code>bool overlap = first.Overlaps(second);</code></example>
    public bool Overlaps(KernelAddressSpaceRegion other) => BaseAddress < other.EndExclusive && other.BaseAddress < EndExclusive;
}

/// <summary>Defines the complete virtual-address policy consumed by later kernel allocators and subsystems.</summary>
/// <nova.when>Use to select the permanent regions for a Inu kernel or a custom OS built with the SDK.</nova.when>
/// <nova.depends>Seven validated, non-overlapping address-space regions.</nova.depends>
public readonly struct KernelAddressSpaceLayout
{
    private KernelAddressSpaceLayout(KernelAddressSpaceRegion user, KernelAddressSpaceRegion kernelImage, KernelAddressSpaceRegion heap, KernelAddressSpaceRegion stacks, KernelAddressSpaceRegion directMap, KernelAddressSpaceRegion mmio, KernelAddressSpaceRegion pageTables)
    { User = user; KernelImage = kernelImage; KernelHeap = heap; KernelStacks = stacks; DirectPhysicalMap = directMap; Mmio = mmio; PageTableWindow = pageTables; }
    /// <summary>Gets the user-space region.</summary>
    public KernelAddressSpaceRegion User { get; }
    /// <summary>Gets the kernel-image region.</summary>
    public KernelAddressSpaceRegion KernelImage { get; }
    /// <summary>Gets the future kernel-heap region.</summary>
    public KernelAddressSpaceRegion KernelHeap { get; }
    /// <summary>Gets the kernel-stack region.</summary>
    public KernelAddressSpaceRegion KernelStacks { get; }
    /// <summary>Gets the direct physical-map region.</summary>
    public KernelAddressSpaceRegion DirectPhysicalMap { get; }
    /// <summary>Gets the MMIO region.</summary>
    public KernelAddressSpaceRegion Mmio { get; }
    /// <summary>Gets the page-table access region.</summary>
    public KernelAddressSpaceRegion PageTableWindow { get; }

    /// <summary>Creates a complete layout after checking region roles and pairwise separation.</summary>
    /// <nova.when>Use for custom layouts before handing them to an architecture-specific validator.</nova.when>
    /// <nova.depends>Correct region kinds and non-overlap.</nova.depends>
    /// <returns><see langword="true"/> when every required region is present and disjoint.</returns>
    /// <example><code>bool ok = KernelAddressSpaceLayout.TryCreate(user, image, heap, stacks, directMap, mmio, pageTables, out var layout);</code></example>
    public static bool TryCreate(KernelAddressSpaceRegion user, KernelAddressSpaceRegion kernelImage, KernelAddressSpaceRegion heap, KernelAddressSpaceRegion stacks, KernelAddressSpaceRegion directMap, KernelAddressSpaceRegion mmio, KernelAddressSpaceRegion pageTables, out KernelAddressSpaceLayout layout)
    {
        layout = default;
        KernelAddressSpaceRegion[] r = [user, kernelImage, heap, stacks, directMap, mmio, pageTables];
        KernelAddressSpaceRegionKind[] k = [KernelAddressSpaceRegionKind.User, KernelAddressSpaceRegionKind.KernelImage, KernelAddressSpaceRegionKind.KernelHeap, KernelAddressSpaceRegionKind.KernelStacks, KernelAddressSpaceRegionKind.DirectPhysicalMap, KernelAddressSpaceRegionKind.Mmio, KernelAddressSpaceRegionKind.PageTableWindow];
        for (int i=0;i<r.Length;i++) { if (r[i].Kind != k[i] || r[i].Length == 0) return false; for(int j=i+1;j<r.Length;j++) if(r[i].Overlaps(r[j])) return false; }
        layout = new KernelAddressSpaceLayout(user, kernelImage, heap, stacks, directMap, mmio, pageTables); return true;
    }

    /// <summary>Gets the region assigned to one well-known purpose.</summary>
    /// <nova.when>Use when a subsystem needs its designated virtual reservation.</nova.when>
    /// <nova.depends>A validated layout.</nova.depends>
    /// <returns>The matching region.</returns>
    /// <example><code>var heap = layout.GetRegion(KernelAddressSpaceRegionKind.KernelHeap);</code></example>
    public KernelAddressSpaceRegion GetRegion(KernelAddressSpaceRegionKind kind) => kind switch { KernelAddressSpaceRegionKind.User => User, KernelAddressSpaceRegionKind.KernelImage => KernelImage, KernelAddressSpaceRegionKind.KernelHeap => KernelHeap, KernelAddressSpaceRegionKind.KernelStacks => KernelStacks, KernelAddressSpaceRegionKind.DirectPhysicalMap => DirectPhysicalMap, KernelAddressSpaceRegionKind.Mmio => Mmio, _ => PageTableWindow };
}

/// <summary>Allows an SDK consumer to supply its own address-space methodology.</summary>
/// <nova.when>Implement when the standard Inu x64 layout is not suitable.</nova.when>
/// <nova.depends>A valid <see cref="KernelAddressSpaceLayout"/>.</nova.depends>
public interface IKernelAddressSpacePolicy
{
    /// <summary>Attempts to produce the complete layout selected by the policy.</summary>
    /// <nova.when>Call during kernel address-space initialization.</nova.when>
    /// <nova.depends>Implementation-specific policy data.</nova.depends>
    /// <returns><see langword="true"/> when a valid layout was produced.</returns>
    /// <example><code>bool ok = policy.TryGetLayout(out KernelAddressSpaceLayout layout);</code></example>
    bool TryGetLayout(out KernelAddressSpaceLayout layout);
}
