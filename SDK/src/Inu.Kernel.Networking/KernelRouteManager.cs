using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Networking;

/// <summary>Owns IPv4 route state independently from the network-interface registry.</summary>
public static unsafe class KernelRouteManager
{
    private struct RouteRecord { public Byte Used; public UInt32 Network,Mask,Gateway,Interface,Metric; }
    private static RouteRecord* _routes;
    private static KernelHeapAllocation _allocation;
    private static UInt32 _capacity,_count,_maximum;
    private static KernelNetworkRegistryMode _mode;
    private static Boolean _initialized;

    public static UInt32 Count=>_count;
    public static UInt32 Capacity=>_capacity;
    public static Boolean IsInitialized()=>_initialized;

    public static Boolean Initialize()=>Initialize(KernelNetworkOptions.DynamicDefault);
    public static Boolean Initialize(KernelNetworkOptions options)
    {
        if(_initialized)return true;
        if(!KernelHeap.IsInitialized()||options.InitialRoutes==0U)return false;
        if(!Allocate(options.InitialRoutes,out _allocation,out _routes))return false;
        _capacity=options.InitialRoutes;_maximum=options.MaximumRoutes;_mode=options.RegistryMode;
        if(!KernelNetworkExtensionServices.RegisterRouteManager(&Add,&ResolveService,&RemoveInterface,&GetCount,&GetCapacity)){KernelHeap.TryRelease(_allocation);_routes=null;_capacity=0U;return false;}
        _initialized=true;return true;
    }

    public static Boolean Add(KernelNetworkRoute route)
    {
        if(!_initialized||route.Interface.Value==0||!KernelNetworkInterfaceRegistryContract.Contains(route.Interface))return false;
        Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}
        RouteRecord* r=_routes+slot;r->Used=1;r->Network=route.Network.Value;r->Mask=route.Mask.Value;r->Gateway=route.Gateway.Value;r->Interface=route.Interface.Value;r->Metric=route.Metric;_count++;return true;
    }

    internal static void RemoveInterface(KernelNetworkInterfaceHandle networkInterface){if(!_initialized)return;for(Int32 i=0;i<(Int32)_capacity;i++){RouteRecord* r=_routes+i;if(r->Used!=0&&r->Interface==networkInterface.Value){Clear((Byte*)r,sizeof(RouteRecord));if(_count>0U)_count--;}}}

    public static Boolean TryResolve(KernelIpv4Address destination,out KernelNetworkRoute route)
    {
        route=default;if(!_initialized)return false;Boolean found=false;UInt32 bestPrefix=0,bestMetric=UInt32.MaxValue;
        for(Int32 i=0;i<(Int32)_capacity;i++){RouteRecord* r=_routes+i;if(r->Used==0)continue;KernelNetworkRoute candidate=new(new KernelIpv4Address(r->Network),new KernelIpv4Address(r->Mask),new KernelIpv4Address(r->Gateway),new KernelNetworkInterfaceHandle(r->Interface),r->Metric);if(!KernelNetworkMath.RouteMatches(candidate,destination))continue;UInt32 prefix=KernelNetworkMath.PrefixLength(candidate.Mask);if(!found||prefix>bestPrefix||(prefix==bestPrefix&&candidate.Metric<bestMetric)){found=true;bestPrefix=prefix;bestMetric=candidate.Metric;route=candidate;}}
        return found;
    }

    private static Boolean ResolveService(KernelIpv4Address destination,KernelNetworkRoute* route){if(route==null)return false;KernelNetworkRoute value;if(!TryResolve(destination,out value))return false;*route=value;return true;}
    private static UInt32 GetCount()=>_count;
    private static UInt32 GetCapacity()=>_capacity;

    private static Int32 Free(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_routes+i)->Used==0)return i;return -1;}
    private static Boolean Grow(){if(_mode!=KernelNetworkRegistryMode.Dynamic)return false;UInt32 next=KernelNetworkMath.NextCapacity(_capacity,_maximum);if(next<=_capacity||!Allocate(next,out KernelHeapAllocation fresh,out RouteRecord* records))return false;Copy((Byte*)_routes,(Byte*)records,(UInt64)_capacity*(UInt64)sizeof(RouteRecord));KernelHeapAllocation old=_allocation;_allocation=fresh;_routes=records;_capacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean Allocate(UInt32 capacity,out KernelHeapAllocation allocation,out RouteRecord* records){allocation=default;records=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(RouteRecord),64,true,out allocation))return false;records=(RouteRecord*)(nuint)allocation.Address;return true;}
    private static void Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];}
    private static void Clear(Byte* target,Int32 bytes){for(Int32 i=0;i<bytes;i++)target[i]=0;}
}
