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
    private static Boolean NetworkTransmit(KernelDeviceHandle device,Byte* frame,UInt32 length)
    {
        if(frame==null||length==0||!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Network||r->Started==0||length>r->Mtu+14U)return false;UInt64 total=VirtioNetworkHeaderBytes+(UInt64)length;UInt64 token,pages,physical,virtualAddress;if(!AllocateDma(total,out token,out pages,out physical,out virtualAddress))return false;Byte* p=(Byte*)(nuint)virtualAddress;Clear(p,VirtioNetworkHeaderBytes);Copy(frame,p+VirtioNetworkHeaderBytes,length);SetDescriptor(&r->Queue1,0,physical,(UInt32)total,0,0);Boolean ok=SubmitAndWait(r,&r->Queue1,0,out _);ReleaseDma(token,physical,pages);return ok;
    }
    private static Boolean NetworkReceiveEnabled(KernelDeviceHandle device,Boolean enabled){if(!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Network)return false;r->ReceiveEnabled=enabled?(Byte)1:(Byte)0;return true;}
    private static Boolean ServiceNetwork(DeviceRecord* r)
    {
        while(TryConsumeUsed(&r->Queue0,out _,out UInt32 length)){if(length>VirtioNetworkHeaderBytes&&length<=r->RxBytes&&r->ReceiveEnabled!=0)KernelNetworking.QueueReceivedFrame(new KernelNetworkInterfaceHandle(r->NetworkHandle),(Byte*)(nuint)(r->RxVirtual+VirtioNetworkHeaderBytes),length-VirtioNetworkHeaderBytes,out _);if(!PostReceiveBuffer(r))return false;}return true;
    }
    private static Boolean PostReceiveBuffer(DeviceRecord* r){SetDescriptor(&r->Queue0,0,r->RxPhysical,r->RxBytes,DescriptorWrite,0);return Submit(r,&r->Queue0,0);}

    private static Boolean TransferSimple(DeviceRecord* r,QueueRecord* q,Byte* source,UInt32 length,Boolean deviceWrites)
    {UInt64 token,pages,physical,virtualAddress;if(!AllocateDma(length,out token,out pages,out physical,out virtualAddress))return false;if(!deviceWrites)Copy(source,(Byte*)(nuint)virtualAddress,length);SetDescriptor(q,0,physical,length,deviceWrites?DescriptorWrite:(UInt16)0,0);Boolean ok=SubmitAndWait(r,q,0,out _);ReleaseDma(token,physical,pages);return ok;}
    private static Boolean TransferSimpleRead(DeviceRecord* r,QueueRecord* q,Byte* destination,UInt32 capacity,out UInt32 bytesRead)
    {bytesRead=0;UInt64 token,pages,physical,virtualAddress;if(!AllocateDma(capacity,out token,out pages,out physical,out virtualAddress))return false;SetDescriptor(q,0,physical,capacity,DescriptorWrite,0);Boolean ok=SubmitAndWait(r,q,0,out UInt32 used);if(ok&&used<=capacity){Copy((Byte*)(nuint)virtualAddress,destination,used);bytesRead=used;}else ok=false;ReleaseDma(token,physical,pages);return ok;}

    private static Boolean SubmitAndWait(DeviceRecord* r,QueueRecord* q,UInt16 head,out UInt32 length)
    {length=0;if(!Submit(r,q,head)||!KernelTime.TryCreateDeadline(SynchronousTimeoutNanoseconds,out UInt64 deadline))return false;while(!TryConsumeUsed(q,out _,out length)){if(KernelTime.HasReached(deadline))return false;}return true;}
    private static Boolean Submit(DeviceRecord* r,QueueRecord* q,UInt16 head)
    {if(q->Ready==0||head>=q->Size)return false;Byte* available=(Byte*)(nuint)(q->VirtualBase+q->AvailableOffset);UInt16 index=Read16((UInt64)(nuint)(available+2));*(UInt16*)(available+4+(UInt64)(index%q->Size)*2UL)=head;Write16((UInt64)(nuint)(available+2),(UInt16)(index+1));UInt64 notify=r->Notify+q->NotifyOffset*r->NotifyMultiplier;Write16(notify,q->Index);return true;}
    private static Boolean TryConsumeUsed(QueueRecord* q,out UInt32 id,out UInt32 length)
    {id=0;length=0;if(q->Ready==0)return false;Byte* used=(Byte*)(nuint)(q->VirtualBase+q->UsedOffset);UInt16 current=Read16((UInt64)(nuint)(used+2));if(q->LastUsed==current)return false;UInt64 element=4UL+(UInt64)(q->LastUsed%q->Size)*8UL;id=Read32((UInt64)(nuint)(used+element));length=Read32((UInt64)(nuint)(used+element+4));q->LastUsed++;return true;}
    private static Boolean SetDescriptor(QueueRecord* q,UInt16 index,UInt64 address,UInt32 length,UInt16 flags,UInt16 next)
    {if(q->Ready==0||index>=q->Size)return false;UInt64 descriptor=q->VirtualBase+(UInt64)index*16UL;Write64(descriptor,address);Write32(descriptor+8,length);Write16(descriptor+12,flags);Write16(descriptor+14,next);return true;}

}
