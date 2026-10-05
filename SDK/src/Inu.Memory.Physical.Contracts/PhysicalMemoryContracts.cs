using Inu.Core;
using Inu.Memory;
using Inu.Primitives;

namespace Inu.Memory.Physical;

/// <summary>Identifies the physical-frame allocation methodology selected by an SDK consumer.</summary>
/// <nova.when>Use when reporting or selecting the implementation behind physical-memory management.</nova.when>
/// <nova.depends>Inu.Memory.Contracts</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum PhysicalAllocatorMethod
{
    /// <summary>Uses one allocation bit for each managed physical frame.</summary>
    Bitmap = 0,
    /// <summary>Uses power-of-two blocks with split and buddy coalescing.</summary>
    Buddy = 1,
    /// <summary>Uses sorted free extents and exact range splitting.</summary>
    Extent = 2,
    /// <summary>Identifies an SDK-consumer implementation of <see cref="IPhysicalMemoryManager"/>.</summary>
    Custom = 3
}

/// <summary>Identifies the intended ownership of an allocation or reservation.</summary>
/// <nova.when>Use to classify physical pages for diagnostics and later address-space policy.</nova.when>
/// <nova.depends>PhysicalAllocation and PhysicalReservation</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum PhysicalMemoryPurpose
{
    /// <summary>No specialised ownership is required.</summary>
    General = 0,
    /// <summary>The pages will hold page-table structures.</summary>
    PageTables = 1,
    /// <summary>The pages will back kernel heap storage.</summary>
    KernelHeap = 2,
    /// <summary>The pages will back a kernel stack.</summary>
    KernelStack = 3,
    /// <summary>The pages must satisfy device DMA constraints.</summary>
    Dma = 4,
    /// <summary>The pages hold kernel or SDK metadata.</summary>
    Metadata = 5,
    /// <summary>The SDK consumer assigns a project-specific meaning.</summary>
    Custom = 6
}

/// <summary>Reports the outcome of a physical-memory operation without requiring exceptions for expected allocation failure.</summary>
/// <nova.when>Inspect when a physical allocator returns <see langword="false"/>.</nova.when>
/// <nova.depends>IPhysicalMemoryManager</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum PhysicalMemoryStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The supplied request, range, map, or capacity was invalid.</summary>
    InvalidParameter = 1,
    /// <summary>The manager has not been initialised.</summary>
    NotInitialized = 2,
    /// <summary>The manager was already initialised.</summary>
    AlreadyInitialized = 3,
    /// <summary>The caller-owned metadata workspace is too small.</summary>
    WorkspaceTooSmall = 4,
    /// <summary>No free range can satisfy the requested page count.</summary>
    OutOfMemory = 5,
    /// <summary>Free memory exists, but none satisfies the requested address or alignment constraints.</summary>
    AddressConstraintUnsatisfied = 6,
    /// <summary>The allocation token is unknown or has already been released.</summary>
    AllocationNotFound = 7,
    /// <summary>The reservation token is unknown or has already been released.</summary>
    ReservationNotFound = 8,
    /// <summary>The requested physical range is not completely free.</summary>
    RangeNotFree = 9,
    /// <summary>The bounded allocation or reservation record table is full.</summary>
    RecordCapacityExhausted = 10
}

