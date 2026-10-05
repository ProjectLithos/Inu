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
    private static Boolean EnablePci(PciLocation location)
    {
        if(!KernelPci.TryRead16(location,0x04,out UInt16 command))return false;
        return KernelPci.TryWrite16(location,0x04,(UInt16)(command|0x0006U));
    }

    private static Boolean MapTransportCapability(PciLocation location,Byte configurationType,out UInt64 virtualAddress,out UInt16 capabilityOffset)
    {virtualAddress=0;capabilityOffset=0;if(!TryFindTransportCapability(location,configurationType,out PciCapabilityInfo capability))return false;if(!KernelPci.TryRead8(location,(UInt16)(capability.Offset+2),out Byte capabilityLength)||capabilityLength<(configurationType==NotifyConfigurationType?20U:16U))return false;if(!KernelPci.TryRead8(location,(UInt16)(capability.Offset+4),out Byte barIndex)||!KernelPci.TryRead32(location,(UInt16)(capability.Offset+8),out UInt32 offset)||!KernelPci.TryRead32(location,(UInt16)(capability.Offset+12),out UInt32 length)||length==0)return false;if(!KernelPci.TryGetBar(location,barIndex,out PciBarInfo bar)||bar.Type==PciBarType.Io||offset>bar.Length||length>bar.Length-offset)return false;if(!KernelPci.TryMapMmio(bar.Address+offset,length,out virtualAddress))return false;capabilityOffset=capability.Offset;return true;}
    private static Boolean TryFindTransportCapability(PciLocation location,Byte configurationType,out PciCapabilityInfo found)
    {found=default;for(UInt32 i=0;i<48U;i++){if(!KernelPci.TryGetCapability(location,i,out PciCapabilityInfo capability))return false;if(capability.Id!=VendorCapabilityId)continue;if(KernelPci.TryRead8(location,(UInt16)(capability.Offset+3),out Byte type)&&type==configurationType){found=capability;return true;}}return false;}

    private static Boolean AllocateDma(UInt64 bytes,out UInt64 token,out UInt64 pages,out UInt64 physical,out UInt64 virtualAddress)
    {token=0;pages=0;physical=0;virtualAddress=0;if(bytes==0||bytes>UInt64.MaxValue-4095UL)return false;pages=(bytes+4095UL)/4096UL;if(!KernelPhysicalMemory.TryAllocate(pages,1,out KernelPhysicalAllocation allocation))return false;if(!KernelAddressSpace.TryPhysicalToDirectMap(allocation.StartAddress,out virtualAddress)){KernelPhysicalMemory.TryRelease(allocation);return false;}token=allocation.Token;physical=allocation.StartAddress;Clear((Byte*)(nuint)virtualAddress,pages*4096UL);return true;}
    private static Boolean ReleaseDma(UInt64 token,UInt64 physical,UInt64 pages)=>token==0?true:KernelPhysicalMemory.TryRelease(new KernelPhysicalAllocation(token,physical,pages));
    private static Boolean ReleaseQueue(QueueRecord* q){if(q->AllocationToken==0)return true;Boolean ok=ReleaseDma(q->AllocationToken,q->PhysicalBase,q->AllocationPages);Clear((Byte*)q,(UInt64)sizeof(QueueRecord));return ok;}
    private static Boolean ReleaseRecordResources(DeviceRecord* r){Boolean ok=ReleaseQueue(&r->Queue0)&ReleaseQueue(&r->Queue1);if(r->RxToken!=0)ok=ReleaseDma(r->RxToken,r->RxPhysical,r->RxPages)&ok;return ok;}

    private static Boolean SetStatus(DeviceRecord* r,VirtioDeviceStatus status){if(r->Common==0)return false;Write8(r->Common+20,(Byte)status);return true;}
    private static VirtioDeviceInfo Info(DeviceRecord* r)=>new(new KernelDeviceHandle(r->DeviceHandle),(VirtioDeviceType)r->Type,r->DeviceFeatures,r->NegotiatedFeatures,r->QueueCount,r->Started!=0,new KernelStorageDeviceHandle(r->StorageHandle),new KernelNetworkInterfaceHandle(r->NetworkHandle));
    private static Boolean TryRecord(KernelDeviceHandle device,out DeviceRecord* record){record=null;if(device.Value==0||_devices==null)return false;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->DeviceHandle==device.Value){record=r;return true;}}return false;}
    private static Int32 FreeRecord(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_devices+i)->Used==0)return i;return -1;}
    private static Boolean AllocateRecords(UInt32 capacity,out KernelHeapAllocation allocation,out DeviceRecord* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(DeviceRecord),64,true,out allocation))return false;pointer=(DeviceRecord*)(nuint)allocation.Address;return true;}
    private static Boolean GrowRecords(){UInt32 next=_capacity>=0x40000000U?UInt32.MaxValue:_capacity*2U;if(next<=_capacity||next>Int32.MaxValue||!AllocateRecords(next,out KernelHeapAllocation fresh,out DeviceRecord* pointer))return false;Copy((Byte*)_devices,(Byte*)pointer,(UInt64)_capacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_deviceAllocation;_deviceAllocation=fresh;_devices=pointer;_capacity=next;return KernelHeap.TryRelease(old);}
    private static UInt64 AlignUp(UInt64 value,UInt64 alignment)=>(value+alignment-1UL)&~(alignment-1UL);
    private static Byte Read8(UInt64 address)=>*(Byte*)(nuint)address;private static UInt16 Read16(UInt64 address)=>*(UInt16*)(nuint)address;private static UInt32 Read32(UInt64 address)=>*(UInt32*)(nuint)address;private static UInt64 Read64(UInt64 address)=>*(UInt64*)(nuint)address;
    private static Boolean Write8(UInt64 address,Byte value){*(Byte*)(nuint)address=value;return true;}private static Boolean Write16(UInt64 address,UInt16 value){*(UInt16*)(nuint)address=value;return true;}private static Boolean Write32(UInt64 address,UInt32 value){*(UInt32*)(nuint)address=value;return true;}private static Boolean Write64(UInt64 address,UInt64 value){*(UInt64*)(nuint)address=value;return true;}
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}private static Boolean Clear(Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=0;return true;}
}
