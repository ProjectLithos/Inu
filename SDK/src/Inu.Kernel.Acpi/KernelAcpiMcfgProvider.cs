#if INU_COMPONENT_ACPI_MCFG
using System;
namespace Inu.Kernel.Acpi;
public static unsafe class KernelAcpiMcfgProvider
{
    public static Boolean Register()=>KernelAcpiMcfgServices.Register(&GetCount,&Get);
    private static UInt32 GetCount(){if(!KernelAcpi.TryGetTable(KernelAcpi.McfgSignature,out UInt64 a,out UInt32 l)||l<44U)return 0U;return(l-44U)/16U;}
    private static Boolean Get(UInt32 i,AcpiPciEcamInfo* v){if(v==null)return false;if(!KernelAcpi.TryGetTable(KernelAcpi.McfgSignature,out UInt64 a,out UInt32 l)||l<44U)return false;UInt32 c=(l-44U)/16U;if(i>=c)return false;Byte* e=(Byte*)a+44U+i*16U;Byte s=e[10],n=e[11];if(s>n)return false;*v=new AcpiPciEcamInfo(Read64(e),Read16(e+8U),s,n);return true;}
    private static UInt16 Read16(Byte* p)=>(UInt16)(p[0]|((UInt16)p[1]<<8)); private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24)); private static UInt64 Read64(Byte* p)=>(UInt64)Read32(p)|((UInt64)Read32(p+4)<<32);
}
#endif
