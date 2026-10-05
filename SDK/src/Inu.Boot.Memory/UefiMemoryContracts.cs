using Inu.Memory;
using Inu.Primitives;

namespace Inu.Boot.Memory;

/// <summary>Identifies a UEFI memory descriptor type without exposing firmware-specific policy to allocators.</summary>
/// <nova.when>Use in the UEFI adapter that captures the firmware memory map.</nova.when>
/// <nova.depends>UEFI specification memory-type values</nova.depends>
public enum UefiMemoryType : uint
{
    /// <summary>Reserved firmware memory.</summary>
    Reserved = 0,
    /// <summary>Loader executable code.</summary>
    LoaderCode = 1,
    /// <summary>Loader writable data.</summary>
    LoaderData = 2,
    /// <summary>Boot-services executable code.</summary>
    BootServicesCode = 3,
    /// <summary>Boot-services writable data.</summary>
    BootServicesData = 4,
    /// <summary>Runtime-services executable code.</summary>
    RuntimeServicesCode = 5,
    /// <summary>Runtime-services writable data.</summary>
    RuntimeServicesData = 6,
    /// <summary>Conventional allocatable memory.</summary>
    ConventionalMemory = 7,
    /// <summary>Known unusable memory.</summary>
    UnusableMemory = 8,
    /// <summary>ACPI reclaimable memory.</summary>
    AcpiReclaimMemory = 9,
    /// <summary>ACPI non-volatile storage.</summary>
    AcpiMemoryNvs = 10,
    /// <summary>Memory-mapped device I/O.</summary>
    MemoryMappedIo = 11,
    /// <summary>Memory-mapped I/O port space.</summary>
    MemoryMappedIoPortSpace = 12,
    /// <summary>Processor abstraction-layer code.</summary>
    PalCode = 13,
    /// <summary>Byte-addressable persistent memory.</summary>
    PersistentMemory = 14,
    /// <summary>Memory that has not yet been accepted by the platform.</summary>
    UnacceptedMemory = 15
}

/// <summary>Defines UEFI cache, protection, persistence, and runtime attributes.</summary>
/// <nova.when>Use when retaining the exact semantics of the final firmware map.</nova.when>
/// <nova.depends>UEFI EFI_MEMORY_ATTRIBUTE values</nova.depends>
[Flags]
public enum UefiMemoryAttributes : ulong
{
    /// <summary>No firmware attribute is present.</summary>
    None = 0,
    /// <summary>Uncacheable memory.</summary>
    Uncacheable = 0x0000000000000001,
    /// <summary>Write-combining memory.</summary>
    WriteCombining = 0x0000000000000002,
    /// <summary>Write-through memory.</summary>
    WriteThrough = 0x0000000000000004,
    /// <summary>Write-back memory.</summary>
    WriteBack = 0x0000000000000008,
    /// <summary>Uncached-exported memory.</summary>
    UncachedExported = 0x0000000000000010,
    /// <summary>Write-protected memory.</summary>
    WriteProtected = 0x0000000000001000,
    /// <summary>Read-protected memory.</summary>
    ReadProtected = 0x0000000000002000,
    /// <summary>Execute-protected memory.</summary>
    ExecuteProtected = 0x0000000000004000,
    /// <summary>Non-volatile memory.</summary>
    NonVolatile = 0x0000000000008000,
    /// <summary>More-reliable memory.</summary>
    MoreReliable = 0x0000000000010000,
    /// <summary>Read-only memory.</summary>
    ReadOnly = 0x0000000000020000,
    /// <summary>Specific-purpose memory.</summary>
    SpecificPurpose = 0x0000000000040000,
    /// <summary>CPU cryptographic-protection capable memory.</summary>
    CpuCrypto = 0x0000000000080000,
    /// <summary>Memory required by runtime services.</summary>
    Runtime = 0x8000000000000000
}

/// <summary>Retains one typed descriptor copied from the final UEFI memory map.</summary>
/// <nova.when>Use only inside the final-map workspace and immutable snapshot.</nova.when>
/// <nova.depends>UefiMemoryType and UefiMemoryAttributes</nova.depends>
public readonly record struct UefiMemoryDescriptor(
    UefiMemoryType Type,
    PhysicalAddress PhysicalStart,
    ulong VirtualStart,
    ulong PageCount,
    UefiMemoryAttributes Attributes);

