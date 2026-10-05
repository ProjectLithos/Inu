using System;

namespace Inu.Kernel.Acpi;

public static unsafe partial class KernelAcpi
{
    public static UInt32 GetProcessorCount() => KernelAcpiMadtServices.GetProcessorCount();
    public static UInt32 GetIoApicCount() => KernelAcpiMadtServices.GetIoApicCount();
    public static UInt32 GetInterruptOverrideCount() => KernelAcpiMadtServices.GetInterruptOverrideCount();
    public static UInt32 GetNmiSourceCount() => KernelAcpiMadtServices.GetNmiSourceCount();
    public static UInt32 GetLocalNmiCount() => KernelAcpiMadtServices.GetLocalNmiCount();
    public static Boolean TryGetProcessor(UInt32 index, out AcpiProcessorInfo value) => KernelAcpiMadtServices.TryGetProcessor(index, out value);
    public static Boolean TryGetIoApic(UInt32 index, out AcpiIoApicInfo value) => KernelAcpiMadtServices.TryGetIoApic(index, out value);
    public static Boolean TryGetInterruptOverride(UInt32 index, out AcpiInterruptOverrideInfo value) => KernelAcpiMadtServices.TryGetInterruptOverride(index, out value);
    public static Boolean TryGetNmiSource(UInt32 index, out AcpiNmiSourceInfo value) => KernelAcpiMadtServices.TryGetNmiSource(index, out value);
    public static Boolean TryGetLocalNmi(UInt32 index, out AcpiLocalNmiInfo value) => KernelAcpiMadtServices.TryGetLocalNmi(index, out value);
    public static Boolean TryGetLocalApicAddress(out UInt64 value) => KernelAcpiMadtServices.TryGetLocalApicAddress(out value);
}