/// <summary>Describes one exact page-aligned physical-frame range.</summary>
/// <nova.when>Use for reservations and for reporting allocator-owned physical ranges.</nova.when>
/// <nova.depends>Inu.Primitives.PhysicalAddress</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalFrameRange
{
    private PhysicalFrameRange(PhysicalAddress start, ulong pageCount)
    {
        Start = start;
        PageCount = pageCount;
    }

    /// <summary>Gets the first physical byte of the range.</summary>
    /// <nova.when>Use as the physical address of the first frame.</nova.when>
    public PhysicalAddress Start { get; }
    /// <summary>Gets the number of 4 KiB frames in the range.</summary>
    /// <nova.when>Use for frame accounting and page-table mapping.</nova.when>
    public ulong PageCount { get; }
    /// <summary>Gets the range length in bytes.</summary>
    /// <nova.when>Use for diagnostics or byte-address calculations.</nova.when>
    public ulong Length => PageCount * 4096UL;
    /// <summary>Gets the exclusive physical end address.</summary>
    /// <nova.when>Use for non-overlap and upper-bound checks.</nova.when>
    public PhysicalAddress EndExclusive
    {
        get { return new PhysicalAddress(Start.Value + Length); }
    }

    /// <summary>Attempts to create a validated 4 KiB page-aligned physical range.</summary>
    /// <nova.when>Use before reserving a fixed physical range.</nova.when>
    /// <nova.depends>4 KiB physical frame size</nova.depends>
    /// <returns><see langword="true"/> when the range is non-empty, aligned, and cannot overflow.</returns>
    /// <example><code>bool valid = PhysicalFrameRange.TryCreate(new PhysicalAddress(0x100000), 16, out PhysicalFrameRange range);</code></example>
    public static bool TryCreate(PhysicalAddress start, ulong pageCount, out PhysicalFrameRange range)
    {
        range = default;
        if (pageCount == 0 || (start.Value & 0xFFFUL) != 0) return false;
        if (pageCount > ulong.MaxValue / 4096UL) return false;
        ulong length = pageCount * 4096UL;
        if (start.Value > ulong.MaxValue - length) return false;
        range = new PhysicalFrameRange(start, pageCount);
        return true;
    }
}

/// <summary>Describes a contiguous allocation request and its physical-address constraints.</summary>
/// <nova.when>Use for normal, aligned, below-limit, or DMA-oriented frame allocation.</nova.when>
/// <nova.depends>PhysicalMemoryPurpose</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalAllocationRequest
{
    private PhysicalAllocationRequest(ulong pageCount, ulong alignmentPages, PhysicalAddress minimumAddress, PhysicalAddress maximumAddressExclusive, PhysicalMemoryPurpose purpose)
    {
        PageCount = pageCount;
        AlignmentPages = alignmentPages;
        MinimumAddress = minimumAddress;
        MaximumAddressExclusive = maximumAddressExclusive;
        Purpose = purpose;
    }

    /// <summary>Gets the requested number of contiguous 4 KiB pages.</summary>
    /// <nova.when>Use to compare requested and allocator-reserved page counts.</nova.when>
    public ulong PageCount { get; }
    /// <summary>Gets the required power-of-two alignment in pages.</summary>
    /// <nova.when>Use for page-table, huge-page, and device-alignment constraints.</nova.when>
    public ulong AlignmentPages { get; }
    /// <summary>Gets the inclusive minimum physical address.</summary>
    /// <nova.when>Use to constrain allocations away from low physical memory.</nova.when>
    public PhysicalAddress MinimumAddress { get; }
    /// <summary>Gets the exclusive maximum physical address, or zero for no upper limit.</summary>
    /// <nova.when>Use for DMA32-style or device-specific physical-address ceilings.</nova.when>
    public PhysicalAddress MaximumAddressExclusive { get; }
    /// <summary>Gets the intended allocation ownership.</summary>
    /// <nova.when>Use for diagnostics and later mapping policy.</nova.when>
    public PhysicalMemoryPurpose Purpose { get; }

    /// <summary>Attempts to create a validated contiguous frame-allocation request.</summary>
    /// <nova.when>Use before calling <see cref="IPhysicalMemoryManager.TryAllocate"/>.</nova.when>
    /// <nova.depends>4 KiB physical frame size and power-of-two alignment</nova.depends>
    /// <returns><see langword="true"/> when the page count, alignment, and address interval are valid.</returns>
    /// <example><code>bool valid = PhysicalAllocationRequest.TryCreate(8, 8, default, new PhysicalAddress(0x100000000), PhysicalMemoryPurpose.Dma, out PhysicalAllocationRequest request);</code></example>
    public static bool TryCreate(ulong pageCount, ulong alignmentPages, PhysicalAddress minimumAddress, PhysicalAddress maximumAddressExclusive, PhysicalMemoryPurpose purpose, out PhysicalAllocationRequest request)
    {
        request = default;
        if (pageCount == 0 || pageCount > ulong.MaxValue / 4096UL) return false;
        if (alignmentPages == 0 || alignmentPages > ulong.MaxValue / 4096UL || (alignmentPages & (alignmentPages - 1)) != 0) return false;
        if ((minimumAddress.Value & 0xFFFUL) != 0 || (maximumAddressExclusive.Value & 0xFFFUL) != 0) return false;
        if (maximumAddressExclusive.Value != 0 && minimumAddress.Value >= maximumAddressExclusive.Value) return false;
        request = new PhysicalAllocationRequest(pageCount, alignmentPages, minimumAddress, maximumAddressExclusive, purpose);
        return true;
    }
}

