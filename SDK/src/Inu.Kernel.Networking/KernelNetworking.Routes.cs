using System;

namespace Inu.Kernel.Networking;

public static unsafe partial class KernelNetworking
{
    public static Boolean AddRoute(KernelNetworkRoute route)=>KernelNetworkExtensionServices.AddRoute(route);
    public static Boolean TryResolveRoute(KernelIpv4Address destination,out KernelNetworkRoute route)=>KernelNetworkExtensionServices.TryResolveRoute(destination,out route);
}
