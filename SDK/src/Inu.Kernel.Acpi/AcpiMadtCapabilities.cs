using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Summarizes platform topology discovered from MADT.</summary>
public readonly struct AcpiMadtCapabilities
{
    internal AcpiMadtCapabilities(Boolean initialized, UInt32 processors, UInt32 ioApics, UInt32 overrides, UInt64 localApic)
    { Initialized = initialized; ProcessorCount = processors; IoApicCount = ioApics; InterruptOverrideCount = overrides; LocalApicAddress = localApic; }
    /// <summary>Gets whether a valid MADT was discovered.</summary>
    public Boolean Initialized { get; }
    /// <summary>Gets the enabled processor count.</summary>
    public UInt32 ProcessorCount { get; }
    /// <summary>Gets the I/O APIC count.</summary>
    public UInt32 IoApicCount { get; }
    /// <summary>Gets the interrupt-source override count.</summary>
    public UInt32 InterruptOverrideCount { get; }
    /// <summary>Gets the local APIC physical base.</summary>
    public UInt64 LocalApicAddress { get; }
}
