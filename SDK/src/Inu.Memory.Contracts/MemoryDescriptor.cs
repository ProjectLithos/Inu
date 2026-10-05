using Inu.Primitives;

namespace Inu.Memory;

/// <summary>Describes one validated, page-aligned physical-memory range.</summary>
/// <nova.when>Use as the platform-independent unit of boot memory-map diagnostics and allocation policy.</nova.when>
/// <nova.depends>Inu.Primitives.PhysicalAddress</nova.depends>
public readonly struct MemoryDescriptor
{
    private MemoryDescriptor(
        PhysicalAddress physicalStart,
        ulong pageCount,
        ulong length,
        MemoryType memoryType,
        MemoryCacheAttributes cacheAttributes,
        MemoryRuntimeStatus runtimeStatus,
        MemoryAvailability availability,
        bool hasNumaNode,
        uint numaNode)
    {
        PhysicalStart = physicalStart;
        PageCount = pageCount;
        Length = length;
        MemoryType = memoryType;
        CacheAttributes = cacheAttributes;
        RuntimeStatus = runtimeStatus;
        Availability = availability;
        HasNumaNode = hasNumaNode;
        NumaNode = numaNode;
    }

    /// <summary>Gets the inclusive physical start address.</summary>
    /// <nova.when>Use to locate the first page represented by this descriptor.</nova.when>
    public PhysicalAddress PhysicalStart { get; }
    /// <summary>Gets the number of 4 KiB pages.</summary>
    /// <nova.when>Use for page-granular reservation and allocation calculations.</nova.when>
    public ulong PageCount { get; }
    /// <summary>Gets the range length in bytes.</summary>
    /// <nova.when>Use for byte totals and diagnostic output.</nova.when>
    public ulong Length { get; }
    /// <summary>Gets the normalised ownership type.</summary>
    /// <nova.when>Use to select ownership-specific allocation policy.</nova.when>
    public MemoryType MemoryType { get; }
    /// <summary>Gets portable cache and protection attributes.</summary>
    /// <nova.when>Use when constructing safe page-table mappings.</nova.when>
    public MemoryCacheAttributes CacheAttributes { get; }
    /// <summary>Gets firmware runtime ownership.</summary>
    /// <nova.when>Use to retain ranges required after ExitBootServices.</nova.when>
    public MemoryRuntimeStatus RuntimeStatus { get; }
    /// <summary>Gets the lifecycle point at which the range is allocatable.</summary>
    /// <nova.when>Use when filtering allocator candidates by boot stage.</nova.when>
    public MemoryAvailability Availability { get; }
    /// <summary>Gets whether a NUMA node has been assigned.</summary>
    /// <nova.when>Check before reading NumaNode.</nova.when>
    public bool HasNumaNode { get; }
    /// <summary>Gets the NUMA node, reserved for later topology discovery.</summary>
    /// <nova.when>Use after HasNumaNode reports that topology data is available.</nova.when>
    public uint NumaNode { get; }
    /// <summary>Gets the exclusive physical end address.</summary>
    /// <nova.when>Use for overlap, slicing, and adjacency checks.</nova.when>
    public ulong EndExclusive => PhysicalStart.Value + Length;

    /// <summary>Creates a validated descriptor without permitting arithmetic overflow.</summary>
    /// <nova.when>Use for firmware translation, explicit reservations, and split output ranges.</nova.when>
    /// <nova.depends>4 KiB page alignment</nova.depends>
    /// <returns><see langword="true"/> when the range is non-empty, aligned, and representable.</returns>
    /// <example><code>bool valid = MemoryDescriptor.TryCreate(new PhysicalAddress(0x100000), 256, MemoryType.UsableConventional, MemoryCacheAttributes.WriteBack, MemoryRuntimeStatus.NotRuntime, MemoryAvailability.AvailableAfterExitBootServices, false, 0, out MemoryDescriptor descriptor);</code></example>
    public static bool TryCreate(
        PhysicalAddress physicalStart,
        ulong pageCount,
        MemoryType memoryType,
        MemoryCacheAttributes cacheAttributes,
        MemoryRuntimeStatus runtimeStatus,
        MemoryAvailability availability,
        bool hasNumaNode,
        uint numaNode,
        out MemoryDescriptor descriptor)
    {
        descriptor = default;
        if (pageCount == 0 || (physicalStart.Value & 0xFFFUL) != 0) return false;
        if (pageCount > ulong.MaxValue / 4096UL) return false;
        ulong length = pageCount * 4096UL;
        if (physicalStart.Value > ulong.MaxValue - length) return false;
        descriptor = new MemoryDescriptor(physicalStart, pageCount, length, memoryType, cacheAttributes, runtimeStatus, availability, hasNumaNode, numaNode);
        return true;
    }

    /// <summary>Creates a page-aligned split of an existing descriptor.</summary>
    /// <nova.when>Use within a normaliser after selecting interval boundaries.</nova.when>
    /// <nova.depends>TryCreate</nova.depends>
    /// <returns><see langword="true"/> when the requested subrange lies inside this descriptor.</returns>
    /// <example><code>bool split = descriptor.TrySlice(0x100000, 0x110000, out MemoryDescriptor part);</code></example>
    public bool TrySlice(ulong start, ulong endExclusive, out MemoryDescriptor descriptor)
    {
        descriptor = default;
        if (start < PhysicalStart.Value || endExclusive > EndExclusive || start >= endExclusive) return false;
        if ((start & 0xFFFUL) != 0 || (endExclusive & 0xFFFUL) != 0) return false;
        return TryCreate(new PhysicalAddress(start), (endExclusive - start) / 4096UL, MemoryType, CacheAttributes, RuntimeStatus, Availability, HasNumaNode, NumaNode, out descriptor);
    }

    /// <summary>Determines whether two adjacent ranges have identical metadata.</summary>
    /// <nova.when>Use before merging compatible adjacent output ranges.</nova.when>
    /// <nova.depends>Validated descriptors</nova.depends>
    /// <returns><see langword="true"/> when the descriptors can be coalesced without losing information.</returns>
    /// <example><code>bool compatible = first.IsMergeCompatible(second);</code></example>
    public bool IsMergeCompatible(MemoryDescriptor other)
    {
        return EndExclusive == other.PhysicalStart.Value &&
            MemoryType == other.MemoryType && CacheAttributes == other.CacheAttributes &&
            RuntimeStatus == other.RuntimeStatus && Availability == other.Availability &&
            HasNumaNode == other.HasNumaNode && (!HasNumaNode || NumaNode == other.NumaNode);
    }
}

