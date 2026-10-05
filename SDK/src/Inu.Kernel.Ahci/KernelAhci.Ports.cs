using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Ahci;
/// <summary>AHCI 1.x PCI driver with SATA discovery, DMA command lists, IDENTIFY, read/write and flush.</summary>
public static unsafe partial class KernelAhci
{
 static Boolean StopPort(UInt64 p){UInt32 cmd=R32(p,0x18);cmd&=~1U;W32(p,0x18,cmd);UInt32 s=SpinLimit;while(s-->0&&(R32(p,0x18)&(1U<<15))!=0){}if((R32(p,0x18)&(1U<<15))!=0)return false;W32(p,0x18,R32(p,0x18)&~(1U<<4));s=SpinLimit;while(s-->0&&(R32(p,0x18)&(1U<<14))!=0){}return (R32(p,0x18)&(1U<<14))==0;}
 static Boolean StartPort(UInt64 p){UInt32 s=SpinLimit;while(s-->0&&(R32(p,0x18)&(1U<<15))!=0){}if((R32(p,0x18)&(1U<<15))!=0)return false;W32(p,0x18,R32(p,0x18)|(1U<<4)|1U);return true;}
}
