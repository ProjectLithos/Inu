using System;

namespace Inu.Kernel.Networking;

/// <summary>Dependency-resolved registry for optional networking services.</summary>
public static unsafe class KernelNetworkExtensionServices
{
    private static delegate*<KernelNetworkRoute,Boolean> _addRoute;
    private static delegate*<KernelIpv4Address,KernelNetworkRoute*,Boolean> _resolveRoute;
    private static delegate*<KernelNetworkInterfaceHandle,void> _removeRoutesForInterface;
    private static delegate*<UInt32> _routeCount;
    private static delegate*<UInt32> _routeCapacity;

    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelMacAddress,Boolean> _updateNeighbor;
    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelMacAddress*,Boolean> _resolveNeighbor;
    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv6Address,KernelMacAddress,Boolean> _updateIpv6Neighbor;
    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv6Address,KernelMacAddress*,Boolean> _resolveIpv6Neighbor;
    private static delegate*<KernelNetworkInterfaceHandle,void> _removeNeighborsForInterface;
    private static delegate*<UInt32> _neighborCount;
    private static delegate*<UInt32> _neighborCapacity;

    public static Boolean HasRouteManager=>_addRoute!=null&&_resolveRoute!=null;
    public static Boolean HasNeighborCache=>_updateNeighbor!=null&&_resolveNeighbor!=null&&_updateIpv6Neighbor!=null&&_resolveIpv6Neighbor!=null;

    public static Boolean RegisterRouteManager(
        delegate*<KernelNetworkRoute,Boolean> addRoute,
        delegate*<KernelIpv4Address,KernelNetworkRoute*,Boolean> resolveRoute,
        delegate*<KernelNetworkInterfaceHandle,void> removeInterface,
        delegate*<UInt32> count,
        delegate*<UInt32> capacity)
    {
        if(addRoute==null||resolveRoute==null||removeInterface==null||count==null||capacity==null)return false;
        if(_addRoute!=null)return false;
        _addRoute=addRoute;_resolveRoute=resolveRoute;_removeRoutesForInterface=removeInterface;_routeCount=count;_routeCapacity=capacity;
        return true;
    }

    public static Boolean RegisterNeighborCache(
        delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelMacAddress,Boolean> updateNeighbor,
        delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelMacAddress*,Boolean> resolveNeighbor,
        delegate*<KernelNetworkInterfaceHandle,KernelIpv6Address,KernelMacAddress,Boolean> updateIpv6Neighbor,
        delegate*<KernelNetworkInterfaceHandle,KernelIpv6Address,KernelMacAddress*,Boolean> resolveIpv6Neighbor,
        delegate*<KernelNetworkInterfaceHandle,void> removeInterface,
        delegate*<UInt32> count,
        delegate*<UInt32> capacity)
    {
        if(updateNeighbor==null||resolveNeighbor==null||updateIpv6Neighbor==null||resolveIpv6Neighbor==null||removeInterface==null||count==null||capacity==null)return false;
        if(_updateNeighbor!=null)return false;
        _updateNeighbor=updateNeighbor;_resolveNeighbor=resolveNeighbor;_updateIpv6Neighbor=updateIpv6Neighbor;_resolveIpv6Neighbor=resolveIpv6Neighbor;_removeNeighborsForInterface=removeInterface;_neighborCount=count;_neighborCapacity=capacity;
        return true;
    }

    internal static Boolean AddRoute(KernelNetworkRoute route)=>_addRoute!=null&&_addRoute(route);
    internal static Boolean TryResolveRoute(KernelIpv4Address destination,out KernelNetworkRoute route)
    {
        route=default;if(_resolveRoute==null)return false;KernelNetworkRoute value=default;if(!_resolveRoute(destination,&value))return false;route=value;return true;
    }
    internal static Boolean UpdateNeighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelMacAddress mac)=>_updateNeighbor!=null&&_updateNeighbor(networkInterface,address,mac);
    internal static Boolean TryResolveNeighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,out KernelMacAddress mac)
    {
        mac=default;if(_resolveNeighbor==null)return false;KernelMacAddress value=default;if(!_resolveNeighbor(networkInterface,address,&value))return false;mac=value;return true;
    }
    internal static Boolean UpdateIpv6Neighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,KernelMacAddress mac)=>_updateIpv6Neighbor!=null&&_updateIpv6Neighbor(networkInterface,address,mac);
    internal static Boolean TryResolveIpv6Neighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,out KernelMacAddress mac)
    {
        mac=default;if(_resolveIpv6Neighbor==null)return false;KernelMacAddress value=default;if(!_resolveIpv6Neighbor(networkInterface,address,&value))return false;mac=value;return true;
    }
    internal static void RemoveInterface(KernelNetworkInterfaceHandle handle)
    {
        if(_removeRoutesForInterface!=null)_removeRoutesForInterface(handle);
        if(_removeNeighborsForInterface!=null)_removeNeighborsForInterface(handle);
    }
    internal static UInt32 RouteCount()=>_routeCount==null?0U:_routeCount();
    internal static UInt32 RouteCapacity()=>_routeCapacity==null?0U:_routeCapacity();
    internal static UInt32 NeighborCount()=>_neighborCount==null?0U:_neighborCount();
    internal static UInt32 NeighborCapacity()=>_neighborCapacity==null?0U:_neighborCapacity();
}
