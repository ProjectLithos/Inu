using System;
using Inu.Core;
using Inu.Primitives;

namespace Inu.Memory.Virtual;

/// <summary>Identifies the virtual-memory implementation selected by an SDK consumer.</summary>
/// <nova.when>Use when reporting or selecting the address-translation implementation behind a kernel address space.</nova.when>
/// <nova.depends>IVirtualMemoryManager</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum VirtualMemoryMethod
{
    /// <summary>Uses the four-level x64 paging hierarchy with 4 KiB, 2 MiB, and 1 GiB leaf mappings.</summary>
    X64FourLevel = 0,
    /// <summary>Identifies an SDK-consumer implementation of <see cref="IVirtualMemoryManager"/>.</summary>
    Custom = 1
}

/// <summary>Identifies a page size supported by virtual-memory contracts.</summary>
/// <nova.when>Use when choosing the granularity of a virtual mapping.</nova.when>
/// <nova.depends>Architecture support for the selected leaf size</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum VirtualPageSize : ulong
{
    /// <summary>A standard 4 KiB page.</summary>
    Page4KiB = 4096UL,
    /// <summary>A 2 MiB large page.</summary>
    Page2MiB = 2UL * 1024UL * 1024UL,
    /// <summary>A 1 GiB large page.</summary>
    Page1GiB = 1024UL * 1024UL * 1024UL
}

/// <summary>Defines architecture-neutral access permissions and cache intent for a virtual mapping.</summary>
/// <nova.when>Use when mapping or changing protection without exposing architecture-specific entry bits.</nova.when>
/// <nova.depends>Architecture-specific page-table encoder</nova.depends>
[Flags]
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum VirtualMemoryProtection : ulong
{
    /// <summary>No access permission is selected.</summary>
    None = 0,
    /// <summary>The mapping may be read.</summary>
    Read = 1UL << 0,
    /// <summary>The mapping may be written.</summary>
    Write = 1UL << 1,
    /// <summary>Instructions may execute from the mapping.</summary>
    Execute = 1UL << 2,
    /// <summary>User-mode code may access the mapping subject to read/write/execute permissions.</summary>
    User = 1UL << 3,
    /// <summary>The translation may remain global across address-space switches.</summary>
    Global = 1UL << 4,
    /// <summary>The mapping targets device memory and requests uncached/strongly ordered semantics.</summary>
    Device = 1UL << 5,
    /// <summary>The mapping requests write-through caching.</summary>
    WriteThrough = 1UL << 6
}

/// <summary>Reports the outcome of a virtual-memory operation without requiring exceptions for expected mapping failures.</summary>
/// <nova.when>Inspect when a virtual-memory manager returns <see langword="false"/>.</nova.when>
/// <nova.depends>IVirtualMemoryManager</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum VirtualMemoryStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The supplied address, size, permissions, or root was invalid.</summary>
    InvalidParameter = 1,
    /// <summary>The manager has not been initialized.</summary>
    NotInitialized = 2,
    /// <summary>The manager was already initialized.</summary>
    AlreadyInitialized = 3,
    /// <summary>The requested virtual address is not canonical for the active architecture.</summary>
    NonCanonicalAddress = 4,
    /// <summary>A mapping already exists at the requested virtual address.</summary>
    AlreadyMapped = 5,
    /// <summary>No mapping exists for the requested virtual address.</summary>
    NotMapped = 6,
    /// <summary>A physical page-table page could not be allocated.</summary>
    PageTableAllocationFailed = 7,
    /// <summary>The architecture backend could not access a page-table page.</summary>
    PageTableAccessFailed = 8,
    /// <summary>The requested page size is unsupported by the selected implementation or processor.</summary>
    UnsupportedPageSize = 9,
    /// <summary>The requested permission combination is unsupported or unsafe.</summary>
    UnsupportedProtection = 10,
    /// <summary>A translation-cache invalidation or address-space activation operation failed.</summary>
    ArchitectureOperationFailed = 11
}

