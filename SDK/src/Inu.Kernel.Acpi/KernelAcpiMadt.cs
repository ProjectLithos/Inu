using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Provides the MADT platform-driver view over ACPI topology.</summary>
public static class KernelAcpiMadt
{
    /// <summary>Gets current MADT capabilities.</summary>
    public static AcpiMadtCapabilities GetCapabilities()
    {
        Boolean present = KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out _, out UInt32 length) && length >= 44U;
        KernelAcpi.TryGetLocalApicAddress(out UInt64 lapic);
        return new AcpiMadtCapabilities(present, KernelAcpi.GetProcessorCount(), KernelAcpi.GetIoApicCount(), KernelAcpi.GetInterruptOverrideCount(), lapic);
    }
    /// <summary>Gets one enabled processor.</summary>
    public static Boolean TryGetProcessor(UInt32 index, out AcpiProcessorInfo value) => KernelAcpi.TryGetProcessor(index, out value);
    /// <summary>Gets one I/O APIC.</summary>
    public static Boolean TryGetIoApic(UInt32 index, out AcpiIoApicInfo value) => KernelAcpi.TryGetIoApic(index, out value);
    /// <summary>Gets one interrupt-source override.</summary>
    public static Boolean TryGetInterruptOverride(UInt32 index, out AcpiInterruptOverrideInfo value) => KernelAcpi.TryGetInterruptOverride(index, out value);
}
