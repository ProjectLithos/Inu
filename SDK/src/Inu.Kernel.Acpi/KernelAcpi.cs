using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Acpi;

/// <summary>Reports the result of ACPI root discovery and validation.</summary>
public enum KernelAcpiStatus
{
    /// <summary>ACPI discovery completed successfully.</summary>
    Success = 0,
    /// <summary>The UEFI boot hand-off did not provide an ACPI root pointer.</summary>
    RootPointerUnavailable = 1,
    /// <summary>The RSDP signature or checksum is invalid.</summary>
    InvalidRootPointer = 2,
    /// <summary>No usable RSDT or XSDT was advertised by the RSDP.</summary>
    RootTableUnavailable = 3,
    /// <summary>The selected RSDT or XSDT failed structural or checksum validation.</summary>
    InvalidRootTable = 4,
    /// <summary>The root table contains an unsupported number of entries.</summary>
    RootTableTooLarge = 5
}

/// <summary>Describes one processor advertised by the ACPI Multiple APIC Description Table.</summary>
public readonly struct AcpiProcessorInfo
{
    internal AcpiProcessorInfo(UInt32 apicId, UInt32 acpiUid, Boolean x2Apic, Boolean enabled)
    { ApicId = apicId; AcpiUid = acpiUid; IsX2Apic = x2Apic; IsEnabled = enabled; }
    /// <summary>Gets the local APIC or x2APIC identifier.</summary>
    public UInt32 ApicId { get; }
    /// <summary>Gets the ACPI processor UID.</summary>
    public UInt32 AcpiUid { get; }
    /// <summary>Gets whether this record came from an x2APIC entry.</summary>
    public Boolean IsX2Apic { get; }
    /// <summary>Gets whether firmware marks the processor enabled or online-capable.</summary>
    public Boolean IsEnabled { get; }
}

/// <summary>Describes one I/O APIC advertised by ACPI.</summary>
public readonly struct AcpiIoApicInfo
{
    internal AcpiIoApicInfo(Byte id, UInt32 address, UInt32 globalSystemInterruptBase)
    { Id = id; Address = address; GlobalSystemInterruptBase = globalSystemInterruptBase; }
    /// <summary>Gets the firmware I/O APIC identifier.</summary>
    public Byte Id { get; }
    /// <summary>Gets the physical MMIO base.</summary>
    public UInt32 Address { get; }
    /// <summary>Gets the first global system interrupt routed by this controller.</summary>
    public UInt32 GlobalSystemInterruptBase { get; }
}

/// <summary>Describes one ACPI interrupt source override.</summary>
public readonly struct AcpiInterruptOverrideInfo
{
    internal AcpiInterruptOverrideInfo(Byte bus, Byte source, UInt32 globalSystemInterrupt, UInt16 flags)
    { Bus = bus; Source = source; GlobalSystemInterrupt = globalSystemInterrupt; Flags = flags; }
    /// <summary>Gets the source bus; zero identifies the legacy ISA bus.</summary>
    public Byte Bus { get; }
    /// <summary>Gets the bus-relative interrupt source.</summary>
    public Byte Source { get; }
    /// <summary>Gets the replacement global system interrupt.</summary>
    public UInt32 GlobalSystemInterrupt { get; }
    /// <summary>Gets ACPI polarity and trigger-mode flags.</summary>
    public UInt16 Flags { get; }
}

/// <summary>Describes one MADT non-maskable interrupt source routed through a global system interrupt.</summary>
public readonly struct AcpiNmiSourceInfo
{
    internal AcpiNmiSourceInfo(UInt16 flags, UInt32 globalSystemInterrupt)
    { Flags=flags; GlobalSystemInterrupt=globalSystemInterrupt; }
    /// <summary>Gets ACPI polarity and trigger-mode flags.</summary>
    public UInt16 Flags { get; }
    /// <summary>Gets the global system interrupt that delivers the NMI.</summary>
    public UInt32 GlobalSystemInterrupt { get; }
}

/// <summary>Describes one MADT processor-local LAPIC/x2APIC NMI declaration.</summary>
public readonly struct AcpiLocalNmiInfo
{
    internal AcpiLocalNmiInfo(Boolean x2Apic, UInt32 processorUid, UInt16 flags, Byte lint)
    { IsX2Apic=x2Apic; ProcessorUid=processorUid; Flags=flags; Lint=lint; }
    /// <summary>Gets whether the record came from a Local x2APIC NMI structure.</summary>
    public Boolean IsX2Apic { get; }
    /// <summary>Gets the ACPI processor UID; 0xFF/0xFFFFFFFF denotes all processors for the corresponding MADT structure.</summary>
    public UInt32 ProcessorUid { get; }
    /// <summary>Gets ACPI polarity and trigger-mode flags.</summary>
    public UInt16 Flags { get; }
    /// <summary>Gets the Local APIC LINT input, zero or one.</summary>
    public Byte Lint { get; }
}

