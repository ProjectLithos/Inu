using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

/// <summary>Reports the result of a freestanding virtual-memory operation.</summary>
public enum KernelVirtualMemoryStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The supplied address, page size, or protection was invalid.</summary>
    InvalidParameter = 1,
    /// <summary>The virtual-memory manager has not been initialized.</summary>
    NotInitialized = 2,
    /// <summary>The virtual-memory manager was already initialized.</summary>
    AlreadyInitialized = 3,
    /// <summary>The supplied virtual address is not canonical for four-level x64 paging.</summary>
    NonCanonicalAddress = 4,
    /// <summary>A leaf mapping already occupies the requested address.</summary>
    AlreadyMapped = 5,
    /// <summary>No present leaf mapping covers the requested address.</summary>
    NotMapped = 6,
    /// <summary>The physical-memory manager could not supply a page-table page.</summary>
    PhysicalAllocationFailed = 7,
    /// <summary>The bounded bootstrap page-table discovery table is full.</summary>
    PageTableCapacityExhausted = 8,
    /// <summary>A required x64 control-register or TLB invalidation operation failed.</summary>
    ArchitectureOperationFailed = 9,
    /// <summary>The requested page size is not supported by this bootstrap manager.</summary>
    UnsupportedPageSize = 10,
    /// <summary>The requested permissions cannot be represented without widening an existing ancestor entry.</summary>
    UnsupportedProtection = 11
}

/// <summary>Identifies a leaf page size supported by the freestanding x64 virtual-memory manager.</summary>
public enum KernelVirtualPageSize : ulong
{
    /// <summary>A standard 4 KiB page.</summary>
    Page4KiB = 4096UL,
    /// <summary>A 2 MiB large page.</summary>
    Page2MiB = 2097152UL,
    /// <summary>A 1 GiB large page.</summary>
    Page1GiB = 1073741824UL
}

/// <summary>Defines bit-combinable access and cache intent for a freestanding virtual mapping.</summary>
public enum KernelVirtualMemoryProtection : ulong
{
    /// <summary>No access permission is selected.</summary>
    None = 0UL,
    /// <summary>The mapping may be read.</summary>
    Read = 1UL << 0,
    /// <summary>The mapping may be written.</summary>
    Write = 1UL << 1,
    /// <summary>Instructions may execute from the mapping.</summary>
    Execute = 1UL << 2,
    /// <summary>User mode may access the mapping subject to its other permissions.</summary>
    User = 1UL << 3,
    /// <summary>The translation may remain global across address-space switches.</summary>
    Global = 1UL << 4,
    /// <summary>The mapping requests uncached device-memory semantics.</summary>
    Device = 1UL << 5,
    /// <summary>The mapping requests write-through caching.</summary>
    WriteThrough = 1UL << 6
}

/// <summary>Reports one exact freestanding virtual-to-physical translation.</summary>
public readonly struct KernelVirtualTranslation
{
    internal KernelVirtualTranslation(UInt64 virtualAddress, UInt64 physicalAddress, KernelVirtualPageSize pageSize, KernelVirtualMemoryProtection protection)
    {
        VirtualAddress = virtualAddress;
        PhysicalAddress = physicalAddress;
        PageSize = pageSize;
        Protection = protection;
    }

    /// <summary>Gets the queried virtual byte address.</summary>
    public UInt64 VirtualAddress { get; }
    /// <summary>Gets the translated physical byte address including the leaf offset.</summary>
    public UInt64 PhysicalAddress { get; }
    /// <summary>Gets the page size of the leaf that supplied the translation.</summary>
    public KernelVirtualPageSize PageSize { get; }
    /// <summary>Gets the decoded leaf permissions and cache intent.</summary>
    public KernelVirtualMemoryProtection Protection { get; }
}

/// <summary>Provides a snapshot of bootstrap x64 virtual-memory accounting.</summary>
public readonly struct KernelVirtualMemoryStatistics
{
    internal KernelVirtualMemoryStatistics(UInt64 protectedBootTables, UInt64 createdTables, UInt64 mapped4KiB, UInt64 mapped2MiB, UInt64 mapped1GiB)
    {
        ProtectedBootPageTables = protectedBootTables;
        CreatedPageTables = createdTables;
        MappedPages4KiB = mapped4KiB;
        MappedPages2MiB = mapped2MiB;
        MappedPages1GiB = mapped1GiB;
    }

    /// <summary>Gets the number of inherited active page-table pages protected from physical allocation.</summary>
    public UInt64 ProtectedBootPageTables { get; }
    /// <summary>Gets the number of new page-table pages allocated by this manager.</summary>
    public UInt64 CreatedPageTables { get; }
    /// <summary>Gets the number of 4 KiB leaf mappings installed through this manager.</summary>
    public UInt64 MappedPages4KiB { get; }
    /// <summary>Gets the number of 2 MiB leaf mappings installed through this manager.</summary>
    public UInt64 MappedPages2MiB { get; }
    /// <summary>Gets the number of 1 GiB leaf mappings installed through this manager.</summary>
    public UInt64 MappedPages1GiB { get; }
}

/// <summary>Provides the default no-heap x64 virtual-memory manager used by the editable kernel template.</summary>
/// <remarks>
/// Version 0.2.0 attaches to the active UEFI-created four-level address space, protects every discovered
/// page-table page from the physical allocator, and supports 4 KiB, 2 MiB, and 1 GiB leaf operations.
/// Until kernel address-space design is introduced, page-table physical pages must remain identity-accessible.
/// </remarks>
public static unsafe partial class KernelVirtualMemory
{
    private const UInt64 Present = 1UL << 0;
    private const UInt64 Writable = 1UL << 1;
    private const UInt64 User = 1UL << 2;
    private const UInt64 WriteThrough = 1UL << 3;
    private const UInt64 CacheDisable = 1UL << 4;
    private const UInt64 LargePage = 1UL << 7;
    private const UInt64 Global = 1UL << 8;
    private const UInt64 NoExecute = 1UL << 63;
    private const UInt64 AddressMask4KiB = 0x000FFFFFFFFFF000UL;
    private const UInt64 AddressMask2MiB = 0x000FFFFFFFE00000UL;
    private const UInt64 AddressMask1GiB = 0x000FFFFFC0000000UL;
    private const Int32 MaximumProtectedTables = 4096;

    private unsafe struct State
    {
        internal fixed UInt64 ProtectedTables[MaximumProtectedTables];
    }

#pragma warning disable CS0169 // Fixed-buffer member access is not counted as use of the containing freestanding state field.
    private static State _state;
#pragma warning restore CS0169
    private static Int32 _protectedTableCount;
    private static UInt64 _rootPhysicalAddress;
    private static UInt64 _createdPageTables;
    private static UInt64 _mapped4KiB;
    private static UInt64 _mapped2MiB;
    private static UInt64 _mapped1GiB;
    private static Boolean _executeDisableEnabled;
    private static Boolean _page1GiBSupported;
    private static Boolean _initialized;
    private static KernelVirtualMemoryStatus _lastStatus;

    /// <summary>Gets whether the bootstrap virtual-memory manager is initialized.</summary>
}
