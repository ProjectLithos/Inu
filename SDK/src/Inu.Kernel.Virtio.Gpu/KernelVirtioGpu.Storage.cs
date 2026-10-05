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
    private static Boolean IsGpu(PciDeviceInfo pci)=>pci.VendorId==VirtioVendorId&&(pci.DeviceId==ModernGpuDeviceId||(pci.DeviceId==TransitionalGpuDeviceId&&pci.SubsystemId==16U));
    private static Boolean MapTransportCapability(PciLocation location,Byte configurationType,out UInt64 virtualAddress,out UInt16 capabilityOffset){virtualAddress=0;capabilityOffset=0;if(!TryFindTransportCapability(location,configurationType,out PciCapabilityInfo capability))return false;if(!KernelPci.TryRead8(location,(UInt16)(capability.Offset+2),out Byte capabilityLength)||capabilityLength<(configurationType==NotifyConfigurationType?20U:16U))return false;if(!KernelPci.TryRead8(location,(UInt16)(capability.Offset+4),out Byte barIndex)||!KernelPci.TryRead32(location,(UInt16)(capability.Offset+8),out UInt32 offset)||!KernelPci.TryRead32(location,(UInt16)(capability.Offset+12),out UInt32 length)||length==0U)return false;if(!KernelPci.TryGetBar(location,barIndex,out PciBarInfo bar)||bar.Type==PciBarType.Io||offset>bar.Length||length>bar.Length-offset)return false;if(!KernelPci.TryMapMmio(bar.Address+offset,length,out virtualAddress))return false;capabilityOffset=capability.Offset;return true;}
    private static Boolean TryFindTransportCapability(PciLocation location,Byte configurationType,out PciCapabilityInfo found){found=default;for(UInt32 i=0;i<48U;i++){if(!KernelPci.TryGetCapability(location,i,out PciCapabilityInfo capability))return false;if(capability.Id!=VendorCapabilityId)continue;if(KernelPci.TryRead8(location,(UInt16)(capability.Offset+3),out Byte type)&&type==configurationType){found=capability;return true;}}return false;}
    private static Boolean AllocateDma(UInt64 bytes,out UInt64 token,out UInt64 pages,out UInt64 physical,out UInt64 virtualAddress){token=0;pages=0;physical=0;virtualAddress=0;if(bytes==0||bytes>UInt64.MaxValue-4095UL)return false;pages=(bytes+4095UL)/4096UL;if(!KernelPhysicalMemory.TryAllocate(pages,1,out KernelPhysicalAllocation allocation))return false;if(!KernelAddressSpace.TryPhysicalToDirectMap(allocation.StartAddress,out virtualAddress)){KernelPhysicalMemory.TryRelease(allocation);return false;}token=allocation.Token;physical=allocation.StartAddress;Clear((Byte*)(nuint)virtualAddress,pages*4096UL);return true;}
    private static Boolean ReleaseDma(UInt64 token,UInt64 physical,UInt64 pages)=>token==0?true:KernelPhysicalMemory.TryRelease(new KernelPhysicalAllocation(token,physical,pages));
    private static Boolean ReleaseResources(DeviceRecord* r){Boolean ok=true;if(r->Control.AllocationToken!=0)ok=ReleaseDma(r->Control.AllocationToken,r->Control.PhysicalBase,r->Control.AllocationPages)&ok;if(r->FrameToken!=0)ok=ReleaseDma(r->FrameToken,r->FramePhysical,r->FramePages)&ok;return ok;}
    private static Boolean SetStatus(DeviceRecord* r,Byte status){if(r->Common==0)return false;Write8(r->Common+20,status);return true;}
    private static VirtioGpuInfo Info(DeviceRecord* r)=>new(new KernelDeviceHandle(r->DeviceHandle),new KernelGraphicsDisplayHandle(r->GraphicsDisplay),new KernelGraphicsMode(r->Width,r->Height,r->Pitch,KernelGraphicsPixelFormat.BlueGreenRedReserved8),r->Scanout,r->Started!=0);
    private static Boolean TryRecord(KernelDeviceHandle device,out DeviceRecord* record){record=null;if(device.Value==0||_devices==null)return false;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->DeviceHandle==device.Value){record=r;return true;}}return false;}
    private static Boolean TryDisplay(KernelGraphicsDisplayHandle display,out DeviceRecord* record){record=null;if(display.Value==0||_devices==null)return false;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->GraphicsDisplay==display.Value){record=r;return true;}}return false;}
    private static Int32 Free(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_devices+i)->Used==0)return i;return -1;}
    private static Boolean AllocateRecords(UInt32 capacity,out KernelHeapAllocation allocation,out DeviceRecord* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(DeviceRecord),64U,true,out allocation))return false;pointer=(DeviceRecord*)(nuint)allocation.Address;return true;}
    private static Boolean Grow(){UInt32 next=_capacity>=0x40000000U?UInt32.MaxValue:_capacity*2U;if(next<=_capacity||next>Int32.MaxValue||!AllocateRecords(next,out KernelHeapAllocation fresh,out DeviceRecord* p))return false;Copy((Byte*)_devices,(Byte*)p,(UInt64)_capacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_allocation;_allocation=fresh;_devices=p;_capacity=next;return KernelHeap.TryRelease(old);}
    private static Byte Read8(UInt64 address)=>*(Byte*)(nuint)address;private static UInt16 Read16(UInt64 address)=>*(UInt16*)(nuint)address;private static UInt32 Read32(UInt64 address)=>*(UInt32*)(nuint)address;
    private static Boolean Write8(UInt64 address,Byte value){*(Byte*)(nuint)address=value;return true;}private static Boolean Write16(UInt64 address,UInt16 value){*(UInt16*)(nuint)address=value;return true;}private static Boolean Write32(UInt64 address,UInt32 value){*(UInt32*)(nuint)address=value;return true;}private static Boolean Write64(UInt64 address,UInt64 value){*(UInt64*)(nuint)address=value;return true;}
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}private static Boolean Clear(Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=0;return true;}
}
