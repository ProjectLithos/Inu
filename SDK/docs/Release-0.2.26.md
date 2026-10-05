# Inu 0.2.26

## Selectable IPv4 control and transport protocols

- `KernelIpv4Protocol` now owns only IPv4 validation, route selection, IPv4 header construction and Ethernet transmission.
- Added `KernelIpv4Services` as the source-neutral IPv4 send contract used by independently selected IPv4 payload protocols.
- Added `KernelIpv4TransportServices` as the registry/dispatch boundary for ICMPv4, UDP and TCP observation.
- Split ICMPv4 echo handling and transmission into selectable `Kernel.Networking.Icmpv4Protocol`.
- Split UDP-over-IPv4 framing, parsing and optional socket delivery into selectable `Kernel.Networking.UdpIpv4Protocol`.
- Split the current minimal TCP header/state observation path into selectable `Kernel.Networking.TcpObservationProtocol`.
- `KernelNetworkStack` retains the existing high-level UDP and ICMPv4 facade calls but dispatches them through the transport registry rather than through the IPv4 implementation.
- Network capability reporting now reflects the actually registered ICMPv4, UDP and TCP-observation components.
- Standard Networking configurations still select all three components by default, preserving the previous behaviour while allowing each to be omitted independently.

## Component model

The IPv4 core is no longer a bundle of IP, ICMP, UDP and TCP responsibilities. The layer boundary is now explicit: Ethernet dispatch -> IPv4 network layer -> IPv4 transport/control registry -> independently selected control/transport components.
