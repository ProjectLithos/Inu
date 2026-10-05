using System;
namespace Inu.Kernel.Nvme;
/// <summary>Pure NVMe register/queue calculations used by the runtime and independent tests.</summary>
public static class NvmeMath
{
 public static Boolean IsNvmeClass(UInt32 classCode)=>classCode==0x010802U;
 public static UInt32 DoorbellOffset(UInt16 queueId,Boolean completion,Byte stride)=>0x1000U+(((UInt32)queueId*2U+(completion?1U:0U))<<(2+stride));
 public static UInt16 SelectQueueEntries(UInt16 maximum,UInt16 requested){if(maximum==0||requested==0)return 0;UInt16 value=maximum<requested?maximum:requested;return value;}
 public static UInt32 NamespaceBlockSize(Byte lbads)=>lbads<9U||lbads>31U?0U:1U<<lbads;
 public static UInt64 PagesForBytes(UInt64 bytes)=>bytes==0?0:(bytes+4095UL)>>12;
}
