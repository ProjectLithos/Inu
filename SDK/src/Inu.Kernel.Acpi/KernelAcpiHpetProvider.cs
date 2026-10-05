#if INU_COMPONENT_ACPI_HPET_TABLE
using System;
namespace Inu.Kernel.Acpi;
public static unsafe class KernelAcpiHpetProvider
{
    public static Boolean Register()=>KernelAcpiHpetServices.Register(&Get);
    private static Boolean Get(AcpiHpetInfo* v){if(v==null)return false;if(!KernelAcpi.TryGetTable(KernelAcpi.HpetSignature,out UInt64 a,out UInt32 l)||l<56U)return false;Byte* t=(Byte*)a;UInt64 b=Read64(t+44U);if(b==0UL)return false;*v=new AcpiHpetInfo(b,t[40],Read16(t+53U),t[52],Read32(t+36U));return true;}
    private static UInt16 Read16(Byte* p)=>(UInt16)(p[0]|((UInt16)p[1]<<8)); private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24)); private static UInt64 Read64(Byte* p)=>(UInt64)Read32(p)|((UInt64)Read32(p+4)<<32);
}
#endif
