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
    private static Boolean EnablePci(PciLocation location){if(!KernelPci.TryRead16(location,0x04,out UInt16 command))return false;return KernelPci.TryWrite16(location,0x04,(UInt16)(command|0x0006U));}
    private static Boolean Reset(DeviceRecord* r){Write16(r,InterruptMask,0);Write8(r,ChipCommand,CommandReset);for(UInt32 spin=0;spin<1000000U;spin++)if((Read8(r,ChipCommand)&CommandReset)==0)return true;return false;}
    private static Boolean ReadMac(DeviceRecord* r,out KernelMacAddress mac){Byte a=Read8(r,Id0),b=Read8(r,Id0+1),c=Read8(r,Id0+2),d=Read8(r,Id0+3),e=Read8(r,Id0+4),f=Read8(r,Id0+5);mac=new(a,b,c,d,e,f);if(mac.IsZero)return false;r->MacA=a;r->MacB=b;r->MacC=c;r->MacD=d;r->MacE=e;r->MacF=f;return true;}
    private static Boolean AllocateRings(DeviceRecord* r)
    {
        UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL,rxBytes=descriptorBytes+(UInt64)DescriptorCount*BufferBytes,txBytes=descriptorBytes+(UInt64)DescriptorCount*BufferBytes;if(!AllocateDma(rxBytes,out r->RxToken,out r->RxPages,out r->RxPhysical,out r->RxVirtual))return false;if(!AllocateDma(txBytes,out r->TxToken,out r->TxPages,out r->TxPhysical,out r->TxVirtual)){ReleaseDma(r->RxToken,r->RxPhysical,r->RxPages);r->RxToken=0;return false;}
        for(UInt32 i=0;i<DescriptorCount;i++){UInt32 rxOptions=DescOwn|BufferBytes;if(i==DescriptorCount-1U)rxOptions|=DescEor;UInt64 rxDescriptor=r->RxVirtual+(UInt64)i*16UL;Write32(rxDescriptor,rxOptions);Write32(rxDescriptor+4,0);UInt64 rxBuffer=r->RxPhysical+descriptorBytes+(UInt64)i*BufferBytes;Write32(rxDescriptor+8,(UInt32)rxBuffer);Write32(rxDescriptor+12,(UInt32)(rxBuffer>>32));UInt64 txDescriptor=r->TxVirtual+(UInt64)i*16UL;Write32(txDescriptor,i==DescriptorCount-1U?DescEor:0U);UInt64 txBuffer=r->TxPhysical+descriptorBytes+(UInt64)i*BufferBytes;Write32(txDescriptor+8,(UInt32)txBuffer);Write32(txDescriptor+12,(UInt32)(txBuffer>>32));}return true;
    }
    private static Boolean InitializeHardware(DeviceRecord* r)
    {
        Write8(r,Cfg9346,0xC0);Write16(r,InterruptMask,0);Write16(r,InterruptStatus,0xFFFF);Write16(r,RxMaxSize,(UInt16)(DefaultMtu+18U));Write32(r,TxDescLow,(UInt32)r->TxPhysical);Write32(r,TxDescHigh,(UInt32)(r->TxPhysical>>32));Write32(r,RxDescLow,(UInt32)r->RxPhysical);Write32(r,RxDescHigh,(UInt32)(r->RxPhysical>>32));Write32(r,RxConfig,0x0000E70FU);Write32(r,TxConfig,0x03000700U);Write8(r,ChipCommand,(Byte)(CommandRxEnable|CommandTxEnable));Write8(r,Cfg9346,0);r->RxIndex=0;r->TxIndex=0;return true;
    }
    private static Boolean ServiceRecord(DeviceRecord* r)
    {
        UInt16 status=Read16(r,InterruptStatus);if(status!=0)Write16(r,InterruptStatus,status);UInt64 descriptorBytes=(UInt64)DescriptorCount*16UL;for(UInt32 guard=0;guard<DescriptorCount;guard++){UInt64 descriptor=r->RxVirtual+(UInt64)r->RxIndex*16UL;UInt32 options=Read32(descriptor);if((options&DescOwn)!=0)break;UInt32 length=options&DescLengthMask;Boolean whole=(options&(DescFs|DescLs))==(DescFs|DescLs);if(whole&&length>4U&&length<=BufferBytes&&r->ReceiveEnabled!=0&&r->NetworkHandle!=0U)KernelNetworking.QueueReceivedFrame(new KernelNetworkInterfaceHandle(r->NetworkHandle),(Byte*)(nuint)(r->RxVirtual+descriptorBytes+(UInt64)r->RxIndex*BufferBytes),length-4U,out _);UInt32 refill=DescOwn|BufferBytes;if(r->RxIndex==DescriptorCount-1U)refill|=DescEor;Write32(descriptor,refill);r->RxIndex=(r->RxIndex+1U)&(DescriptorCount-1U);}return true;
    }
}