/// <summary>Describes one PCI Express ECAM segment from the ACPI MCFG table.</summary>
public readonly struct AcpiPciEcamInfo
{
    internal AcpiPciEcamInfo(UInt64 baseAddress, UInt16 segment, Byte startBus, Byte endBus)
    { BaseAddress = baseAddress; SegmentGroup = segment; StartBus = startBus; EndBus = endBus; }
    /// <summary>Gets the physical ECAM base.</summary>
    public UInt64 BaseAddress { get; }
    /// <summary>Gets the PCI segment group.</summary>
    public UInt16 SegmentGroup { get; }
    /// <summary>Gets the first bus covered by this allocation.</summary>
    public Byte StartBus { get; }
    /// <summary>Gets the last bus covered by this allocation.</summary>
    public Byte EndBus { get; }
}

/// <summary>Describes the ACPI HPET hardware block.</summary>
public readonly struct AcpiHpetInfo
{
    internal AcpiHpetInfo(UInt64 baseAddress, Byte addressSpace, UInt16 minimumTick, Byte sequence, UInt32 blockId)
    { BaseAddress = baseAddress; AddressSpace = addressSpace; MinimumTick = minimumTick; Sequence = sequence; EventTimerBlockId = blockId; }
    /// <summary>Gets the timer-register base address.</summary>
    public UInt64 BaseAddress { get; }
    /// <summary>Gets the ACPI generic-address-space identifier.</summary>
    public Byte AddressSpace { get; }
    /// <summary>Gets the minimum periodic tick in clock ticks.</summary>
    public UInt16 MinimumTick { get; }
    /// <summary>Gets the HPET sequence number.</summary>
    public Byte Sequence { get; }
    /// <summary>Gets the event-timer block identifier.</summary>
    public UInt32 EventTimerBlockId { get; }
}

/// <summary>Performs allocation-free ACPI discovery from the UEFI-supplied RSDP.</summary>
public static unsafe partial class KernelAcpi
{
    /// <summary>Little-endian signature for APIC/MADT.</summary>
    public const UInt32 ApicSignature = 0x43495041U;
    /// <summary>Little-endian signature for the Fixed ACPI Description Table.</summary>
    public const UInt32 FadtSignature = 0x50434146U;
    /// <summary>Little-endian signature for HPET.</summary>
    public const UInt32 HpetSignature = 0x54455048U;
    /// <summary>Little-endian signature for PCI Express MCFG.</summary>
    public const UInt32 McfgSignature = 0x4746434DU;
    private const UInt32 RsdtSignature = 0x54445352U;
    private const UInt32 XsdtSignature = 0x54445358U;
    private const UInt32 MaximumTableLength = 16U * 1024U * 1024U;
    private const UInt32 MaximumRootEntries = 4096U;
    private static Boolean _initialized;
    private static KernelAcpiStatus _status = KernelAcpiStatus.RootPointerUnavailable;
    private static UInt64 _rsdpAddress;
    private static UInt64 _rootAddress;
    private static Boolean _usesXsdt;
    private static UInt32 _rootEntryCount;
    private static Byte _revision;

    /// <summary>Gets whether ACPI discovery completed successfully.</summary>
    public static Boolean IsInitialized() => _initialized;
    /// <summary>Gets the most recent discovery status.</summary>
    public static KernelAcpiStatus GetLastStatus() => _status;
    /// <summary>Gets a freestanding-safe status name.</summary>
    public static String GetLastStatusName()
    {
        if (_status == KernelAcpiStatus.Success) return "Success";
        if (_status == KernelAcpiStatus.RootPointerUnavailable) return "RootPointerUnavailable";
        if (_status == KernelAcpiStatus.InvalidRootPointer) return "InvalidRootPointer";
        if (_status == KernelAcpiStatus.RootTableUnavailable) return "RootTableUnavailable";
        if (_status == KernelAcpiStatus.InvalidRootTable) return "InvalidRootTable";
        if (_status == KernelAcpiStatus.RootTableTooLarge) return "RootTableTooLarge";
        return "Unknown";
    }
    /// <summary>Gets the physical RSDP address captured from the UEFI configuration table.</summary>
    public static UInt64 GetRootPointerAddress() => _rsdpAddress;
    /// <summary>Gets the selected RSDT/XSDT physical address.</summary>
    public static UInt64 GetRootTableAddress() => _rootAddress;
    /// <summary>Gets whether the active root is the 64-bit XSDT.</summary>
    public static Boolean UsesXsdt() => _usesXsdt;
    /// <summary>Gets the ACPI revision advertised by the RSDP.</summary>
    public static Byte GetRevision() => _revision;
    /// <summary>Gets the number of table pointers in the selected root table.</summary>
    public static UInt32 GetRootTableCount() => _rootEntryCount;

}
