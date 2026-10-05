using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Networking;

public static unsafe partial class KernelNetworking
{
    private static Boolean Allocate(UInt32 capacity,Int32 size,out KernelHeapAllocation allocation,out Byte* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)size,64,true,out allocation))return false;pointer=(Byte*)(nuint)allocation.Address;return true;}
    private static Boolean TryInterface(KernelNetworkInterfaceHandle h,out InterfaceRecord* r){r=null;Int32 i=(Int32)h.Value-1;if(!_initialized||i<0||(UInt32)i>=_interfaceCapacity||(_interfaces+i)->Used==0)return false;r=_interfaces+i;return true;}
    private static Int32 FreeInterface(){for(Int32 i=0;i<(Int32)_interfaceCapacity;i++)if((_interfaces+i)->Used==0)return i;return -1;}
    private static Boolean GrowInterfaces(){if(_options.RegistryMode!=KernelNetworkRegistryMode.Dynamic)return false;UInt32 next=KernelNetworkMath.NextCapacity(_interfaceCapacity,_options.MaximumInterfaces);if(next<=_interfaceCapacity||!Allocate(next,sizeof(InterfaceRecord),out KernelHeapAllocation fresh,out Byte* pointer))return false;Copy((Byte*)_interfaces,pointer,(UInt64)_interfaceCapacity*(UInt64)sizeof(InterfaceRecord));KernelHeapAllocation old=_interfaceAllocation;_interfaceAllocation=fresh;_interfaces=(InterfaceRecord*)pointer;_interfaceCapacity=next;return KernelHeap.TryRelease(old);}
    private static void Clear(Byte* target,Int32 bytes){for(Int32 i=0;i<bytes;i++)target[i]=0;}
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}
}
