using System;
using Inu.Kernel.Console;
#if INU_KERNELAREA_NETWORKING
using Inu.Kernel.Networking;
using Inu.Kernel.Virtio;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
using Inu.Kernel.E1000;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
using Inu.Kernel.Rtl8168;
#endif
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Starts only networking protocols, services and drivers selected by the OS architecture.</summary>
public static class NetworkingHardwareStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_NETWORKING
#if INU_COMPONENT_NETWORK_SOCKET_SERVICE
        if (!KernelSockets.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_DHCP_CODEC
        if (!KernelDhcpCodec.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_DNS_CODEC
        if (!KernelDnsCodec.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_ARP_PROTOCOL
        if (!KernelArpProtocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_IPV4_PROTOCOL
        if (!KernelIpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_ICMPV4_PROTOCOL
        if (!KernelIcmpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_UDP_IPV4_PROTOCOL
        if (!KernelUdpIpv4Protocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_TCP_OBSERVATION_PROTOCOL
        if (!KernelTcpObservationProtocol.Register()) return false;
#endif
#if INU_COMPONENT_NETWORK_IPV6_NDP_PROTOCOL
        if (!KernelIpv6NdpProtocol.Register()) return false;
#endif
        if (!KernelNetworking.Initialize()) return false;
#if INU_COMPONENT_NETWORK_ROUTE_MANAGER
        if (!KernelRouteManager.Initialize()) return false;
#endif
#if INU_COMPONENT_NETWORK_NEIGHBOR_CACHE
        if (!KernelNeighborCache.Initialize()) return false;
#endif
        if (!KernelVirtio.Initialize()) return false;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
        if (!KernelE1000.Initialize()) return false;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
        if (!KernelRtl8168.Initialize()) return false;
#endif
        if (!KernelStructuredLogging.InfoLine("networking","NetworkingHardwareStartup.Initialize","Selected kernel-domain networking services online.")) return false;
#endif
        return true;
    }
}
