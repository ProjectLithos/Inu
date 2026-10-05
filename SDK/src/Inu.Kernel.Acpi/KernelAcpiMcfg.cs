using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Provides the MCFG PCI Express platform-driver view.</summary>
public static class KernelAcpiMcfg
{
    /// <summary>Gets the number of validated ECAM allocations.</summary>
    public static UInt32 GetSegmentCount() => KernelAcpi.GetPciEcamCount();
    /// <summary>Gets one ECAM allocation.</summary>
    public static Boolean TryGetSegment(UInt32 index, out AcpiPciEcamInfo value) => KernelAcpi.TryGetPciEcam(index, out value);
}
