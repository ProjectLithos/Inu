using System;

namespace Inu.Kernel.Rtl8168;

/// <summary>Provides allocation-free Realtek RTL8168/RTL8111 identification and ring helpers.</summary>
public static class Rtl8168Math
{
    public static Rtl8168ControllerFamily Identify(UInt16 vendorId,UInt16 deviceId)
    {
        if(vendorId!=0x10ECU)return Rtl8168ControllerFamily.Unknown;
        if(deviceId==0x8168U)return Rtl8168ControllerFamily.Rtl8168Or8111;
        if(deviceId==0x8169U)return Rtl8168ControllerFamily.Rtl8169;
        return Rtl8168ControllerFamily.Unknown;
    }
    public static Boolean IsSupported(UInt16 vendorId,UInt16 deviceId)=>Identify(vendorId,deviceId)!=Rtl8168ControllerFamily.Unknown;
    public static Boolean IsValidDescriptorCount(UInt32 count)=>count>=8U&&count<=4096U&&(count&(count-1U))==0U;
    public static UInt32 DescriptorBytes(UInt32 count)=>IsValidDescriptorCount(count)?count*16U:0U;
}