/// <summary>Represents one live physical allocation and its release token.</summary>
/// <nova.when>Retain until the allocation is released back to the same manager.</nova.when>
/// <nova.depends>IPhysicalMemoryManager.TryAllocate</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalAllocation
{
    internal PhysicalAllocation(PhysicalFrameRange range, ulong requestedPageCount, ulong token, PhysicalMemoryPurpose purpose)
    {
        Range = range;
        RequestedPageCount = requestedPageCount;
        Token = token;
        Purpose = purpose;
    }

    /// <summary>Gets the actual physical range owned by the allocation.</summary>
    /// <nova.when>Use for mapping or hardware programming.</nova.when>
    public PhysicalFrameRange Range { get; }
    /// <summary>Gets the page count requested by the caller before allocator rounding.</summary>
    /// <nova.when>Use to measure buddy-allocator internal fragmentation.</nova.when>
    public ulong RequestedPageCount { get; }
    /// <summary>Gets the opaque token required for release validation.</summary>
    /// <nova.when>Preserve unchanged until releasing the allocation.</nova.when>
    public ulong Token { get; }
    /// <summary>Gets the declared ownership purpose.</summary>
    /// <nova.when>Use for allocation diagnostics.</nova.when>
    public PhysicalMemoryPurpose Purpose { get; }
}

/// <summary>Represents one live fixed physical reservation and its release token.</summary>
/// <nova.when>Retain while an allocator-managed range must remain unavailable for ordinary allocation.</nova.when>
/// <nova.depends>IPhysicalMemoryManager.TryReserve</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalReservation
{
    internal PhysicalReservation(PhysicalFrameRange range, ulong token, PhysicalMemoryPurpose purpose)
    {
        Range = range;
        Token = token;
        Purpose = purpose;
    }

    /// <summary>Gets the exact reserved physical range.</summary>
    /// <nova.when>Use for diagnostics and ownership tracking.</nova.when>
    public PhysicalFrameRange Range { get; }
    /// <summary>Gets the opaque token required for reservation release.</summary>
    /// <nova.when>Preserve unchanged until releasing the reservation.</nova.when>
    public ulong Token { get; }
    /// <summary>Gets the declared reservation purpose.</summary>
    /// <nova.when>Use for reservation diagnostics.</nova.when>
    public PhysicalMemoryPurpose Purpose { get; }
}

