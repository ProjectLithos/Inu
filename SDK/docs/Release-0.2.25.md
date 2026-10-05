# Inu 0.2.25

## Selectable network-layer protocols

- Added `KernelNetworkPacketServices` as the neutral packet-protocol registration and dispatch boundary beneath the Ethernet facade.
- Split ARP packet processing into selectable `Kernel.Networking.ArpProtocol`.
- Split IPv4 validation, ICMPv4 echo, UDP framing and TCP observation into selectable `Kernel.Networking.Ipv4Protocol`.
- Split IPv6 validation and NDP processing into selectable `Kernel.Networking.Ipv6NdpProtocol`.
- `KernelNetworkStack` no longer directly contains ARP, IPv4, IPv6, NDP, ICMPv4, UDP or TCP packet implementations.
- `KernelNetworkApi.GetCapabilities()` now reports ARP/IPv4/IPv6/NDP availability from registered protocol components instead of hard-coding those capabilities.
- The socket component now declares its real IPv4 dependency in the language-neutral component graph.
- Standard generated Networking configurations select all three protocol providers by default, preserving existing behaviour while allowing them to be omitted independently.

## Scope

This release intentionally keeps ICMPv4, UDP framing and TCP observation inside the IPv4 component. They are separable responsibilities, but they are not promoted to independent Kath choices until their own source-neutral contracts and lifecycle/dependency rules are extracted.
