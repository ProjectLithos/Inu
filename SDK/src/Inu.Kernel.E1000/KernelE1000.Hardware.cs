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
    private static Boolean EnablePci(PciLocation location){if(!KernelPci.TryRead16(location,0x04,out UInt16 command))return false;return KernelPci.TryWrite16(location,0x04,(UInt16)(command|0x0006U));}
    private static Boolean Reset(DeviceRecord* r){Write32(r,Imc,0xFFFFFFFFU);Write32(r,Ctrl,Read32(r,Ctrl)|CtrlReset);for(UInt32 spin=0;spin<1000000U;spin++)if((Read32(r,Ctrl)&CtrlReset)==0U){Read32(r,Icr);return true;}return false;}
    private static Boolean AllocateRings(DeviceRecord* r)
    {
        UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL,rxBytes=descriptorBytes+(UInt64)DescriptorCount*BufferBytes,txBytes=descriptorBytes+(UInt64)DescriptorCount*BufferBytes;if(!AllocateDma(rxBytes,out r->RxToken,out r->RxPages,out r->RxPhysical,out r->RxVirtual))return false;if(!AllocateDma(txBytes,out r->TxToken,out r->TxPages,out r->TxPhysical,out r->TxVirtual)){ReleaseDma(r->RxToken,r->RxPhysical,r->RxPages);r->RxToken=0;return false;}
        for(UInt32 i=0;i<DescriptorCount;i++){UInt64 rxDescriptor=r->RxVirtual+(UInt64)i*16UL;Write64(rxDescriptor,r->RxPhysical+descriptorBytes+(UInt64)i*BufferBytes);Write8(rxDescriptor+12,0);UInt64 txDescriptor=r->TxVirtual+(UInt64)i*16UL;Write64(txDescriptor,r->TxPhysical+descriptorBytes+(UInt64)i*BufferBytes);Write8(txDescriptor+12,TxDone);}return true;
    }
    private static Boolean InitializeReceive(DeviceRecord* r){UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;Write32(r,Rdbal,(UInt32)r->RxPhysical);Write32(r,Rdbah,(UInt32)(r->RxPhysical>>32));Write32(r,Rdlen,(UInt32)descriptorBytes);Write32(r,Rdh,0);Write32(r,Rdt,DescriptorCount-1U);r->RxIndex=0;Write32(r,Rctl,RctlEnable|RctlBroadcast|RctlStripCrc);return true;}
    private static Boolean InitializeTransmit(DeviceRecord* r){UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;Write32(r,Tdbal,(UInt32)r->TxPhysical);Write32(r,Tdbah,(UInt32)(r->TxPhysical>>32));Write32(r,Tdlen,(UInt32)descriptorBytes);Write32(r,Tdh,0);Write32(r,Tdt,0);r->TxIndex=0;Write32(r,Tipg,0x0060200AU);Write32(r,Tctl,TctlEnable|TctlPadShort|(15U<<4)|(64U<<12));return true;}
    private static Boolean ServiceRecord(DeviceRecord* r)
    {
        Read32(r,Icr);UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;for(UInt32 guard=0;guard<DescriptorCount;guard++){UInt64 descriptor=r->RxVirtual+(UInt64)r->RxIndex*16UL;Byte status=Read8(descriptor+12);if((status&RxDone)==0)break;UInt16 length=Read16(descriptor+8);Byte errors=Read8(descriptor+13);if(errors==0&&length>=14U&&length<=BufferBytes&&r->ReceiveEnabled!=0&&r->NetworkHandle!=0U)KernelNetworking.QueueReceivedFrame(new KernelNetworkInterfaceHandle(r->NetworkHandle),(Byte*)(nuint)(r->RxVirtual+descriptorBytes+(UInt64)r->RxIndex*BufferBytes),length,out _);Write8(descriptor+12,0);UInt32 completed=r->RxIndex;r->RxIndex=(r->RxIndex+1U)&(DescriptorCount-1U);Write32(r,Rdt,completed);}return true;
    }
}
