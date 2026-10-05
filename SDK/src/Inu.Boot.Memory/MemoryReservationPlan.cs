using Inu.Memory;
using Inu.Primitives;

namespace Inu.Boot.Memory;

/// <summary>Builds the explicit reservation overlay required before normalisation.</summary>
/// <nova.when>Use to reserve the kernel, Inu boot structures, framebuffer, MMIO, page tables, and early allocations.</nova.when>
/// <nova.depends>MemoryReservation</nova.depends>
public sealed class MemoryReservationPlan
{
    private readonly MemoryReservation[] _reservations;
    private int _count;
    private bool _hasKernelImage;
    private bool _hasBootStructures;
    private bool _hasFramebuffer;
    private bool _hasMemoryMappedIo;
    private bool _hasPageTables;
    private bool _hasEarlyAllocations;

    /// <summary>Creates a bounded reservation plan.</summary>
    /// <nova.when>Create before collecting every kernel and platform-owned range.</nova.when>
    /// <nova.depends>MemoryReservation fixed-capacity storage</nova.depends>
    /// <returns>A mutable builder that produces an immutable reservation copy.</returns>
    /// <example><code>MemoryReservationPlan plan = new(32);</code></example>
    /// <param name="capacity">Maximum reservation count.</param>
    public MemoryReservationPlan(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _reservations = new MemoryReservation[capacity];
    }

    /// <summary>Gets the current reservation count.</summary>
    /// <nova.when>Use to report how many explicit ranges will override firmware ownership.</nova.when>
    public int Count => _count;
    /// <summary>Gets the maximum reservation count.</summary>
    /// <nova.when>Use to ensure the plan has enough preallocated capacity.</nova.when>
    public int Capacity => _reservations.Length;
    /// <summary>Gets whether at least one kernel-image range is reserved.</summary>
    /// <nova.when>Use when validating the mandatory kernel reservation.</nova.when>
    public bool HasKernelImage => _hasKernelImage;
    /// <summary>Gets whether at least one Inu boot-structure range is reserved.</summary>
    /// <nova.when>Use when validating mandatory hand-off reservations.</nova.when>
    public bool HasBootStructures => _hasBootStructures;
    /// <summary>Gets whether at least one framebuffer range is reserved.</summary>
    /// <nova.when>Use when a framebuffer aperture is present.</nova.when>
    public bool HasFramebuffer => _hasFramebuffer;
    /// <summary>Gets whether at least one explicit MMIO range is reserved.</summary>
    /// <nova.when>Use when platform MMIO apertures are known.</nova.when>
    public bool HasMemoryMappedIo => _hasMemoryMappedIo;
    /// <summary>Gets whether at least one active page-table range is reserved.</summary>
    /// <nova.when>Use to verify active translation structures cannot be allocated.</nova.when>
    public bool HasPageTables => _hasPageTables;
    /// <summary>Gets whether at least one early allocation is reserved.</summary>
    /// <nova.when>Use to verify committed early allocations cannot be reused.</nova.when>
    public bool HasEarlyAllocations => _hasEarlyAllocations;

    /// <summary>Adds a kernel-image reservation.</summary>
    /// <nova.when>Call with the complete loaded kernel image range.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddKernelImage(new PhysicalAddress(0x100000), 256);</code></example>
    public bool TryAddKernelImage(PhysicalAddress start, ulong pages)
        => TryAdd(start, pages, MemoryType.LoaderKernelImage, MemoryCacheAttributes.WriteBack);

    /// <summary>Adds a Inu boot-structure reservation.</summary>
    /// <nova.when>Call for the boot context, copied maps, command lines, modules, and hand-off records.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddBootStructures(new PhysicalAddress(0x180000), 8);</code></example>
    public bool TryAddBootStructures(PhysicalAddress start, ulong pages)
        => TryAdd(start, pages, MemoryType.BootStructures, MemoryCacheAttributes.WriteBack);

    /// <summary>Adds a framebuffer reservation.</summary>
    /// <nova.when>Call with the page-rounded framebuffer aperture.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddFramebuffer(new PhysicalAddress(0xE0000000), 2048, MemoryCacheAttributes.WriteCombining);</code></example>
    public bool TryAddFramebuffer(PhysicalAddress start, ulong pages, MemoryCacheAttributes attributes)
        => TryAdd(start, pages, MemoryType.Framebuffer, attributes);

