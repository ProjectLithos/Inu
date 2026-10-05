using System;

namespace Inu.Kernel.Acpi;

/// <summary>Dispatch boundary between the ACPI root-table registry and an optional MADT topology consumer.</summary>
public static unsafe class KernelAcpiMadtServices
{
    private static Byte _registered;
    private static delegate*<UInt32> _processorCount, _ioApicCount, _overrideCount, _nmiSourceCount, _localNmiCount;
    private static delegate*<UInt32, AcpiProcessorInfo*, Boolean> _processor;
    private static delegate*<UInt32, AcpiIoApicInfo*, Boolean> _ioApic;
    private static delegate*<UInt32, AcpiInterruptOverrideInfo*, Boolean> _interruptOverride;
    private static delegate*<UInt32, AcpiNmiSourceInfo*, Boolean> _nmiSource;
    private static delegate*<UInt32, AcpiLocalNmiInfo*, Boolean> _localNmi;
    private static delegate*<UInt64*, Boolean> _localApicAddress;
    public static Boolean Register(delegate*<UInt32> processorCount, delegate*<UInt32> ioApicCount, delegate*<UInt32> overrideCount, delegate*<UInt32> nmiSourceCount, delegate*<UInt32> localNmiCount, delegate*<UInt32, AcpiProcessorInfo*, Boolean> processor, delegate*<UInt32, AcpiIoApicInfo*, Boolean> ioApic, delegate*<UInt32, AcpiInterruptOverrideInfo*, Boolean> interruptOverride, delegate*<UInt32, AcpiNmiSourceInfo*, Boolean> nmiSource, delegate*<UInt32, AcpiLocalNmiInfo*, Boolean> localNmi, delegate*<UInt64*, Boolean> localApicAddress)
    {
        if (_registered != 0 || processorCount == null || ioApicCount == null || overrideCount == null || nmiSourceCount == null || localNmiCount == null || processor == null || ioApic == null || interruptOverride == null || nmiSource == null || localNmi == null || localApicAddress == null) return false;
        _processorCount=processorCount; _ioApicCount=ioApicCount; _overrideCount=overrideCount; _nmiSourceCount=nmiSourceCount; _localNmiCount=localNmiCount; _processor=processor; _ioApic=ioApic; _interruptOverride=interruptOverride; _nmiSource=nmiSource; _localNmi=localNmi; _localApicAddress=localApicAddress; _registered=1; return true;
    }
    public static Boolean IsRegistered()=>_registered!=0;
    public static UInt32 GetProcessorCount()=>_registered!=0?_processorCount():0U;
    public static UInt32 GetIoApicCount()=>_registered!=0?_ioApicCount():0U;
    public static UInt32 GetInterruptOverrideCount()=>_registered!=0?_overrideCount():0U;
    public static UInt32 GetNmiSourceCount()=>_registered!=0?_nmiSourceCount():0U;
    public static UInt32 GetLocalNmiCount()=>_registered!=0?_localNmiCount():0U;
    public static Boolean TryGetProcessor(UInt32 i,out AcpiProcessorInfo v){v=default;if(_registered==0)return false;AcpiProcessorInfo local=default;if(!_processor(i,&local))return false;v=local;return true;}
    public static Boolean TryGetIoApic(UInt32 i,out AcpiIoApicInfo v){v=default;if(_registered==0)return false;AcpiIoApicInfo local=default;if(!_ioApic(i,&local))return false;v=local;return true;}
    public static Boolean TryGetInterruptOverride(UInt32 i,out AcpiInterruptOverrideInfo v){v=default;if(_registered==0)return false;AcpiInterruptOverrideInfo local=default;if(!_interruptOverride(i,&local))return false;v=local;return true;}
    public static Boolean TryGetNmiSource(UInt32 i,out AcpiNmiSourceInfo v){v=default;if(_registered==0)return false;AcpiNmiSourceInfo local=default;if(!_nmiSource(i,&local))return false;v=local;return true;}
    public static Boolean TryGetLocalNmi(UInt32 i,out AcpiLocalNmiInfo v){v=default;if(_registered==0)return false;AcpiLocalNmiInfo local=default;if(!_localNmi(i,&local))return false;v=local;return true;}
    public static Boolean TryGetLocalApicAddress(out UInt64 v){v=0UL;if(_registered==0)return false;UInt64 local=0UL;if(!_localApicAddress(&local))return false;v=local;return true;}
}