/// <summary>Reports the result of a platform GetMemoryMap adapter call.</summary>
/// <nova.when>Use to distinguish success, capacity exhaustion, and firmware errors.</nova.when>
/// <nova.depends>IUefiMemoryMapProvider</nova.depends>
public enum UefiMemoryMapStatus
{
    /// <summary>The destination contains a complete current map.</summary>
    Success = 0,
    /// <summary>The preallocated destination is too small.</summary>
    BufferTooSmall = 1,
    /// <summary>Firmware returned an invalid parameter.</summary>
    InvalidParameter = 2,
    /// <summary>Firmware or the platform adapter failed.</summary>
    FirmwareError = 3
}

/// <summary>Reports the result of ExitBootServices.</summary>
/// <nova.when>Use to retry only when the map key became stale.</nova.when>
/// <nova.depends>IUefiMemoryMapProvider</nova.depends>
public enum UefiExitBootServicesStatus
{
    /// <summary>Boot services were exited successfully.</summary>
    Success = 0,
    /// <summary>The map key became stale and the final map must be obtained again.</summary>
    InvalidMapKey = 1,
    /// <summary>Firmware rejected the operation for another reason.</summary>
    FirmwareError = 2
}

/// <summary>Abstracts the architecture-specific UEFI GetMemoryMap and ExitBootServices calls.</summary>
/// <nova.when>Implement in a native UEFI adapter or a deterministic test provider.</nova.when>
/// <nova.depends>Caller-preallocated descriptor storage</nova.depends>
public interface IUefiMemoryMapProvider
{
    /// <summary>Copies the current UEFI map into caller-owned storage.</summary>
    /// <nova.when>Call immediately before ExitBootServices; do not allocate after success.</nova.when>
    /// <nova.depends>UEFI GetMemoryMap</nova.depends>
    /// <returns>The firmware map acquisition status.</returns>
    /// <example><code>UefiMemoryMapStatus status = provider.GetMemoryMap(buffer, out int count, out ulong key, out uint version);</code></example>
    UefiMemoryMapStatus GetMemoryMap(UefiMemoryDescriptor[] destination, out int count, out ulong mapKey, out uint descriptorVersion);

    /// <summary>Exits boot services using the key from the immediately preceding successful map call.</summary>
    /// <nova.when>Call with no allocation or firmware operation between GetMemoryMap and this method.</nova.when>
    /// <nova.depends>UEFI ExitBootServices</nova.depends>
    /// <returns>The exit status, including stale-key retry information.</returns>
    /// <example><code>UefiExitBootServicesStatus status = provider.ExitBootServices(imageHandle, mapKey);</code></example>
    UefiExitBootServicesStatus ExitBootServices(ulong imageHandle, ulong mapKey);
}

