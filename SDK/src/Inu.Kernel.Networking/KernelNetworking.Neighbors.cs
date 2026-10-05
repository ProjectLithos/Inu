using System;

namespace Inu.Kernel.Networking;

public static unsafe partial class KernelNetworking
{
    public static Boolean UpdateNeighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelMacAddress mac)=>KernelNetworkExtensionServices.UpdateNeighbor(networkInterface,address,mac);
    public static Boolean TryResolveNeighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,out KernelMacAddress mac)=>KernelNetworkExtensionServices.TryResolveNeighbor(networkInterface,address,out mac);
    public static Boolean UpdateIpv6Neighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,KernelMacAddress mac)=>KernelNetworkExtensionServices.UpdateIpv6Neighbor(networkInterface,address,mac);
    public static Boolean TryResolveIpv6Neighbor(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,out KernelMacAddress mac)=>KernelNetworkExtensionServices.TryResolveIpv6Neighbor(networkInterface,address,out mac);
}