/// <summary>Describes one page-aligned virtual-address range.</summary>
/// <nova.when>Use for unmapping, protection changes, and address-space diagnostics.</nova.when>
/// <nova.depends>VirtualPageSize</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct VirtualAddressRange
{
    private readonly ulong _pageSizeBytes;

    private VirtualAddressRange(ulong start, ulong pageCount, VirtualPageSize pageSize)
    {
        Start = start;
        PageCount = pageCount;
        PageSize = pageSize;
        _pageSizeBytes = (ulong)pageSize;
    }

    /// <summary>Gets the first virtual byte in the range.</summary>
    /// <nova.when>Use as the first mapping or protection-change address.</nova.when>
    public ulong Start { get; }
    /// <summary>Gets the number of pages in the range.</summary>
    /// <nova.when>Use for range iteration and accounting.</nova.when>
    public ulong PageCount { get; }
    /// <summary>Gets the mapping granularity of every page in the range.</summary>
    /// <nova.when>Use when walking or removing leaf entries.</nova.when>
    public VirtualPageSize PageSize { get; }
    /// <summary>Gets the byte length of the range.</summary>
    /// <nova.when>Use for diagnostics and end-address calculations.</nova.when>
    public ulong Length { get { return PageCount * _pageSizeBytes; } }

    /// <summary>Attempts to create a validated aligned virtual-address range.</summary>
    /// <nova.when>Use before unmapping or changing protection on a contiguous range.</nova.when>
    /// <nova.depends>Power-of-two page size and checked address arithmetic</nova.depends>
    /// <returns><see langword="true"/> when the range is non-empty, aligned, and does not overflow.</returns>
    /// <example><code>bool valid = VirtualAddressRange.TryCreate(0xFFFF800000000000, 4, VirtualPageSize.Page4KiB, out VirtualAddressRange range);</code></example>
    public static bool TryCreate(ulong start, ulong pageCount, VirtualPageSize pageSize, out VirtualAddressRange range)
    {
        range = default;
        ulong size = (ulong)pageSize;
        if (!IsSupportedSize(size) || pageCount == 0 || (start & (size - 1UL)) != 0) return false;
        if (pageCount > ulong.MaxValue / size) return false;
        ulong length = pageCount * size;
        if (start > ulong.MaxValue - length) return false;
        range = new VirtualAddressRange(start, pageCount, pageSize);
        return true;
    }

    private static bool IsSupportedSize(ulong size) => size == 4096UL || size == 2097152UL || size == 1073741824UL;
}

/// <summary>Describes one physical-to-virtual mapping request.</summary>
/// <nova.when>Use to add a leaf translation to an address space.</nova.when>
/// <nova.depends>VirtualPageSize and VirtualMemoryProtection</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct VirtualMappingRequest
{
    private VirtualMappingRequest(ulong virtualAddress, PhysicalAddress physicalAddress, VirtualPageSize pageSize, VirtualMemoryProtection protection)
    {
        VirtualAddress = virtualAddress;
        PhysicalAddress = physicalAddress;
        PageSize = pageSize;
        Protection = protection;
    }

    /// <summary>Gets the aligned virtual leaf address.</summary>
    /// <nova.when>Use as the translation key.</nova.when>
    public ulong VirtualAddress { get; }
    /// <summary>Gets the aligned physical leaf address.</summary>
    /// <nova.when>Use as the translation target.</nova.when>
    public PhysicalAddress PhysicalAddress { get; }
    /// <summary>Gets the requested leaf page size.</summary>
    /// <nova.when>Use to select the architecture leaf level.</nova.when>
    public VirtualPageSize PageSize { get; }
    /// <summary>Gets architecture-neutral access and caching intent.</summary>
    /// <nova.when>Use to encode the leaf entry.</nova.when>
    public VirtualMemoryProtection Protection { get; }

    /// <summary>Attempts to create a validated single-page mapping request.</summary>
    /// <nova.when>Use before calling <see cref="IVirtualMemoryManager.TryMap"/>.</nova.when>
    /// <nova.depends>Aligned physical and virtual addresses</nova.depends>
    /// <returns><see langword="true"/> when both addresses are aligned and at least read access is requested.</returns>
    /// <example><code>bool valid = VirtualMappingRequest.TryCreate(0xFFFF800000200000, new PhysicalAddress(0x200000), VirtualPageSize.Page2MiB, VirtualMemoryProtection.Read | VirtualMemoryProtection.Write, out VirtualMappingRequest request);</code></example>
    public static bool TryCreate(ulong virtualAddress, PhysicalAddress physicalAddress, VirtualPageSize pageSize, VirtualMemoryProtection protection, out VirtualMappingRequest request)
    {
        request = default;
        ulong size = (ulong)pageSize;
        if (size != 4096UL && size != 2097152UL && size != 1073741824UL) return false;
        if ((virtualAddress & (size - 1UL)) != 0 || (physicalAddress.Value & (size - 1UL)) != 0) return false;
        if ((protection & VirtualMemoryProtection.Read) == 0) return false;
        request = new VirtualMappingRequest(virtualAddress, physicalAddress, pageSize, protection);
        return true;
    }
}