/// <summary>Reports current page accounting for one physical-memory manager.</summary>
/// <nova.when>Use for boot diagnostics, pressure reporting, and allocator comparisons.</nova.when>
/// <nova.depends>IPhysicalMemoryManager.GetStatistics</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalMemoryStatistics
{
    private PhysicalMemoryStatistics(ulong totalManagedPages, ulong freePages, ulong allocatedPages, ulong reservedPages, ulong largestFreeRunPages, int activeAllocations, int activeReservations)
    {
        TotalManagedPages = totalManagedPages;
        FreePages = freePages;
        AllocatedPages = allocatedPages;
        ReservedPages = reservedPages;
        LargestFreeRunPages = largestFreeRunPages;
        ActiveAllocations = activeAllocations;
        ActiveReservations = activeReservations;
    }

    /// <summary>Gets the number of frames initially admitted from the normalised map.</summary>
    /// <nova.when>Use as the accounting denominator for this manager.</nova.when>
    public ulong TotalManagedPages { get; }
    /// <summary>Gets the number of frames currently available for allocation.</summary>
    /// <nova.when>Use for memory-pressure checks.</nova.when>
    public ulong FreePages { get; }
    /// <summary>Gets the number of frames currently owned by live allocations.</summary>
    /// <nova.when>Use for allocation accounting, including buddy rounding.</nova.when>
    public ulong AllocatedPages { get; }
    /// <summary>Gets the number of frames currently held by live reservations.</summary>
    /// <nova.when>Use for fixed-range ownership accounting.</nova.when>
    public ulong ReservedPages { get; }
    /// <summary>Gets the largest currently free contiguous run or buddy block.</summary>
    /// <nova.when>Use when deciding whether a large contiguous request is likely to succeed.</nova.when>
    public ulong LargestFreeRunPages { get; }
    /// <summary>Gets the number of live allocation records.</summary>
    /// <nova.when>Use to monitor bounded allocation-record capacity.</nova.when>
    public int ActiveAllocations { get; }
    /// <summary>Gets the number of live reservation records.</summary>
    /// <nova.when>Use to monitor bounded reservation-record capacity.</nova.when>
    public int ActiveReservations { get; }

    /// <summary>Creates an immutable physical-memory statistics snapshot.</summary>
    /// <nova.when>Use from physical-memory manager implementations when returning current accounting.</nova.when>
    /// <nova.depends>Consistent manager counters captured at one point in time</nova.depends>
    /// <returns>A statistics value containing the supplied counters.</returns>
    /// <example><code>PhysicalMemoryStatistics statistics = PhysicalMemoryStatistics.Create(total, free, allocated, reserved, largest, allocations, reservations);</code></example>
    public static PhysicalMemoryStatistics Create(ulong totalManagedPages, ulong freePages, ulong allocatedPages, ulong reservedPages, ulong largestFreeRunPages, int activeAllocations, int activeReservations)
        => new(totalManagedPages, freePages, allocatedPages, reservedPages, largestFreeRunPages, activeAllocations, activeReservations);
}

/// <summary>Describes caller-owned raw storage used exclusively for allocator metadata.</summary>
/// <nova.when>Provide memory reserved before physical allocation begins so the manager itself never requires the kernel heap.</nova.when>
/// <nova.depends>Stable writable memory address valid for the manager lifetime</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct PhysicalAllocatorWorkspace
{
    private PhysicalAllocatorWorkspace(nint address, ulong byteLength)
    {
        Address = address;
        ByteLength = byteLength;
    }

    /// <summary>Gets the writable metadata address.</summary>
    /// <nova.when>Use only when integrating custom allocator implementations.</nova.when>
    public nint Address { get; }
    /// <summary>Gets the number of writable metadata bytes.</summary>
    /// <nova.when>Use to validate implementation-specific workspace requirements.</nova.when>
    public ulong ByteLength { get; }

    /// <summary>Attempts to describe a non-null writable metadata buffer.</summary>
    /// <nova.when>Use with static, boot-reserved, stack, or otherwise caller-owned storage before manager initialisation.</nova.when>
    /// <nova.depends>The caller owns the buffer lifetime</nova.depends>
    /// <returns><see langword="true"/> when the address is non-zero, 8-byte aligned, and the buffer is non-empty.</returns>
    /// <example><code>bool valid = PhysicalAllocatorWorkspace.TryCreate(address, byteLength, out PhysicalAllocatorWorkspace workspace);</code></example>
    public static bool TryCreate(nint address, ulong byteLength, out PhysicalAllocatorWorkspace workspace)
    {
        workspace = default;
        if (address == 0 || byteLength == 0 || (((nuint)address) & (nuint)7) != 0) return false;
        workspace = new PhysicalAllocatorWorkspace(address, byteLength);
        return true;
    }
}

