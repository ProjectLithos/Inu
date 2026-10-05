using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

public static unsafe partial class KernelPci
{
    private static PciDeviceInfo Info(DeviceRecord* r)=>new(new PciLocation(r->Segment,r->Bus,r->Device,r->Function),(PciConfigurationTransport)r->Transport,new KernelDeviceHandle(r->Handle),r->VendorId,r->DeviceId,r->SubsystemVendorId,r->SubsystemId,r->ClassCode,r->Revision,r->HeaderType);
    private static Boolean AllocateDeviceTable(UInt32 capacity,out KernelHeapAllocation allocation,out DeviceRecord* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(DeviceRecord),64,true,out allocation))return false;pointer=(DeviceRecord*)(nuint)allocation.Address;return true;}
    private static Int32 FreeDevice(){for(Int32 i=0;i<(Int32)_deviceCapacity;i++)if((_devices+i)->Used==0)return i;return -1;}
    private static Boolean GrowDevices(){UInt32 next=_deviceCapacity>=0x40000000U?UInt32.MaxValue:_deviceCapacity*2U;if(next<=_deviceCapacity||next>Int32.MaxValue)return false;if(!AllocateDeviceTable(next,out KernelHeapAllocation fresh,out DeviceRecord* pointer))return false;Copy((Byte*)_devices,(Byte*)pointer,(UInt64)_deviceCapacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_deviceAllocation;_deviceAllocation=fresh;_devices=pointer;_deviceCapacity=next;return KernelHeap.TryRelease(old);}
    private static UInt64 AlignUp(UInt64 value,UInt64 alignment)=> (value+(alignment-1UL))&~(alignment-1UL);
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}
    private static Boolean Clear(Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=0;return true;}
}