/// <summary>Reports one resolved virtual-to-physical translation.</summary>
/// <nova.when>Use for page-fault diagnostics, address validation, and mapping inspection.</nova.when>
/// <nova.depends>IVirtualMemoryManager.TryTranslate</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct VirtualTranslation
{
    /// <summary>Creates one resolved translation result.</summary>
    /// <nova.when>Use from virtual-memory implementations after decoding a present leaf entry.</nova.when>
    /// <nova.depends>Validated leaf address and page size</nova.depends>
    /// <returns>A translation value containing the supplied validated fields.</returns>
    /// <example><code>VirtualTranslation translation = new(virtualAddress, physicalAddress, VirtualPageSize.Page4KiB, VirtualMemoryProtection.Read);</code></example>
    public VirtualTranslation(ulong virtualAddress, PhysicalAddress physicalAddress, VirtualPageSize pageSize, VirtualMemoryProtection protection)
    {
        VirtualAddress = virtualAddress;
        PhysicalAddress = physicalAddress;
        PageSize = pageSize;
        Protection = protection;
    }

    /// <summary>Gets the queried virtual address.</summary>
    /// <nova.when>Use to correlate the result with a page fault or probe.</nova.when>
    public ulong VirtualAddress { get; }
    /// <summary>Gets the exact translated physical byte address including the leaf offset.</summary>
    /// <nova.when>Use for diagnostics or physical ownership checks.</nova.when>
    public PhysicalAddress PhysicalAddress { get; }
    /// <summary>Gets the leaf page size that supplied the translation.</summary>
    /// <nova.when>Use to identify 4 KiB versus large-page translations.</nova.when>
    public VirtualPageSize PageSize { get; }
    /// <summary>Gets the decoded effective leaf protection.</summary>
    /// <nova.when>Use to validate access policy.</nova.when>
    public VirtualMemoryProtection Protection { get; }
}

/// <summary>Provides a current virtual-memory accounting snapshot.</summary>
/// <nova.when>Use for diagnostics and page-table memory accounting.</nova.when>
/// <nova.depends>IVirtualMemoryManager.GetStatistics</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct VirtualMemoryStatistics
{
    /// <summary>Creates one immutable virtual-memory statistics snapshot.</summary>
    /// <nova.when>Use from manager implementations when exposing current accounting.</nova.when>
    /// <nova.depends>Current address-space state</nova.depends>
    /// <returns>An immutable statistics value containing the supplied counters.</returns>
    /// <example><code>VirtualMemoryStatistics statistics = new(1024, 16, 2, 1);</code></example>
    public VirtualMemoryStatistics(ulong mappedPages4KiB, ulong mappedPages2MiB, ulong mappedPages1GiB, ulong pageTablePages)
    {
        MappedPages4KiB = mappedPages4KiB;
        MappedPages2MiB = mappedPages2MiB;
        MappedPages1GiB = mappedPages1GiB;
        PageTablePages = pageTablePages;
    }

    /// <summary>Gets the number of managed 4 KiB leaf mappings.</summary>
    /// <nova.when>Use for fine-grained mapping accounting.</nova.when>
    public ulong MappedPages4KiB { get; }
    /// <summary>Gets the number of managed 2 MiB leaf mappings.</summary>
    /// <nova.when>Use for large-page accounting.</nova.when>
    public ulong MappedPages2MiB { get; }
    /// <summary>Gets the number of managed 1 GiB leaf mappings.</summary>
    /// <nova.when>Use for huge-page accounting.</nova.when>
    public ulong MappedPages1GiB { get; }
    /// <summary>Gets the number of page-table pages owned or protected by the manager.</summary>
    /// <nova.when>Use to quantify translation-structure physical-memory cost.</nova.when>
    public ulong PageTablePages { get; }
}

