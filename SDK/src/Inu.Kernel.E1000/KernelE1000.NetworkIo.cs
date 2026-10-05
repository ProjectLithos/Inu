using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.E1000;

/// <summary>Provides Intel E1000/E1000e PCI gigabit Ethernet controllers using DMA descriptor rings.</summary>
public static unsafe partial class KernelE1000
{
    private static Boolean Transmit(KernelDeviceHandle device,Byte* frame,UInt32 length)
    {
        if(frame==null||length<14U||!TryRecord(device,out DeviceRecord* r)||r->Started==0||length>r->Mtu+14U)return false;UInt32 index=r->TxIndex;UInt64 descriptor=r->TxVirtual+(UInt64)index*16UL;if((Read8(descriptor+12)&TxDone)==0)return false;UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;Copy(frame,(Byte*)(nuint)(r->TxVirtual+descriptorBytes+(UInt64)index*BufferBytes),length);Write16(descriptor+8,(UInt16)length);Write8(descriptor+11,TxEopIfcsRs);Write8(descriptor+12,0);r->TxIndex=(index+1U)&(DescriptorCount-1U);Write32(r,Tdt,r->TxIndex);for(UInt32 spin=0;spin<1000000U;spin++)if((Read8(descriptor+12)&TxDone)!=0)return true;return false;
    }
    private static Boolean SetReceiveEnabled(KernelDeviceHandle device,Boolean enabled){if(!TryRecord(device,out DeviceRecord* r))return false;r->ReceiveEnabled=enabled?(Byte)1:(Byte)0;UInt32 value=Read32(r,Rctl);return Write32(r,Rctl,enabled?value|RctlEnable:value&~RctlEnable);}
}
