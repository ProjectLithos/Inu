using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio.Gpu;

public static unsafe partial class KernelVirtioGpu
{
    private static Boolean InitializeTransport(DeviceRecord* r,PciLocation location)
    {
        if(!MapTransportCapability(location,CommonConfigurationType,out r->Common,out _)||!MapTransportCapability(location,NotifyConfigurationType,out r->Notify,out UInt16 notifyCap))return false;MapTransportCapability(location,IsrConfigurationType,out r->Isr,out _);MapTransportCapability(location,DeviceConfigurationType,out r->DeviceConfig,out _);if(!KernelPci.TryRead32(location,(UInt16)(notifyCap+16U),out UInt32 multiplier)||multiplier==0U)return false;r->NotifyMultiplier=multiplier;
        Write8(r->Common+20,0);Write16(r->Common+16,0xFFFF);Write8(r->Common+20,1);Write8(r->Common+20,3);Write32(r->Common,0);UInt64 features=Read32(r->Common+4);Write32(r->Common,1);features|=(UInt64)Read32(r->Common+4)<<32;r->DeviceFeatures=features;if((features&FeatureVersion1)==0UL)return false;r->NegotiatedFeatures=features&FeatureVersion1;Write32(r->Common+8,0);Write32(r->Common+12,(UInt32)r->NegotiatedFeatures);Write32(r->Common+8,1);Write32(r->Common+12,(UInt32)(r->NegotiatedFeatures>>32));Write8(r->Common+20,(Byte)(Read8(r->Common+20)|8U));return (Read8(r->Common+20)&8U)!=0;
    }
    private static Boolean SetupQueue(DeviceRecord* r,QueueRecord* q,UInt16 queueIndex,UInt16 requested)
    {
        Write16(r->Common+22,queueIndex);UInt16 maximum=Read16(r->Common+24);UInt16 size=SelectQueueSize(maximum,requested);if(size==0U)return false;Write16(r->Common+24,size);UInt16 notifyOffset=Read16(r->Common+30);UInt64 descriptors=(UInt64)size*16UL,available=6UL+(UInt64)size*2UL,usedOffset=(descriptors+available+3UL)&~3UL,total=usedOffset+6UL+(UInt64)size*8UL;if(!AllocateDma(total,out q->AllocationToken,out q->AllocationPages,out q->PhysicalBase,out q->VirtualBase))return false;q->Index=queueIndex;q->Size=size;q->AvailableOffset=descriptors;q->UsedOffset=usedOffset;q->NotifyOffset=notifyOffset;Write64(r->Common+32,q->PhysicalBase);Write64(r->Common+40,q->PhysicalBase+q->AvailableOffset);Write64(r->Common+48,q->PhysicalBase+q->UsedOffset);Write16(r->Common+28,1);q->Ready=1;return true;
    }
    private static UInt16 SelectQueueSize(UInt16 maximum,UInt16 requested){UInt16 limit=maximum<requested?maximum:requested;if(limit==0U)return 0;UInt16 result=1;while(result<=limit/2U)result=(UInt16)(result*2U);return result;}
    private static Boolean SubmitAndWait(DeviceRecord* r,QueueRecord* q,UInt16 head,out UInt32 length){length=0;if(!Submit(r,q,head)||!KernelTime.TryCreateDeadline(SynchronousTimeoutNanoseconds,out UInt64 deadline))return false;while(!TryConsumeUsed(q,out _,out length)){if(KernelTime.HasReached(deadline))return false;}return true;}
    private static Boolean Submit(DeviceRecord* r,QueueRecord* q,UInt16 head){if(q->Ready==0||head>=q->Size)return false;Byte* available=(Byte*)(nuint)(q->VirtualBase+q->AvailableOffset);UInt16 index=Read16((UInt64)(nuint)(available+2));*(UInt16*)(available+4+(UInt64)(index%q->Size)*2UL)=head;Write16((UInt64)(nuint)(available+2),(UInt16)(index+1));Write16(r->Notify+(UInt64)q->NotifyOffset*r->NotifyMultiplier,q->Index);return true;}
    private static Boolean TryConsumeUsed(QueueRecord* q,out UInt32 id,out UInt32 length){id=0;length=0;if(q->Ready==0)return false;Byte* used=(Byte*)(nuint)(q->VirtualBase+q->UsedOffset);UInt16 current=Read16((UInt64)(nuint)(used+2));if(q->LastUsed==current)return false;UInt64 element=4UL+(UInt64)(q->LastUsed%q->Size)*8UL;id=Read32((UInt64)(nuint)(used+element));length=Read32((UInt64)(nuint)(used+element+4));q->LastUsed++;return true;}
    private static Boolean SetDescriptor(QueueRecord* q,UInt16 index,UInt64 address,UInt32 length,UInt16 flags,UInt16 next){if(q->Ready==0||index>=q->Size)return false;UInt64 d=q->VirtualBase+(UInt64)index*16UL;Write64(d,address);Write32(d+8,length);Write16(d+12,flags);Write16(d+14,next);return true;}

    private static Boolean EnablePci(PciLocation location)
    {
        if(!KernelPci.TryRead16(location,0x04,out UInt16 command))return false;
        // VirtIO BAR access needs PCI Memory Space and virtqueue DMA needs Bus Master.
        return KernelPci.TryWrite16(location,0x04,(UInt16)(command|0x0006U));
    }

}
