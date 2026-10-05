using Inu.Core;

namespace Inu.Memory;

/// <summary>Identifies the architecture-independent ownership of a physical-memory range.</summary>
/// <nova.when>Use when interpreting a normalised boot memory map or creating explicit reservations.</nova.when>
/// <nova.depends>Inu.Core</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum MemoryType
{
    /// <summary>The range has no recognised ownership.</summary>
    Unknown = 0,
    /// <summary>The range is conventional memory available to a physical allocator.</summary>
    UsableConventional = 1,
    /// <summary>The range contains firmware loader data or the Inu kernel image.</summary>
    LoaderKernelImage = 2,
    /// <summary>The range is owned by UEFI boot services until ExitBootServices succeeds.</summary>
    BootServices = 3,
    /// <summary>The range is required by UEFI runtime services.</summary>
    RuntimeServices = 4,
    /// <summary>The range may be reclaimed after ACPI tables have been copied or consumed.</summary>
    AcpiReclaimable = 5,
    /// <summary>The range contains ACPI non-volatile sleep state.</summary>
    AcpiNvs = 6,
    /// <summary>The range backs a linear framebuffer.</summary>
    Framebuffer = 7,
    /// <summary>The range is memory-mapped device input/output.</summary>
    MemoryMappedIo = 8,
    /// <summary>The range is reserved by firmware or by an unknown platform component.</summary>
    FirmwareReserved = 9,
    /// <summary>The range is known to contain unusable physical memory.</summary>
    BadMemory = 10,
    /// <summary>The range contains byte-addressable persistent memory.</summary>
    PersistentMemory = 11,
    /// <summary>The range contains Inu boot hand-off structures.</summary>
    BootStructures = 12,
    /// <summary>The range contains active page tables.</summary>
    PageTables = 13,
    /// <summary>The range was consumed by the early boot allocator.</summary>
    EarlyAllocatorAllocations = 14
}

/// <summary>Defines portable caching and protection attributes for a physical-memory range.</summary>
/// <nova.when>Use when preserving firmware attributes or preparing later page-table mappings.</nova.when>
/// <nova.depends>Platform memory-attribute translation</nova.depends>
[Flags]
public enum MemoryCacheAttributes : ulong
{
    /// <summary>No cache or protection attribute was reported.</summary>
    None = 0,
    /// <summary>The range is uncacheable.</summary>
    Uncacheable = 1UL << 0,
    /// <summary>The range supports write-combining.</summary>
    WriteCombining = 1UL << 1,
    /// <summary>The range uses write-through caching.</summary>
    WriteThrough = 1UL << 2,
    /// <summary>The range uses write-back caching.</summary>
    WriteBack = 1UL << 3,
    /// <summary>The range is uncached when exported to another agent.</summary>
    UncachedExported = 1UL << 4,
    /// <summary>Writes must be prohibited.</summary>
    WriteProtected = 1UL << 5,
    /// <summary>Reads must be prohibited.</summary>
    ReadProtected = 1UL << 6,
    /// <summary>Instruction execution must be prohibited.</summary>
    ExecuteProtected = 1UL << 7,
    /// <summary>The range is non-volatile.</summary>
    NonVolatile = 1UL << 8,
    /// <summary>The range has enhanced reliability.</summary>
    MoreReliable = 1UL << 9,
    /// <summary>The range is read-only.</summary>
    ReadOnly = 1UL << 10,
    /// <summary>The range has a platform-specific purpose.</summary>
    SpecificPurpose = 1UL << 11,
    /// <summary>The range may be used for CPU cryptographic protection.</summary>
    CpuCrypto = 1UL << 12
}

/// <summary>Identifies whether firmware requires a range after ExitBootServices.</summary>
/// <nova.when>Use when deciding whether a range may be reclaimed or remapped.</nova.when>
/// <nova.depends>UEFI runtime memory attributes</nova.depends>
public enum MemoryRuntimeStatus
{
    /// <summary>The range is not part of the runtime-services address map.</summary>
    NotRuntime = 0,
    /// <summary>The range contains runtime-service executable code.</summary>
    RuntimeCode = 1,
    /// <summary>The range contains runtime-service data.</summary>
    RuntimeData = 2,
    /// <summary>The range combines runtime-service code and data ownership.</summary>
    RuntimeCodeAndData = 3
}

/// <summary>Identifies when a physical-memory range may be allocated.</summary>
/// <nova.when>Use when filtering a normalised map for allocator candidates.</nova.when>
/// <nova.depends>MemoryType and boot lifecycle</nova.depends>
public enum MemoryAvailability
{
    /// <summary>The range must not be allocated.</summary>
    Unavailable = 0,
    /// <summary>The range is available after ExitBootServices succeeds.</summary>
    AvailableAfterExitBootServices = 1,
    /// <summary>The range becomes available after ACPI initialisation.</summary>
    ReclaimableAfterAcpiInitialization = 2,
    /// <summary>The range remains permanently reserved.</summary>
    PermanentlyReserved = 3,
    /// <summary>The range remains owned by firmware runtime services.</summary>
    RuntimeOwned = 4,
    /// <summary>The range is physically defective or unaccepted.</summary>
    Defective = 5
}

/// <summary>Selects one of the supported overlap-resolution implementations.</summary>
/// <nova.when>Use when selecting how malformed or overlapping firmware maps are handled.</nova.when>
/// <nova.depends>Inu.Boot.Memory normaliser implementations</nova.depends>
public enum MemoryMapNormalisationMethod
{
    /// <summary>Rejects incompatible firmware overlaps while still applying explicit Inu reservations.</summary>
    Strict = 0,
    /// <summary>Resolves overlaps by selecting the safest highest-priority ownership.</summary>
    SafetyPriority = 1,
    /// <summary>Converts every incompatible overlap into firmware-reserved or runtime-owned memory.</summary>
    Conservative = 2
}

/// <summary>Reports the result of memory-map normalisation.</summary>
/// <nova.when>Use to diagnose rejected descriptors, capacity limits, or overlap conflicts.</nova.when>
/// <nova.depends>IMemoryMapNormaliser</nova.depends>
public enum MemoryMapNormalisationStatus
{
    /// <summary>Normalisation completed successfully.</summary>
    Success = 0,
    /// <summary>An input was missing or empty.</summary>
    InvalidInput = 1,
    /// <summary>A descriptor was unaligned, zero-length, or overflowed its address range.</summary>
    InvalidDescriptor = 2,
    /// <summary>The strict implementation found incompatible overlapping firmware descriptors.</summary>
    OverlapConflict = 3,
    /// <summary>The supplied workspace could not contain all boundaries or output ranges.</summary>
    InsufficientCapacity = 4,
    /// <summary>The final UEFI map had not been sealed by a successful ExitBootServices call.</summary>
    FinalMapRequired = 5
}