    /// <summary>Adds an MMIO reservation.</summary>
    /// <nova.when>Call for every platform MMIO aperture known before allocator startup.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddMemoryMappedIo(new PhysicalAddress(0xFEC00000), 1);</code></example>
    public bool TryAddMemoryMappedIo(PhysicalAddress start, ulong pages)
        => TryAdd(start, pages, MemoryType.MemoryMappedIo, MemoryCacheAttributes.Uncacheable);

    /// <summary>Adds an active page-table reservation.</summary>
    /// <nova.when>Call for every page-table page installed by native or managed bootstrap.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddPageTables(new PhysicalAddress(0x190000), 16);</code></example>
    public bool TryAddPageTables(PhysicalAddress start, ulong pages)
        => TryAdd(start, pages, MemoryType.PageTables, MemoryCacheAttributes.WriteBack);

    /// <summary>Adds memory consumed by the early allocator.</summary>
    /// <nova.when>Call whenever an early allocation is committed before the physical allocator takes ownership.</nova.when>
    /// <nova.depends>TryAdd</nova.depends>
    /// <returns><see langword="true"/> when the reservation was added.</returns>
    /// <example><code>bool added = plan.TryAddEarlyAllocation(new PhysicalAddress(0x1A0000), 4);</code></example>
    public bool TryAddEarlyAllocation(PhysicalAddress start, ulong pages)
        => TryAdd(start, pages, MemoryType.EarlyAllocatorAllocations, MemoryCacheAttributes.WriteBack);


    /// <summary>Validates the core and platform-dependent reservations required for normalisation.</summary>
    /// <nova.when>Call before passing this plan to a normaliser.</nova.when>
    /// <nova.depends>Kernel image and boot structures are always required; framebuffer and MMIO are conditional.</nova.depends>
    /// <returns><see langword="true"/> when every requested reservation category is present.</returns>
    /// <example><code>bool complete = plan.TryValidateRequiredReservations(framebufferPresent, mmioPresent, out MemoryType missingType);</code></example>
    public bool TryValidateRequiredReservations(bool framebufferRequired, bool memoryMappedIoRequired, out MemoryType missingType)
    {
        if (!_hasKernelImage)
        {
            missingType = MemoryType.LoaderKernelImage;
            return false;
        }
        if (!_hasBootStructures)
        {
            missingType = MemoryType.BootStructures;
            return false;
        }
        if (framebufferRequired && !_hasFramebuffer)
        {
            missingType = MemoryType.Framebuffer;
            return false;
        }
        if (memoryMappedIoRequired && !_hasMemoryMappedIo)
        {
            missingType = MemoryType.MemoryMappedIo;
            return false;
        }
        missingType = MemoryType.Unknown;
        return true;
    }

    /// <summary>Returns an independent copy of the configured reservations.</summary>
    /// <nova.when>Pass the returned array to any normaliser implementation.</nova.when>
    /// <nova.depends>Count</nova.depends>
    /// <returns>A copy that callers may retain independently of the plan.</returns>
    /// <example><code>MemoryReservation[] reservations = plan.ToArray();</code></example>
    public MemoryReservation[] ToArray()
    {
        MemoryReservation[] result = new MemoryReservation[_count];
        Array.Copy(_reservations, result, _count);
        return result;
    }

    private bool TryAdd(PhysicalAddress start, ulong pages, MemoryType type, MemoryCacheAttributes attributes)
    {
        if (_count >= _reservations.Length) return false;
        if (!MemoryReservation.TryCreate(start, pages, type, attributes, out MemoryReservation reservation)) return false;
        _reservations[_count++] = reservation;
        switch (type)
        {
            case MemoryType.LoaderKernelImage: _hasKernelImage = true; break;
            case MemoryType.BootStructures: _hasBootStructures = true; break;
            case MemoryType.Framebuffer: _hasFramebuffer = true; break;
            case MemoryType.MemoryMappedIo: _hasMemoryMappedIo = true; break;
            case MemoryType.PageTables: _hasPageTables = true; break;
            case MemoryType.EarlyAllocatorAllocations: _hasEarlyAllocations = true; break;
        }
        return true;
    }
}
