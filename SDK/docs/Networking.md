# Networking

Inu 0.23.0 implements roadmap item 21 as a heap-backed networking stack layered on the item-19 driver framework.

## Driver-facing interfaces

Network-interface drivers register a `KernelDeviceHandle`, MAC address, MTU and `KernelNetworkInterfaceCallbacks`. The networking layer owns protocol details; NIC drivers only transmit complete link-layer frames and enable/disable receive delivery. Received frames enter through `KernelNetworking.ReceiveFrame`.

## Registries and policy

`KernelNetworking.Initialize()` uses `KernelNetworkOptions.DynamicDefault`. Interface, IPv4 route, ARP-neighbour and socket tables start small and grow from the already-initialised kernel heap. `KernelNetworkOptions.Fixed(...)` retains deterministic explicit bounds for RTOS and safety-oriented kernels.

## Protocols

The initial stack provides Ethernet II dispatch, ARP neighbour learning and ARP replies, IPv4 header validation and longest-prefix routing, IPv6 header foundations, ICMP echo replies, UDP datagram transmit/receive and socket delivery, DHCP discovery/offer parsing, DNS A-query/response helpers, plus TCP socket-state foundations for LISTEN, SYN-SENT, SYN-RECEIVED and ESTABLISHED transitions. TCP retransmission, congestion control, ordered stream buffering and timers remain extensions rather than being advertised as complete in this release.

## Socket API

`KernelSockets` provides heap-backed datagram and stream handles with create, bind, connect, listen, send-to, receive, close and state-inspection operations. UDP is usable through the IPv4 stack. TCP exposes connection-state foundations but is not yet a complete production TCP transport.


## 0.14.1 corrective build integration

The 0.14.1 corrective release changes only host-side build/test integration: the template-policy networking reference check is correctly scoped, and `Inu.Networking.Tests` links the testable source surface instead of referencing the freestanding networking project. The kernel networking implementation is unchanged from 0.14.0.
