# Inu Network Stack API

Inu 0.22.0 standardises the kernel network stack behind one public, freestanding contract. Hardware drivers supply NIC frame transport; Ethernet and protocol processing live above that boundary. Applications and services use sockets rather than calling NIC drivers directly.

## Layers

The standard stack exposes the following layers: NIC, Ethernet, ARP, NDP, IPv4, IPv6, ICMP, UDP, TCP, sockets and DNS. `KernelNetworkApi.GetCapabilities()` reports the standard layer surface while `IKernelNetworkStackContract` provides the stable subsystem ABI.

### NIC

A NIC is registered through `KernelNetworking.RegisterInterface`. A driver provides transmit and receive-enable callbacks and receives a `KernelNetworkInterfaceHandle`. The network stack owns interface state, MTU, addresses, routing and neighbor state.

### Ethernet

`KernelNetworkApi.ReceiveEthernet` accepts complete Ethernet frames. EtherType routing dispatches ARP, IPv4 and IPv6 without exposing device-specific details to upper layers.

### ARP and NDP

ARP maintains IPv4-to-MAC neighbor entries. IPv6 Neighbor Discovery processes ICMPv6 Neighbor Solicitation and Neighbor Advertisement options and maintains IPv6-to-MAC neighbor entries. Neighbor tables are owned by the networking subsystem, not individual NIC drivers.

### IPv4 and IPv6

IPv4 retains route selection and per-interface addressing. IPv6 has a standard address and endpoint type and an NDP-aware receive path. Architecture-neutral API structures carry addresses; no x64 assumptions are present in the public network contract.

### ICMP

ICMPv4 echo request/reply handling is part of the stack, and `SendIcmpEchoIpv4` exposes an explicit diagnostic transmit path. ICMPv6 is the transport for NDP and is parsed separately from IPv4 ICMP.

### UDP and TCP

UDP datagrams use route lookup, neighbor resolution, IPv4 framing and socket delivery. TCP exposes the socket state machine and connection observation surface already used by Inu. The API keeps TCP state behind the socket contract so a more complete congestion/retransmission engine can evolve without changing application-facing handles.

### Sockets

`KernelNetworkApi` standardises socket create, bind, connect, listen, receive, UDP send and close operations. `KernelNetworkEndpoint`, `KernelIpv4Endpoint` and `KernelIpv6Endpoint` provide address-family-neutral API shapes.

### DNS

The DNS API supports both A and AAAA records. `BuildDnsQuery` accepts `KernelDnsRecordType.A` or `KernelDnsRecordType.Aaaa`; response parsers return `KernelIpv4Address` or `KernelIpv6Address` respectively.

## Driver boundary

NIC drivers sit underneath `KernelNetworking`. They do not implement ARP, IP, TCP, UDP, sockets or DNS. A driver is responsible for moving Ethernet frames and reporting link receive state. This keeps E1000, RTL8168, VirtIO-net and future hardware behind the same contract.

## Allocation model

The stack uses the Inu kernel heap for registries and fixed caller-owned buffers for packet/query APIs. Public hot-path APIs do not require a managed networking runtime or the desktop .NET socket stack.
