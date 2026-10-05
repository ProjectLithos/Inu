using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    private static Int32 FindFreeDriver(){for(Int32 i=0;i<(Int32)_driverCapacity;i++)if(Driver(i)->Used==0)return i;return -1;}
    private static Int32 FindFreeDevice(){for(Int32 i=0;i<(Int32)_deviceCapacity;i++)if(Device(i)->Used==0)return i;return -1;}
    private static Boolean GrowDrivers(){if(_mode!=KernelDriverRegistryMode.Dynamic)return false;UInt32 next=KernelDriverMath.NextCapacity(_driverCapacity,_maximumDrivers);if(next<=_driverCapacity)return false;if(!AllocateDriverTable(next,out KernelHeapAllocation allocation,out DriverRecord* table))return false;Copy((Byte*)_drivers,(Byte*)table,(UInt64)_driverCapacity*(UInt64)sizeof(DriverRecord));KernelHeapAllocation old=_driverAllocation;if(!KernelHeap.TryRelease(old)){KernelHeap.TryRelease(allocation);return false;}_driverAllocation=allocation;_drivers=table;_driverCapacity=next;return true;}
    private static Boolean GrowDevices(){if(_mode!=KernelDriverRegistryMode.Dynamic)return false;UInt32 next=KernelDriverMath.NextCapacity(_deviceCapacity,_maximumDevices);if(next<=_deviceCapacity)return false;if(!AllocateDeviceTable(next,out KernelHeapAllocation allocation,out DeviceRecord* table))return false;Copy((Byte*)_devices,(Byte*)table,(UInt64)_deviceCapacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_deviceAllocation;if(!KernelHeap.TryRelease(old)){KernelHeap.TryRelease(allocation);return false;}_deviceAllocation=allocation;_devices=table;_deviceCapacity=next;return true;}
    private static Boolean AllocateDriverTable(UInt32 capacity,out KernelHeapAllocation allocation,out DriverRecord* table){allocation=default;table=null;UInt64 bytes=(UInt64)capacity*(UInt64)sizeof(DriverRecord);if(!KernelHeap.TryAllocate(bytes,64UL,true,out allocation))return false;table=(DriverRecord*)(nuint)allocation.Address;return true;}
    private static Boolean AllocateDeviceTable(UInt32 capacity,out KernelHeapAllocation allocation,out DeviceRecord* table){allocation=default;table=null;UInt64 bytes=(UInt64)capacity*(UInt64)sizeof(DeviceRecord);if(!KernelHeap.TryAllocate(bytes,64UL,true,out allocation))return false;table=(DeviceRecord*)(nuint)allocation.Address;return true;}
    private static Boolean AllocateDeviceEventTable(out KernelHeapAllocation allocation,out DeviceEventRecord* table){allocation=default;table=null;UInt64 bytes=(UInt64)DeviceEventCapacity*(UInt64)sizeof(DeviceEventRecord);if(!KernelHeap.TryAllocate(bytes,64UL,true,out allocation))return false;table=(DeviceEventRecord*)(nuint)allocation.Address;return true;}
    private static Boolean TryDriver(KernelDriverHandle handle,out DriverRecord* record){record=null;Int32 i=(Int32)handle.Value-1;if(!_initialized||i<0||(UInt32)i>=_driverCapacity)return false;DriverRecord* r=Driver(i);if(r->Used==0)return false;record=r;return true;}
    private static Boolean TryDevice(KernelDeviceHandle handle,out DeviceRecord* record){record=null;Int32 i=(Int32)handle.Value-1;if(!_initialized||i<0||(UInt32)i>=_deviceCapacity)return false;DeviceRecord* d=Device(i);if(d->Used==0)return false;record=d;return true;}
    private static Boolean TryBound(KernelDeviceHandle device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context){r=null;context=default;if(!TryDevice(device,out d)||d->BoundDriver==0U)return false;r=Driver((Int32)d->BoundDriver-1);if(r->Used==0)return false;context=new KernelDriverDeviceContext(device,new KernelDriverHandle(d->BoundDriver),Identifier(d));return true;}
    private static void CopyDriverName(DriverRecord* r,String name){if(r==null||name==null)return;Int32 n=name.Length;if(n>DriverNameBytes)n=DriverNameBytes;for(Int32 i=0;i<n;i++){Char c=name[i];r->Name[i]=(Byte)(c>=32&&c<=126?c:'?');}r->NameLength=(Byte)n;}
    private static KernelDeviceIdentifier Identifier(DeviceRecord* d)=>new((KernelDeviceBus)d->Bus,d->Vendor,d->Device,d->SubsystemVendor,d->Subsystem,d->ClassCode,d->Revision,d->Location);
    private static KernelDriverMatchRule Rule(DriverRecord* r)=>new((KernelDeviceBus)r->Bus,r->MatchBus!=0,r->Vendor,r->MatchVendor!=0,r->Device,r->MatchDevice!=0,r->ClassCode,r->ClassMask);
    private static DriverRecord* Driver(Int32 slot)=>_drivers+slot; private static DeviceRecord* Device(Int32 slot)=>_devices+slot;
    private static void Clear(Byte* p,Int32 bytes){for(Int32 i=0;i<bytes;i++)p[i]=0;} private static void Copy(Byte* source,Byte* target,UInt64 bytes){for(UInt64 i=0UL;i<bytes;i++)target[i]=source[i];}
}
