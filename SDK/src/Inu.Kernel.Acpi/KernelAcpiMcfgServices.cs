using System;
namespace Inu.Kernel.Acpi;
public static unsafe class KernelAcpiMcfgServices
{
    private static Byte _registered; private static delegate*<UInt32> _count; private static delegate*<UInt32,AcpiPciEcamInfo*,Boolean> _get;
    public static Boolean Register(delegate*<UInt32> count,delegate*<UInt32,AcpiPciEcamInfo*,Boolean> get){if(_registered!=0||count==null||get==null)return false;_count=count;_get=get;_registered=1;return true;}
    public static Boolean IsRegistered()=>_registered!=0; public static UInt32 GetSegmentCount()=>_registered!=0?_count():0U;
    public static Boolean TryGetSegment(UInt32 i,out AcpiPciEcamInfo v){v=default;if(_registered==0)return false;AcpiPciEcamInfo local=default;if(!_get(i,&local))return false;v=local;return true;}
}