/// <summary>Translates UEFI descriptors into architecture-independent Inu descriptors.</summary>
/// <nova.when>Used by FinalUefiMemoryMapSnapshot when a normaliser reads the retained map.</nova.when>
/// <nova.depends>Inu.Memory.Contracts</nova.depends>
public static class UefiMemoryDescriptorMapper
{
    /// <summary>Attempts to translate one UEFI descriptor while rejecting overflow and misalignment.</summary>
    /// <nova.when>Use for every retained firmware descriptor before normalisation.</nova.when>
    /// <nova.depends>MemoryDescriptor.TryCreate</nova.depends>
    /// <returns><see langword="true"/> when the UEFI descriptor is valid and recognised.</returns>
    /// <example><code>bool mapped = UefiMemoryDescriptorMapper.TryMap(uefi, out MemoryDescriptor descriptor);</code></example>
    public static bool TryMap(UefiMemoryDescriptor source, out MemoryDescriptor descriptor)
    {
        MemoryType type;
        MemoryRuntimeStatus runtime = MemoryRuntimeStatus.NotRuntime;
        MemoryAvailability availability;
        switch (source.Type)
        {
            case UefiMemoryType.ConventionalMemory:
                type = MemoryType.UsableConventional;
                availability = MemoryAvailability.AvailableAfterExitBootServices;
                break;
            case UefiMemoryType.LoaderCode:
            case UefiMemoryType.LoaderData:
                type = MemoryType.LoaderKernelImage;
                availability = MemoryAvailability.Unavailable;
                break;
            case UefiMemoryType.BootServicesCode:
            case UefiMemoryType.BootServicesData:
                type = MemoryType.BootServices;
                availability = MemoryAvailability.AvailableAfterExitBootServices;
                break;
            case UefiMemoryType.RuntimeServicesCode:
                type = MemoryType.RuntimeServices;
                runtime = MemoryRuntimeStatus.RuntimeCode;
                availability = MemoryAvailability.RuntimeOwned;
                break;
            case UefiMemoryType.RuntimeServicesData:
                type = MemoryType.RuntimeServices;
                runtime = MemoryRuntimeStatus.RuntimeData;
                availability = MemoryAvailability.RuntimeOwned;
                break;
            case UefiMemoryType.AcpiReclaimMemory:
                type = MemoryType.AcpiReclaimable;
                availability = MemoryAvailability.ReclaimableAfterAcpiInitialization;
                break;
            case UefiMemoryType.AcpiMemoryNvs:
                type = MemoryType.AcpiNvs;
                availability = MemoryAvailability.PermanentlyReserved;
                break;
            case UefiMemoryType.MemoryMappedIo:
            case UefiMemoryType.MemoryMappedIoPortSpace:
                type = MemoryType.MemoryMappedIo;
                availability = MemoryAvailability.PermanentlyReserved;
                break;
            case UefiMemoryType.UnusableMemory:
            case UefiMemoryType.UnacceptedMemory:
                type = MemoryType.BadMemory;
                availability = MemoryAvailability.Defective;
                break;
            case UefiMemoryType.PersistentMemory:
                type = MemoryType.PersistentMemory;
                availability = MemoryAvailability.Unavailable;
                break;
            default:
                type = MemoryType.FirmwareReserved;
                availability = MemoryAvailability.PermanentlyReserved;
                break;
        }

        if ((source.Attributes & UefiMemoryAttributes.Runtime) != 0 && runtime == MemoryRuntimeStatus.NotRuntime)
        {
            runtime = MemoryRuntimeStatus.RuntimeData;
            availability = MemoryAvailability.RuntimeOwned;
            if (type != MemoryType.MemoryMappedIo) type = MemoryType.RuntimeServices;
        }

        MemoryCacheAttributes attributes = TranslateAttributes(source.Attributes);
        return MemoryDescriptor.TryCreate(source.PhysicalStart, source.PageCount, type, attributes, runtime, availability, false, 0, out descriptor);
    }

    private static MemoryCacheAttributes TranslateAttributes(UefiMemoryAttributes source)
    {
        MemoryCacheAttributes result = MemoryCacheAttributes.None;
        if ((source & UefiMemoryAttributes.Uncacheable) != 0) result |= MemoryCacheAttributes.Uncacheable;
        if ((source & UefiMemoryAttributes.WriteCombining) != 0) result |= MemoryCacheAttributes.WriteCombining;
        if ((source & UefiMemoryAttributes.WriteThrough) != 0) result |= MemoryCacheAttributes.WriteThrough;
        if ((source & UefiMemoryAttributes.WriteBack) != 0) result |= MemoryCacheAttributes.WriteBack;
        if ((source & UefiMemoryAttributes.UncachedExported) != 0) result |= MemoryCacheAttributes.UncachedExported;
        if ((source & UefiMemoryAttributes.WriteProtected) != 0) result |= MemoryCacheAttributes.WriteProtected;
        if ((source & UefiMemoryAttributes.ReadProtected) != 0) result |= MemoryCacheAttributes.ReadProtected;
        if ((source & UefiMemoryAttributes.ExecuteProtected) != 0) result |= MemoryCacheAttributes.ExecuteProtected;
        if ((source & UefiMemoryAttributes.NonVolatile) != 0) result |= MemoryCacheAttributes.NonVolatile;
        if ((source & UefiMemoryAttributes.MoreReliable) != 0) result |= MemoryCacheAttributes.MoreReliable;
        if ((source & UefiMemoryAttributes.ReadOnly) != 0) result |= MemoryCacheAttributes.ReadOnly;
        if ((source & UefiMemoryAttributes.SpecificPurpose) != 0) result |= MemoryCacheAttributes.SpecificPurpose;
        if ((source & UefiMemoryAttributes.CpuCrypto) != 0) result |= MemoryCacheAttributes.CpuCrypto;
        return result;
    }
}