/// <summary>Defines a Inu-owned range that must override firmware availability.</summary>
/// <nova.when>Use to reserve the kernel image, boot structures, framebuffer, MMIO, page tables, or early allocations.</nova.when>
/// <nova.depends>MemoryDescriptor.TryCreate</nova.depends>
public readonly struct MemoryReservation
{
    private MemoryReservation(MemoryDescriptor descriptor) => Descriptor = descriptor;

    /// <summary>Gets the validated reservation descriptor.</summary>
    /// <nova.when>Use when overlaying this reservation during normalisation.</nova.when>
    public MemoryDescriptor Descriptor { get; }

    /// <summary>Creates a validated reservation with permanently unavailable ownership.</summary>
    /// <nova.when>Use before normalisation to overlay Inu-owned physical ranges.</nova.when>
    /// <nova.depends>4 KiB alignment</nova.depends>
    /// <returns><see langword="true"/> when the reservation type and range are valid.</returns>
    /// <example><code>bool reserved = MemoryReservation.TryCreate(new PhysicalAddress(0x200000), 32, MemoryType.PageTables, MemoryCacheAttributes.WriteBack, out MemoryReservation reservation);</code></example>
    public static bool TryCreate(PhysicalAddress start, ulong pageCount, MemoryType type, MemoryCacheAttributes attributes, out MemoryReservation reservation)
    {
        reservation = default;
        if (type is not (MemoryType.LoaderKernelImage or MemoryType.BootStructures or MemoryType.Framebuffer or MemoryType.MemoryMappedIo or MemoryType.PageTables or MemoryType.EarlyAllocatorAllocations or MemoryType.FirmwareReserved)) return false;
        MemoryAvailability availability = type == MemoryType.MemoryMappedIo || type == MemoryType.Framebuffer || type == MemoryType.FirmwareReserved
            ? MemoryAvailability.PermanentlyReserved
            : MemoryAvailability.Unavailable;
        if (!MemoryDescriptor.TryCreate(start, pageCount, type, attributes, MemoryRuntimeStatus.NotRuntime, availability, false, 0, out MemoryDescriptor descriptor)) return false;
        reservation = new MemoryReservation(descriptor);
        return true;
    }
}
