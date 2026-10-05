using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Networking;

/// <summary>Owns IPv4/IPv6 neighbour-resolution state independently from interface storage.</summary>
public static unsafe class KernelNeighborCache
{
    private struct NeighborRecord { public Byte Used,Family; public UInt32 Address,Interface; public UInt64 Address6High,Address6Low; public Byte A,B,C,D,E,F; }
    private static NeighborRecord* _neighbors;
    private static KernelHeapAllocation _allocation;
    private static UInt32 _capacity,_count,_maximum;
    private static KernelNetworkRegistryMode _mode;
    private static Boolean _initialized;

    public static UInt32 Count=>_count;
    public static UInt32 Capacity=>_capacity;
    public static Boolean IsInitialized()=>_initialized;

    public static Boolean Initialize()=>Initialize(KernelNetworkOptions.DynamicDefault);
    public static Boolean Initialize(KernelNetworkOptions options){if(_initialized)return true;if(!KernelHeap.IsInitialized()||options.InitialNeighbors==0U)return false;if(!Allocate(options.InitialNeighbors,out _allocation,out _neighbors))return false;_capacity=options.InitialNeighbors;_maximum=options.MaximumNeighbors;_mode=options.RegistryMode;if(!KernelNetworkExtensionServices.RegisterNeighborCache(&Update,&ResolveService,&UpdateIpv6,&ResolveIpv6Service,&RemoveInterface,&GetCount,&GetCapacity)){KernelHeap.TryRelease(_allocation);_neighbors=null;_capacity=0U;return false;}_initialized=true;return true;}

    internal static void RemoveInterface(KernelNetworkInterfaceHandle networkInterface){if(!_initialized)return;for(Int32 i=0;i<(Int32)_capacity;i++){NeighborRecord* n=_neighbors+i;if(n->Used!=0&&n->Interface==networkInterface.Value){Clear((Byte*)n,sizeof(NeighborRecord));if(_count>0U)_count--;}}}

    public static Boolean Update(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelMacAddress mac){if(!_initialized||networkInterface.Value==0||address.Value==0||mac.IsZero||!KernelNetworkInterfaceRegistryContract.Contains(networkInterface))return false;for(Int32 i=0;i<(Int32)_capacity;i++){NeighborRecord* n=_neighbors+i;if(n->Used!=0&&n->Family==4&&n->Interface==networkInterface.Value&&n->Address==address.Value){SetMac(n,mac);return true;}}Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}NeighborRecord* r=_neighbors+slot;r->Used=1;r->Family=4;r->Interface=networkInterface.Value;r->Address=address.Value;SetMac(r,mac);_count++;return true;}
    public static Boolean TryResolve(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,out KernelMacAddress mac){mac=default;if(!_initialized)return false;for(Int32 i=0;i<(Int32)_capacity;i++){NeighborRecord* n=_neighbors+i;if(n->Used!=0&&n->Family==4&&n->Interface==networkInterface.Value&&n->Address==address.Value){mac=new(n->A,n->B,n->C,n->D,n->E,n->F);return true;}}return false;}
    public static Boolean UpdateIpv6(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,KernelMacAddress mac){if(!_initialized||networkInterface.Value==0||address.IsUnspecified||mac.IsZero||!KernelNetworkInterfaceRegistryContract.Contains(networkInterface))return false;for(Int32 i=0;i<(Int32)_capacity;i++){NeighborRecord* n=_neighbors+i;if(n->Used!=0&&n->Family==6&&n->Interface==networkInterface.Value&&n->Address6High==address.High&&n->Address6Low==address.Low){SetMac(n,mac);return true;}}Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}NeighborRecord* r=_neighbors+slot;r->Used=1;r->Family=6;r->Interface=networkInterface.Value;r->Address6High=address.High;r->Address6Low=address.Low;SetMac(r,mac);_count++;return true;}
    public static Boolean TryResolveIpv6(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,out KernelMacAddress mac){mac=default;if(!_initialized)return false;for(Int32 i=0;i<(Int32)_capacity;i++){NeighborRecord* n=_neighbors+i;if(n->Used!=0&&n->Family==6&&n->Interface==networkInterface.Value&&n->Address6High==address.High&&n->Address6Low==address.Low){mac=new(n->A,n->B,n->C,n->D,n->E,n->F);return true;}}return false;}

    private static Boolean ResolveService(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelMacAddress* mac){if(mac==null)return false;KernelMacAddress value;if(!TryResolve(networkInterface,address,out value))return false;*mac=value;return true;}
    private static Boolean ResolveIpv6Service(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,KernelMacAddress* mac){if(mac==null)return false;KernelMacAddress value;if(!TryResolveIpv6(networkInterface,address,out value))return false;*mac=value;return true;}
    private static UInt32 GetCount()=>_count;
    private static UInt32 GetCapacity()=>_capacity;

    private static Int32 Free(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_neighbors+i)->Used==0)return i;return -1;}
    private static Boolean Grow(){if(_mode!=KernelNetworkRegistryMode.Dynamic)return false;UInt32 next=KernelNetworkMath.NextCapacity(_capacity,_maximum);if(next<=_capacity||!Allocate(next,out KernelHeapAllocation fresh,out NeighborRecord* records))return false;Copy((Byte*)_neighbors,(Byte*)records,(UInt64)_capacity*(UInt64)sizeof(NeighborRecord));KernelHeapAllocation old=_allocation;_allocation=fresh;_neighbors=records;_capacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean Allocate(UInt32 capacity,out KernelHeapAllocation allocation,out NeighborRecord* records){allocation=default;records=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(NeighborRecord),64,true,out allocation))return false;records=(NeighborRecord*)(nuint)allocation.Address;return true;}
    private static void SetMac(NeighborRecord* r,KernelMacAddress m){r->A=m.A;r->B=m.B;r->C=m.C;r->D=m.D;r->E=m.E;r->F=m.F;}
    private static void Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];}
    private static void Clear(Byte* target,Int32 bytes){for(Int32 i=0;i<bytes;i++)target[i]=0;}
}
