using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio;

public static unsafe partial class KernelVirtio
{
    private static Boolean InitializeTransport(DeviceRecord* r,PciLocation location,VirtioDeviceType type)
    {
        if(!MapTransportCapability(location,CommonConfigurationType,out r->Common,out _)||!MapTransportCapability(location,NotifyConfigurationType,out r->Notify,out UInt16 notifyCap))return false;
        MapTransportCapability(location,IsrConfigurationType,out r->Isr,out _);MapTransportCapability(location,DeviceConfigurationType,out r->DeviceConfig,out _);if(!KernelPci.TryRead32(location,(UInt16)(notifyCap+16U),out UInt32 multiplier)||multiplier==0U)return false;r->NotifyMultiplier=multiplier;
        Write8(r->Common+20,0);Write16(r->Common+16,(UInt16)0xFFFF);Write8(r->Common+20,(Byte)VirtioDeviceStatus.Acknowledge);Write8(r->Common+20,(Byte)(VirtioDeviceStatus.Acknowledge|VirtioDeviceStatus.Driver));
        Write32(r->Common+0,0);UInt64 features=Read32(r->Common+4);Write32(r->Common+0,1);features|=(UInt64)Read32(r->Common+4)<<32;r->DeviceFeatures=features;if((features&FeatureVersion1)==0UL)return false;
        UInt64 supported=FeatureVersion1;if(type==VirtioDeviceType.Block)supported|=BlockFeatureReadOnly|BlockFeatureBlockSize|BlockFeatureFlush;else if(type==VirtioDeviceType.Network)supported|=NetworkFeatureMtu|NetworkFeatureMac|NetworkFeatureStatus;r->NegotiatedFeatures=features&supported;
        Write32(r->Common+8,0);Write32(r->Common+12,(UInt32)r->NegotiatedFeatures);Write32(r->Common+8,1);Write32(r->Common+12,(UInt32)(r->NegotiatedFeatures>>32));
        Byte status=(Byte)(Read8(r->Common+20)|(Byte)VirtioDeviceStatus.FeaturesOk);Write8(r->Common+20,status);if((Read8(r->Common+20)&(Byte)VirtioDeviceStatus.FeaturesOk)==0)return false;return true;
    }

    private static Boolean InitializeBlock(DeviceRecord* r)
    {
        if(r->DeviceConfig==0||!SetupQueue(r,&r->Queue0,0,128))return false;UInt64 sectors=Read64(r->DeviceConfig);UInt32 blockSize=(r->NegotiatedFeatures&BlockFeatureBlockSize)!=0?Read32(r->DeviceConfig+20):512U;if(blockSize<512U||(blockSize&(blockSize-1U))!=0U)return false;UInt64 bytes=sectors>UInt64.MaxValue/512UL?UInt64.MaxValue:sectors*512UL;UInt64 blocks=bytes/blockSize;if(blocks==0)return false;r->BlockSize=blockSize;r->BlockCount=blocks;Boolean readOnly=(r->NegotiatedFeatures&BlockFeatureReadOnly)!=0;
        KernelStorageGeometry geometry=new(blockSize,blockSize,blocks,readOnly,false);KernelContextualBlockDeviceCallbacks callbacks=new(&BlockRead,&BlockWrite,&BlockFlush);if(!KernelStorage.RegisterBlockDevice(new KernelDeviceHandle(r->DeviceHandle),KernelStorageDeviceKind.Virtual,geometry,callbacks,out KernelStorageDeviceHandle storage))return false;r->StorageHandle=storage.Value;return true;
    }

    private static Boolean InitializeNetwork(DeviceRecord* r)
    {
        if(!KernelNetworking.IsInitialized())return false;if(r->DeviceConfig==0||(r->NegotiatedFeatures&NetworkFeatureMac)==0||!SetupQueue(r,&r->Queue0,0,128)||!SetupQueue(r,&r->Queue1,1,128))return false;KernelMacAddress mac=new(Read8(r->DeviceConfig),Read8(r->DeviceConfig+1),Read8(r->DeviceConfig+2),Read8(r->DeviceConfig+3),Read8(r->DeviceConfig+4),Read8(r->DeviceConfig+5));if(mac.IsZero)return false;UInt32 mtu=(r->NegotiatedFeatures&NetworkFeatureMtu)!=0?Read16(r->DeviceConfig+10):1500U;if(mtu<576U||mtu>65525U)return false;r->Mtu=mtu;
        UInt64 rxBytes=(UInt64)mtu+VirtioNetworkHeaderBytes;if(!AllocateDma(rxBytes,out r->RxToken,out r->RxPages,out r->RxPhysical,out r->RxVirtual))return false;r->RxBytes=(UInt32)rxBytes;if(!PostReceiveBuffer(r))return false;KernelContextualNetworkInterfaceCallbacks callbacks=new(&NetworkTransmit,&NetworkReceiveEnabled);if(!KernelNetworking.RegisterInterface(new KernelDeviceHandle(r->DeviceHandle),mac,mtu,callbacks,out KernelNetworkInterfaceHandle network))return false;r->NetworkHandle=network.Value;return true;
    }

    private static Boolean InitializeConsole(DeviceRecord* r)=>SetupQueue(r,&r->Queue0,0,64)&&SetupQueue(r,&r->Queue1,1,64);
    private static Boolean InitializeEntropy(DeviceRecord* r)=>SetupQueue(r,&r->Queue0,0,64);

    private static Boolean SetupQueue(DeviceRecord* r,QueueRecord* q,UInt16 index,UInt16 requested)
    {
        Write16(r->Common+22,index);UInt16 maximum=Read16(r->Common+24);if(maximum==0)return false;UInt16 size=VirtioMath.SelectQueueSize(maximum,requested);if(size<8)return false;Write16(r->Common+24,size);
        UInt64 descriptorBytes=(UInt64)size*16UL;UInt64 availableOffset=descriptorBytes;UInt64 availableBytes=6UL+(UInt64)size*2UL;UInt64 usedOffset=AlignUp(availableOffset+availableBytes,4UL);UInt64 usedBytes=6UL+(UInt64)size*8UL;UInt64 bytes=usedOffset+usedBytes;
        if(!AllocateDma(bytes,out UInt64 token,out UInt64 pages,out UInt64 physical,out UInt64 virtualAddress))return false;Clear((Byte*)(nuint)virtualAddress,pages*4096UL);q->Index=index;q->Size=size;q->PhysicalBase=physical;q->VirtualBase=virtualAddress;q->DescriptorOffset=0;q->AvailableOffset=availableOffset;q->UsedOffset=usedOffset;q->AllocationToken=token;q->AllocationPages=pages;q->LastUsed=0;
        Write64(r->Common+32,physical);Write64(r->Common+40,physical+availableOffset);Write64(r->Common+48,physical+usedOffset);q->NotifyOffset=Read16(r->Common+30);Write16(r->Common+26,(UInt16)0xFFFF);Write16(r->Common+28,1);if(Read16(r->Common+28)==0){ReleaseDma(token,physical,pages);Clear((Byte*)q,(UInt64)sizeof(QueueRecord));return false;}q->Ready=1;return true;
    }

}