/// <summary>Defines the architecture-neutral virtual-memory service contract.</summary>
/// <nova.when>Implement to provide a custom address-space manager or consume through SDK-neutral kernel code.</nova.when>
/// <nova.depends>Physical-memory ownership and architecture translation primitives</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public interface IVirtualMemoryManager
{
    /// <summary>Gets the implementation methodology.</summary>
    /// <nova.when>Use for configuration and diagnostics.</nova.when>
    VirtualMemoryMethod Method { get; }
    /// <summary>Gets whether the manager has attached to or created an address space.</summary>
    /// <nova.when>Check before mapping when ownership is indirect.</nova.when>
    bool IsInitialized { get; }
    /// <summary>Gets the root page-table physical address.</summary>
    /// <nova.when>Use for diagnostics and architecture address-space activation.</nova.when>
    PhysicalAddress RootPhysicalAddress { get; }

    /// <summary>Maps one aligned physical page into one aligned virtual leaf address.</summary>
    /// <nova.when>Use after physical backing has been allocated or reserved.</nova.when>
    /// <nova.depends>Initialized manager and architecture page-table storage</nova.depends>
    /// <returns><see langword="true"/> when the leaf mapping is installed and invalidated as required.</returns>
    /// <example><code>bool mapped = manager.TryMap(request, out VirtualMemoryStatus status);</code></example>
    bool TryMap(VirtualMappingRequest request, out VirtualMemoryStatus status);

    /// <summary>Removes the leaf mapping at one aligned virtual address.</summary>
    /// <nova.when>Use before releasing or repurposing the physical backing.</nova.when>
    /// <nova.depends>Existing leaf mapping</nova.depends>
    /// <returns><see langword="true"/> when a present leaf mapping was removed.</returns>
    /// <example><code>bool unmapped = manager.TryUnmap(virtualAddress, out VirtualMemoryStatus status);</code></example>
    bool TryUnmap(ulong virtualAddress, out VirtualMemoryStatus status);

    /// <summary>Changes protection on one existing leaf mapping without changing its physical target.</summary>
    /// <nova.when>Use for W^X transitions, read-only data, user access, and device cache policy.</nova.when>
    /// <nova.depends>Existing leaf mapping</nova.depends>
    /// <returns><see langword="true"/> when protection was replaced and the translation invalidated.</returns>
    /// <example><code>bool protectedPage = manager.TryProtect(virtualAddress, VirtualMemoryProtection.Read, out VirtualMemoryStatus status);</code></example>
    bool TryProtect(ulong virtualAddress, VirtualMemoryProtection protection, out VirtualMemoryStatus status);

    /// <summary>Resolves one virtual byte address through the current page tables.</summary>
    /// <nova.when>Use for diagnostics and access validation without changing mappings.</nova.when>
    /// <nova.depends>Current page-table hierarchy</nova.depends>
    /// <returns><see langword="true"/> when a present leaf covers the address.</returns>
    /// <example><code>bool translated = manager.TryTranslate(virtualAddress, out VirtualTranslation translation, out VirtualMemoryStatus status);</code></example>
    bool TryTranslate(ulong virtualAddress, out VirtualTranslation translation, out VirtualMemoryStatus status);

    /// <summary>Returns a current virtual-memory accounting snapshot.</summary>
    /// <nova.when>Use for diagnostics and page-table memory-cost reporting.</nova.when>
    /// <nova.depends>Current manager state</nova.depends>
    /// <returns>Current leaf and page-table counters.</returns>
    /// <example><code>VirtualMemoryStatistics statistics = manager.GetStatistics();</code></example>
    VirtualMemoryStatistics GetStatistics();
}
