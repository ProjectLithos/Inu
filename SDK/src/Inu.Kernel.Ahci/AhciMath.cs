using System;
namespace Inu.Kernel.Ahci;
public static class AhciMath
{
 public static Boolean IsAhciClass(UInt32 classCode)=>classCode==0x010601U;
 public static AhciPortType DecodeSignature(UInt32 signature)=>signature==0x00000101U?AhciPortType.Sata:signature==0xEB140101U?AhciPortType.Satapi:signature==0xC33C0101U?AhciPortType.Enclosure:signature==0x96690101U?AhciPortType.PortMultiplier:AhciPortType.None;
 public static Boolean IsDevicePresent(UInt32 ssts)=> (ssts&15U)==3U && ((ssts>>8)&15U)==1U;
 public static UInt64 DecodeLba48(UInt16 w100,UInt16 w101,UInt16 w102,UInt16 w103)=>(UInt64)w100|((UInt64)w101<<16)|((UInt64)w102<<32)|((UInt64)w103<<48);
 public static UInt32 DecodeLogicalSectorSize(UInt16 word106,UInt16 word117,UInt16 word118){if((word106&0xC000U)!=0x4000U||(word106&0x1000U)==0)return 512U;UInt32 words=(UInt32)word117|((UInt32)word118<<16);return words<256U?512U:words*2U;}
}
