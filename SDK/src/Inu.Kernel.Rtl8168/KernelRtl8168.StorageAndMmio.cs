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
    private static Rtl8168DeviceInfo Info(DeviceRecord* r)=>new(new KernelDeviceHandle(r->DeviceHandle),new PciLocation(r->Segment,r->Bus,r->PciDevice,r->Function),(Rtl8168ControllerFamily)r->Family,new KernelMacAddress(r->MacA,r->MacB,r->MacC,r->MacD,r->MacE,r->MacF),new KernelNetworkInterfaceHandle(r->NetworkHandle),r->Mtu,r->Msi!=0);
    private static Boolean TryRecord(KernelDeviceHandle device,out DeviceRecord* record){record=null;if(device.Value==0||_devices==null)return false;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->DeviceHandle==device.Value){record=r;return true;}}return false;}
    private static Int32 FreeRecord(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_devices+i)->Used==0)return i;return -1;}
    private static Boolean AllocateRecords(UInt32 capacity,out KernelHeapAllocation allocation,out DeviceRecord* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(DeviceRecord),64,true,out allocation))return false;pointer=(DeviceRecord*)(nuint)allocation.Address;return true;}
    private static Boolean GrowRecords(){UInt32 next=_capacity>=0x40000000U?UInt32.MaxValue:_capacity*2U;if(next<=_capacity||next>Int32.MaxValue||!AllocateRecords(next,out KernelHeapAllocation fresh,out DeviceRecord* pointer))return false;Copy((Byte*)_devices,(Byte*)pointer,(UInt64)_capacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_allocation;_allocation=fresh;_devices=pointer;_capacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean AllocateDma(UInt64 bytes,out UInt64 token,out UInt64 pages,out UInt64 physical,out UInt64 virtualAddress){token=pages=physical=virtualAddress=0;if(bytes==0||bytes>UInt64.MaxValue-4095UL)return false;pages=(bytes+4095UL)/4096UL;if(!KernelPhysicalMemory.TryAllocate(pages,1,out KernelPhysicalAllocation a))return false;if(!KernelAddressSpace.TryPhysicalToDirectMap(a.StartAddress,out virtualAddress)){KernelPhysicalMemory.TryRelease(a);return false;}token=a.Token;physical=a.StartAddress;Clear((Byte*)(nuint)virtualAddress,pages*4096UL);return true;}
    private static Boolean ReleaseDma(UInt64 token,UInt64 physical,UInt64 pages)=>token==0?true:KernelPhysicalMemory.TryRelease(new KernelPhysicalAllocation(token,physical,pages));private static Boolean ReleaseResources(DeviceRecord* r){Boolean ok=true;if(r->RxToken!=0)ok=ReleaseDma(r->RxToken,r->RxPhysical,r->RxPages)&ok;if(r->TxToken!=0)ok=ReleaseDma(r->TxToken,r->TxPhysical,r->TxPages)&ok;r->RxToken=r->TxToken=0;return ok;}
    private static Byte Read8(DeviceRecord* r,UInt32 offset)=>*(Byte*)(nuint)(r->Mmio+offset);private static UInt16 Read16(DeviceRecord* r,UInt32 offset)=>*(UInt16*)(nuint)(r->Mmio+offset);private static UInt32 Read32(DeviceRecord* r,UInt32 offset)=>*(UInt32*)(nuint)(r->Mmio+offset);private static Boolean Write8(DeviceRecord* r,UInt32 offset,Byte value){*(Byte*)(nuint)(r->Mmio+offset)=value;return true;}private static Boolean Write16(DeviceRecord* r,UInt32 offset,UInt16 value){*(UInt16*)(nuint)(r->Mmio+offset)=value;return true;}private static Boolean Write32(DeviceRecord* r,UInt32 offset,UInt32 value){*(UInt32*)(nuint)(r->Mmio+offset)=value;return true;}
    private static UInt32 Read32(UInt64 address)=>*(UInt32*)(nuint)address;private static Boolean Write32(UInt64 address,UInt32 value){*(UInt32*)(nuint)address=value;return true;}
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}private static Boolean Clear(Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=0;return true;}
}
