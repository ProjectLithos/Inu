using System;

namespace Inu.Kernel.Acpi;

/// <summary>Registration and dispatch boundary for an optional ACPI embedded-controller provider.</summary>
public static unsafe class KernelAcpiEcServices
{
    private static Byte _registered, _ready;
    private static delegate*<Boolean> _initialize;
    private static delegate*<AcpiEcInfo*,Boolean> _getInfo;
    private static delegate*<Byte,Byte*,Boolean> _read;
    private static delegate*<Byte,Byte,Boolean> _write;

    public static Boolean Register(delegate*<Boolean> initialize, delegate*<AcpiEcInfo*,Boolean> getInfo, delegate*<Byte,Byte*,Boolean> read, delegate*<Byte,Byte,Boolean> write)
    { if(_registered!=0||initialize==null||getInfo==null||read==null||write==null)return false;_initialize=initialize;_getInfo=getInfo;_read=read;_write=write;_registered=1;return true; }
    public static Boolean Initialize(){if(_ready!=0)return true;if(_registered==0||!_initialize())return false;_ready=1;return true;}
    public static Boolean IsAvailable()=>_ready!=0;
    public static Boolean TryGetInfo(out AcpiEcInfo info){info=default;if(_ready==0)return false;AcpiEcInfo local=default;if(!_getInfo(&local))return false;info=local;return true;}
    public static Boolean TryRead(Byte address,out Byte value){value=0;if(_ready==0)return false;Byte local=0;if(!_read(address,&local))return false;value=local;return true;}
    public static Boolean TryWrite(Byte address,Byte value)=>_ready!=0&&_write(address,value);
}