/// <summary>Defines the replaceable contract for physical-frame allocation.</summary>
/// <nova.when>Implement for a custom allocator or consume through a selected Inu allocator methodology.</nova.when>
/// <nova.depends>NormalisedMemoryMap and PhysicalAllocatorWorkspace</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public interface IPhysicalMemoryManager
{
    /// <summary>Gets the allocator methodology.</summary>
    /// <nova.when>Use for diagnostics and configuration reporting.</nova.when>
    PhysicalAllocatorMethod Method { get; }
    /// <summary>Gets whether the manager has accepted a normalised map and workspace.</summary>
    /// <nova.when>Check before allocation when manager ownership is indirect.</nova.when>
    bool IsInitialized { get; }
    /// <summary>Gets the physical frame size in bytes.</summary>
    /// <nova.when>Use when converting frame counts to byte lengths.</nova.when>
    ulong PageSize { get; }

    /// <summary>Initialises the manager from immediately allocatable ranges in a normalised boot map.</summary>
    /// <nova.when>Call once after boot memory-map normalisation and before virtual-memory construction.</nova.when>
    /// <nova.depends>NormalisedMemoryMap and caller-owned workspace</nova.depends>
    /// <returns><see langword="true"/> when initialisation succeeds.</returns>
    /// <example><code>bool ready = manager.TryInitialize(map, workspace, 128, 32, out PhysicalMemoryStatus status);</code></example>
    bool TryInitialize(NormalisedMemoryMap map, PhysicalAllocatorWorkspace workspace, int allocationCapacity, int reservationCapacity, out PhysicalMemoryStatus status);

    /// <summary>Allocates one contiguous physical range.</summary>
    /// <nova.when>Use for page tables, stacks, heap backing, DMA, and other frame ownership.</nova.when>
    /// <nova.depends>Successful TryInitialize</nova.depends>
    /// <returns><see langword="true"/> when a range satisfying all constraints is allocated.</returns>
    /// <example><code>bool allocated = manager.TryAllocate(request, out PhysicalAllocation allocation, out PhysicalMemoryStatus status);</code></example>
    bool TryAllocate(PhysicalAllocationRequest request, out PhysicalAllocation allocation, out PhysicalMemoryStatus status);

    /// <summary>Releases a live allocation back to the same manager.</summary>
    /// <nova.when>Use when the owning subsystem no longer needs the physical frames.</nova.when>
    /// <nova.depends>PhysicalAllocation token returned by TryAllocate</nova.depends>
    /// <returns><see langword="true"/> when the live allocation was found and released exactly once.</returns>
    /// <example><code>bool released = manager.TryRelease(allocation, out PhysicalMemoryStatus status);</code></example>
    bool TryRelease(PhysicalAllocation allocation, out PhysicalMemoryStatus status);

    /// <summary>Removes an exact currently-free range from ordinary allocation.</summary>
    /// <nova.when>Use for late hardware discoveries or physical metadata that must become reserved after manager initialisation.</nova.when>
    /// <nova.depends>PhysicalFrameRange</nova.depends>
    /// <returns><see langword="true"/> when every requested frame was free and the reservation was recorded.</returns>
    /// <example><code>bool reserved = manager.TryReserve(range, PhysicalMemoryPurpose.Metadata, out PhysicalReservation reservation, out PhysicalMemoryStatus status);</code></example>
    bool TryReserve(PhysicalFrameRange range, PhysicalMemoryPurpose purpose, out PhysicalReservation reservation, out PhysicalMemoryStatus status);

    /// <summary>Releases a live reservation back to the same manager.</summary>
    /// <nova.when>Use only when the reserving subsystem has made the exact range safe for general reuse.</nova.when>
    /// <nova.depends>PhysicalReservation token returned by TryReserve</nova.depends>
    /// <returns><see langword="true"/> when the live reservation was found and released exactly once.</returns>
    /// <example><code>bool released = manager.TryReleaseReservation(reservation, out PhysicalMemoryStatus status);</code></example>
    bool TryReleaseReservation(PhysicalReservation reservation, out PhysicalMemoryStatus status);

    /// <summary>Returns a current allocator accounting snapshot.</summary>
    /// <nova.when>Use for diagnostics and memory-pressure decisions.</nova.when>
    /// <nova.depends>Current allocator state</nova.depends>
    /// <returns>Current total, free, allocated, reserved, and largest-run counters.</returns>
    /// <example><code>PhysicalMemoryStatistics statistics = manager.GetStatistics();</code></example>
    PhysicalMemoryStatistics GetStatistics();
}
