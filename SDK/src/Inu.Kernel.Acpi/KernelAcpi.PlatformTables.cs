using System;

namespace Inu.Kernel.Acpi;

public static unsafe partial class KernelAcpi
{
    public static UInt32 GetPciEcamCount()=>KernelAcpiMcfgServices.GetSegmentCount();
    public static Boolean TryGetPciEcam(UInt32 index,out AcpiPciEcamInfo value)=>KernelAcpiMcfgServices.TryGetSegment(index,out value);
    public static Boolean TryGetHpet(out AcpiHpetInfo value)=>KernelAcpiHpetServices.TryGetDevice(out value);
}
