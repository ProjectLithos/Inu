using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.Rtl8168;

/// <summary>Provides Realtek RTL8168/RTL8111-class PCIe gigabit Ethernet controllers using DMA descriptor rings.</summary>
public static unsafe partial class KernelRtl8168
{
    private static Boolean Transmit(KernelDeviceHandle device,Byte* frame,UInt32 length)
    {
        if(frame==null||length<14U||!TryRecord(device,out DeviceRecord* r)||r->Started==0||length>r->Mtu+14U)return false;UInt32 index=r->TxIndex;UInt64 descriptor=r->TxVirtual+(UInt64)index*16UL;UInt32 old=Read32(descriptor);if((old&DescOwn)!=0)return false;UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;Copy(frame,(Byte*)(nuint)(r->TxVirtual+descriptorBytes+(UInt64)index*BufferBytes),length);UInt32 options=DescOwn|DescFs|DescLs|length;if(index==DescriptorCount-1U)options|=DescEor;Write32(descriptor+4,0);Write32(descriptor,options);r->TxIndex=(index+1U)&(DescriptorCount-1U);Write8(r,TxPoll,0x40);return true;
    }
    private static Boolean SetReceiveEnabled(KernelDeviceHandle device,Boolean enabled){if(!TryRecord(device,out DeviceRecord* r))return false;r->ReceiveEnabled=enabled?(Byte)1:(Byte)0;Byte cmd=Read8(r,ChipCommand);return Write8(r,ChipCommand,enabled?(Byte)(cmd|CommandRxEnable):(Byte)(cmd&~CommandRxEnable));}
}
