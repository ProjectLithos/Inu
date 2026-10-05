using System;
namespace Inu.Kernel.Acpi;
public static unsafe class KernelAcpiHpetServices
{
    private static Byte _registered; private static delegate*<AcpiHpetInfo*,Boolean> _get;
    public static Boolean Register(delegate*<AcpiHpetInfo*,Boolean> get){if(_registered!=0||get==null)return false;_get=get;_registered=1;return true;}
    public static Boolean IsRegistered()=>_registered!=0;
    public static Boolean TryGetDevice(out AcpiHpetInfo v){v=default;if(_registered==0)return false;AcpiHpetInfo local=default;if(!_get(&local))return false;v=local;return true;}
}
